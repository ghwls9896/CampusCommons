using System;using System.IO;using CampusCommons.Webcam;
class StoreTests {
 static int n;static void C(bool b,string m){n++;if(!b)throw new Exception(m);}
 static void Main(string[] args){if(args.Length>0){try{StudyStore.Load(args[0]);Environment.Exit(1);}catch(IOException){Environment.Exit(0);}return;}string path=Path.Combine(Path.GetTempPath(),"study-"+Guid.NewGuid()+".xml");try{
  var store=StudyStore.Load(path);long day=StudyTime.DayStart(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
  var a=store.Begin("a",day);store.Append(a,day,day+1000,"studying");store.Append(a,day+1000,day+2000,"studying");C(a.segments.Count==1,"coalesce unsent contiguous interval");
  var batch=store.Batch(a);store.Append(a,day+2000,day+3000,"studying");C(a.segments.Count==2&&batch[0].end_ms==day+2000,"freeze request before extending");store.Save();
  var probe=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo{FileName=System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,Arguments="\""+System.Reflection.Assembly.GetExecutingAssembly().Location+"\" \""+path+"\"",UseShellExecute=false});probe.WaitForExit();C(probe.ExitCode==0,"another process cannot overwrite active outbox");
  store.Dispose();var reloaded=StudyStore.Load(path);C(reloaded.Today("a",day)==3000,"durable pending totals");C(reloaded.Today("b",day)==0,"account separated");
  reloaded.Acknowledge(reloaded.Data.sessions[0],0,day,2000);C(reloaded.Today("a",day)==3000,"ack cannot double total");reloaded.Acknowledge(reloaded.Data.sessions[0],0,day,2000);C(reloaded.Today("a",day)==3000,"repeated ack idempotent");
  reloaded.Append(reloaded.Data.sessions[0],day+86400000-1000,day+86400000+2000,"studying");C(reloaded.Today("a",day+86400000)==2000,"KST midnight pending split");reloaded.Save();
  reloaded.Dispose();File.WriteAllText(path,"broken");C(StudyStore.Load(path).Data.sessions.Count==1,"recover last atomic backup");
  Console.WriteLine("STUDY_STORE_PASS "+n);
 }finally{foreach(var f in new[]{path,path+".bak",path+".tmp"})if(File.Exists(f))File.Delete(f);}}
}
