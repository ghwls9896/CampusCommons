using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Webcam;
public static class RuntimePackageInstallerTests
{
 static int checks;
 static string temp;
 static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static void Reject(Action action,string why){bool rejected=false;try{action();}catch(Exception){rejected=true;}Check(rejected,why);}
 static RuntimePackageManifest Package(string name,string extra=null,bool symlink=false)
 {
  string path=Path.Combine(temp,name+".zip");long total=0;
  using(var fs=File.Create(path))using(var zip=new ZipArchive(fs,ZipArchiveMode.Create))
  foreach(var file in new[]{"python/bin/python3.12","app/live.py","model/manifest.json",extra})
  {
   if(file==null)continue;var entry=zip.CreateEntry(file);if(symlink&&file==extra)entry.ExternalAttributes=(0xA1FF<<16);
   using(var writer=new StreamWriter(entry.Open()))writer.Write("test");total+=4;
  }
  return new RuntimePackageManifest{schema=1,platform="macos-arm64",archive=Path.GetFileName(path),sha256=RuntimePackageInstaller.HashFile(path,CancellationToken.None),archiveBytes=new FileInfo(path).Length,unpackedBytes=total,python="python/bin/python3.12",executablePaths=new[]{"python/bin/python3.12"}};
 }
 static string Install(RuntimePackageManifest p,string target=null,bool force=false,CancellationToken token=default(CancellationToken))
 {return RuntimePackageInstaller.Install(p,Path.Combine(temp,p.archive),target??Path.Combine(temp,"installed"),"macos-arm64",null,token,force);}
 public static int Main(string[] args)
 {
  if(args.Length>0) {
   using(RuntimePackageInstaller.AcquireUsage(args[0])) {
    File.WriteAllText(args[0]+".active","ready");
    for(int n=0;n<200&&!File.Exists(args[0]+".release");n++)Thread.Sleep(50);
   }
   return 0;
  }
  return Run();
 }
 public static int Run()
 {
  temp=Path.Combine(Path.GetTempPath(),"runtime test 한글 "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  try{
   var p=Package("good");string root=Install(p);Check(File.Exists(Path.Combine(root,p.python)),"fresh install");Check(Install(p)==root,"cached install");
   Check(Path.GetFileName(root)==p.sha256.Substring(0,16),"runtime directory is not shortened");
   Check(File.ReadAllText(Path.Combine(root,"ready")).StartsWith(p.sha256+"\n",StringComparison.Ordinal),"ready marker lost full hash");
   string collisionBase=Path.Combine(temp,"collision"), collisionRoot=Path.Combine(collisionBase,p.sha256.Substring(0,16));
   Directory.CreateDirectory(collisionRoot);
   string otherHash=p.sha256.Substring(0,16)+new string(p.sha256[16]=='0'?'1':'0',48);
   File.WriteAllText(Path.Combine(collisionRoot,"ready"),otherHash+"\nexisting");
   File.WriteAllText(Path.Combine(collisionRoot,"keep"),"existing runtime");
   Reject(()=>Install(p,collisionBase),"short-prefix collision accepted");
   Reject(()=>Install(p,collisionBase,force:true),"force repair overwrote a hash collision");
   Check(File.ReadAllText(Path.Combine(collisionRoot,"keep"))=="existing runtime","collision modified existing runtime");
   File.Delete(Path.Combine(root,"app/live.py"));Install(p);Check(File.Exists(Path.Combine(root,"app/live.py")),"repair missing entrypoint");
   File.WriteAllText(Path.Combine(root,"app/live.py"),"broken");Install(p,force:true);Check(File.ReadAllText(Path.Combine(root,"app/live.py"))=="test","force repair");
   foreach(var bad in new[]{"../escape","/absolute","C:/escape","dir\\escape","app/../../escape","app/LIVE.py","APP/other.py"}){var b=Package("bad"+checks,bad);Reject(()=>Install(b),"unsafe path accepted: "+bad);}
   var sym=Package("symlink","link",true);Reject(()=>Install(sym),"symlink accepted");
   var corrupt=Package("corrupt");File.AppendAllText(Path.Combine(temp,corrupt.archive),"tamper");Reject(()=>Install(corrupt),"size/hash mismatch accepted");
   var wrong=Package("wrong");wrong.platform="windows-x64";Reject(()=>Install(wrong),"wrong platform accepted");
   var size=Package("size");size.unpackedBytes=3;Reject(()=>Install(size),"size overflow accepted");
   var path=Package("pythonpath");path.python="../escape";Reject(()=>Install(path),"unsafe Python path accepted");
   using(var active=RuntimePackageInstaller.AcquireUsage(root)) {
    Reject(()=>Install(p,force:true),"repair replaced an active runtime");
    Check(File.ReadAllText(Path.Combine(root,"app/live.py"))=="test","active runtime was modified");
   }
   using(var one=RuntimePackageInstaller.AcquireUsage(root))using(var two=RuntimePackageInstaller.AcquireUsage(root)) Check(one.CanRead&&two.CanRead,"shared use blocked");
   var child=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, "\""+typeof(RuntimePackageInstallerTests).Assembly.Location+"\" \""+root+"\""){UseShellExecute=false});
   try {
    for(int n=0;n<100&&!File.Exists(root+".active");n++)Thread.Sleep(50);
    Check(File.Exists(root+".active"),"child lease not acquired");
    Reject(()=>Install(p,force:true),"repair ignored another process lease");
    Check(File.ReadAllText(Path.Combine(root,"app/live.py"))=="test","cross-process repair touched active files");
   }finally{File.WriteAllText(root+".release","");if(!child.WaitForExit(5000))child.Kill();child.Dispose();}
   Check(Directory.Exists(Install(p,force:true)),"repair did not resume after lease release");
   var cts=new CancellationTokenSource();cts.Cancel();Reject(()=>Install(p,Path.Combine(temp,"cancel"),token:cts.Token),"cancel ignored");
   Check(!Directory.Exists(Path.Combine(temp,"cancel",p.sha256.Substring(0,16))),"cancel installed ready directory");
   var mid=Package("mid");var midCancel=new CancellationTokenSource();string midBase=Path.Combine(temp,"mid");
   Reject(()=>RuntimePackageInstaller.Install(mid,Path.Combine(temp,mid.archive),midBase,"macos-arm64",(v,msg)=>{if(v>.1f)midCancel.Cancel();},midCancel.Token),"mid-extraction cancel ignored");
   Check(!Directory.Exists(Path.Combine(midBase,mid.sha256.Substring(0,16)))&&!Directory.Exists(Path.Combine(midBase,mid.sha256.Substring(0,16)+".installing")),"partial install left after cancel");
   Check(Directory.Exists(Install(mid,midBase)),"retry after interruption failed");
   string concurrent=Path.Combine(temp,"concurrent");Task.WaitAll(Task.Run(()=>Install(p,concurrent)),Task.Run(()=>Install(p,concurrent)));Check(File.Exists(Path.Combine(concurrent,p.sha256.Substring(0,16),"ready")),"concurrent install failed");
   var retry=Package("retry","../bad");Reject(()=>Install(retry),"bad initial package");var good=Package("retry");Check(Directory.Exists(Install(good)),"retry did not recover");
   Console.WriteLine("RUNTIME_INSTALLER_PASS "+checks+" checks");return 0;
  }finally{Directory.Delete(temp,true);}
 }
}
