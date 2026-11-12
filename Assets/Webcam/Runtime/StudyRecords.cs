using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Xml;using System.Xml.Serialization;
namespace CampusCommons.Webcam {
 [Serializable]public class StudySegment {public int sequence;public long start_ms,end_ms;public string state;}
 [Serializable]public class StudyRecord {
  public string user_id,session_id;public long started_ms,ended_ms;public int acked=-1,frozen=-1;public bool end_acked;
  public List<StudySegment> segments=new List<StudySegment>();
 }
 [Serializable]public class StudyDaily {public string user_id;public long day_ms,total_ms;}
 [Serializable]public class StudyArchive {public List<StudyRecord> sessions=new List<StudyRecord>();public List<StudyDaily> totals=new List<StudyDaily>();}
 public sealed class StudyStore:IDisposable {
  [System.Runtime.InteropServices.DllImport("libc",SetLastError=true)]static extern int flock(int fd,int operation);
  FileStream lease;public void Dispose(){lease?.Dispose();lease=null;}
  public StudyArchive Data {get;private set;}=new StudyArchive();readonly string path;
  StudyStore(string path){this.path=path;Directory.CreateDirectory(Path.GetDirectoryName(path));bool windows=Environment.OSVersion.Platform==PlatformID.Win32NT;
   lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,windows?FileShare.None:FileShare.ReadWrite);
   if(!windows&&flock(lease.SafeFileHandle.DangerousGetHandle().ToInt32(),2|4)!=0){Dispose();throw new IOException("Study records already in use");}}
  public static StudyStore Load(string path){var s=new StudyStore(path);if(!File.Exists(path)&&!File.Exists(path+".bak"))return s;
   foreach(var candidate in new[]{path,path+".bak"})try{using(var stream=File.OpenRead(candidate))using(var xml=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null})){s.Data=(StudyArchive)new XmlSerializer(typeof(StudyArchive)).Deserialize(xml);return s;}}catch{}
   s.Dispose();throw new IOException("Study history cannot be loaded");
  }
  public void Save(){Directory.CreateDirectory(Path.GetDirectoryName(path));using(var file=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None)){new XmlSerializer(typeof(StudyArchive)).Serialize(file,Data);file.Flush(true);}
   if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");else File.Move(path+".tmp",path);
  }
  public StudyRecord Begin(string user,long utc){var r=new StudyRecord{user_id=user,session_id=Guid.NewGuid().ToString(),started_ms=utc};Data.sessions.Add(r);return r;}
  public void Append(StudyRecord r,long start,long end,string state){if(end<=start)return;var last=r.segments.LastOrDefault();
   if(last!=null&&last.sequence>r.frozen&&last.state==state&&last.end_ms==start&&end-last.start_ms<=30000){last.end_ms=end;return;}
   r.segments.Add(new StudySegment{sequence=r.segments.Count,start_ms=start,end_ms=end,state=state});
  }
  public StudySegment[] Batch(StudyRecord r){var result=r.segments.Where(x=>x.sequence>r.acked).Take(128).Select(x=>new StudySegment{sequence=x.sequence,start_ms=x.start_ms,end_ms=x.end_ms,state=x.state}).ToArray();if(result.Length>0)r.frozen=Math.Max(r.frozen,result[result.Length-1].sequence);return result;}
  public void Acknowledge(StudyRecord r,int sequence,long day,long total){if(sequence<r.acked||sequence>r.frozen)throw new InvalidDataException("Unexpected study acknowledgement");r.acked=sequence;SetTotal(r.user_id,day,total);}
  public void SetTotal(string user,long day,long total){if(total<0||total>86400000)throw new InvalidDataException("Invalid daily total");var c=Data.totals.Find(x=>x.user_id==user);if(c==null){c=new StudyDaily{user_id=user};Data.totals.Add(c);}if(day>=c.day_ms){c.day_ms=day;c.total_ms=total;}}
  public long Today(string user,long now){long day=StudyTime.DayStart(now);var c=Data.totals.Find(x=>x.user_id==user&&x.day_ms==day);long sum=c?.total_ms??0;
   foreach(var r in Data.sessions)if(r.user_id==user)foreach(var p in r.segments)if(p.sequence>r.acked&&p.state=="studying")sum+=StudyTime.Overlap(p.start_ms,p.end_ms,day,day+86400000);return sum;}
 }
}
