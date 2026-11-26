using System;using System.IO;using CampusCommons.Webcam;
class StoreTests {
 static int n;static void C(bool b,string m){n++;if(!b)throw new Exception(m);}
 static void Main(string[] args){
  if (args.Length == 3)
  {
   using (var other = StudyStore.OpenForAccount(args[0], args[1]))
   {
    long day = long.Parse(args[2]);
    other.Append(other.Begin(args[1], day), day, day + 4000, "studying");
    other.Save();
   }
   return;
  }
  if(args.Length>0){try{StudyStore.Load(args[0]);Environment.Exit(1);}catch(IOException){Environment.Exit(0);}return;}string path=Path.Combine(Path.GetTempPath(),"study-"+Guid.NewGuid()+".xml");try{
  var store=StudyStore.Load(path);long day=StudyTime.DayStart(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
  var a=store.Begin("a",day);store.Append(a,day,day+1000,"studying");store.Append(a,day+1000,day+2000,"studying");C(a.segments.Count==1,"coalesce unsent contiguous interval");
  var batch=store.Batch(a);store.Append(a,day+2000,day+3000,"studying");C(a.segments.Count==2&&batch[0].end_ms==day+2000,"freeze request before extending");store.Save();
  var probe=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo{FileName=System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,Arguments="\""+System.Reflection.Assembly.GetExecutingAssembly().Location+"\" \""+path+"\"",UseShellExecute=false});probe.WaitForExit();C(probe.ExitCode==0,"another process cannot overwrite active outbox");
  store.Dispose();var reloaded=StudyStore.Load(path);C(reloaded.Today("a",day)==3000,"durable pending totals");C(reloaded.Today("b",day)==0,"account separated");
  reloaded.Acknowledge(reloaded.Data.sessions[0],0,day,2000);C(reloaded.Today("a",day)==3000,"ack cannot double total");reloaded.Acknowledge(reloaded.Data.sessions[0],0,day,2000);C(reloaded.Today("a",day)==3000,"repeated ack idempotent");
  reloaded.Append(reloaded.Data.sessions[0],day+86400000-1000,day+86400000+2000,"studying");C(reloaded.Today("a",day+86400000)==2000,"KST midnight pending split");reloaded.Save();
  reloaded.Dispose();File.WriteAllText(path,"broken");C(StudyStore.Load(path).Data.sessions.Count==1,"recover last atomic backup");
  CheckAccountWindows(day);
  Console.WriteLine("STUDY_STORE_PASS "+n);
 }finally{foreach(var f in new[]{path,path+".bak",path+".tmp"})if(File.Exists(f))File.Delete(f);}}
 static void CheckAccountWindows(long day)
 {
  string directory = Path.Combine(Path.GetTempPath(), "cc-study-windows-" + Guid.NewGuid());
  string legacy = Path.Combine(directory, "study-records.xml");
  const string a = "11111111-1111-4111-8111-111111111111";
  const string b = "22222222-2222-4222-8222-222222222222";
  try
  {
   using (var old = StudyStore.Load(legacy))
   {
    old.Append(old.Begin(a, day), day, day + 1000, "studying");
    old.SetTotal(b, day, 3000);
    old.Save();
    bool busy = false;
    try { using (StudyStore.OpenForAccount(legacy, a)) {} } catch (IOException) { busy = true; }
    C(busy, "an old running writer delays migration without creating an empty account archive");
   }
   using (var first = StudyStore.OpenForAccount(legacy, a))
   {
    var start = new System.Diagnostics.ProcessStartInfo {
     FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,
     Arguments = "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\" \"" + legacy + "\" " + b + " " + day,
     UseShellExecute = false
    };
    using (var child = System.Diagnostics.Process.Start(start))
    {
     C(child.WaitForExit(10000) && child.ExitCode == 0, "second game process can start and save while the first account holds its lease");
    }
    C(first.Today(a, day) == 1000 && first.Data.totals.Count == 0, "first account inherits only its own pending history");
    first.Data.sessions.Clear(); // The old pending entry has been acknowledged by the server.
    first.Append(first.Begin(a, day + 1000), day + 1000, day + 3000, "studying");
    first.Save();
    bool locked = false;
    try { using (StudyStore.OpenForAccount(legacy, a)) {} } catch (IOException) { locked = true; }
    C(locked, "same account cannot overwrite active study records");
   }
   using (var second = StudyStore.OpenForAccount(legacy, b))
   using (var first = StudyStore.OpenForAccount(legacy, a))
   {
    C(first.Today(a, day) == 2000 && second.Today(b, day) == 7000, "both windows persist across reversed startup order");
    C(first.Data.sessions.Count == 1 && second.Data.sessions.Count == 1, "reopening does not reimport acknowledged or other accounts' records");
   }
  }
  finally { Directory.Delete(directory, true); }
 }
}
