using System;
using System.Collections.Generic;
namespace CampusCommons.Webcam
{
 public struct StudyInterval {public double Start,End;public string State;}
 public static class StudyTime
 {
  public static long Overlap(long start,long end,long from,long to)=>Math.Max(0,Math.Min(end,to)-Math.Max(start,from));
  public static long DayStart(long utcMs)=>((utcMs+32400000)/86400000)*86400000-32400000;
  public static string Clock(long ms){long seconds=Math.Max(0,ms)/1000;return (seconds/3600).ToString("00")+":"+(seconds/60%60).ToString("00")+":"+(seconds%60).ToString("00");}
 }
 // Capture timestamps, not inference completion count, define the observed intervals.
 public sealed class StudyTracker
 {
  sealed class Sample {public double At;public double[] Scores;}
  readonly List<Sample> samples=new List<Sample>();
  public readonly List<StudyInterval> Intervals=new List<StudyInterval>();
  public double StudySeconds {get;private set;}
  public string Label {get;private set;}
  public bool Counting=>stable && !pending;
  bool stable,pending;double last=double.NaN,cursor,pendingAt,labelAt;string labelCandidate;
  static readonly string[] Names={"studying","phone","other","away","drowsy"};
  public void Observe(double at,double[] scores)
  {
   if(double.IsNaN(at)||double.IsInfinity(at)||scores==null||scores.Length!=5)return;
   double total=0;foreach(var p in scores){if(double.IsNaN(p)||double.IsInfinity(p)||p<0||p>1)return;total+=p;}
   if(Math.Abs(total-1)>.001 || (!double.IsNaN(last)&&at<=last))return;
   if(!double.IsNaN(last)&&at-last>3)Break();
   if(double.IsNaN(last))cursor=at;
   last=at;samples.RemoveAll(x=>at-x.At>3);samples.Add(new Sample{At=at,Scores=(double[])scores.Clone()});
   double[] mean=new double[5];double weight=0;
   foreach(var s in samples){double w=Math.Exp((s.At-at)/.8);weight+=w;for(int i=0;i<5;i++)mean[i]+=w*s.Scores[i];}
   int best=0;for(int i=1;i<5;i++)if(mean[i]>mean[best])best=i;
   string candidate=Names[best];bool study=mean[0]/weight>.5;
   if(candidate!=labelCandidate){labelCandidate=candidate;labelAt=at;}
   if(at-labelAt>=(candidate=="drowsy"?8:candidate=="studying"?3:2))Label=candidate;
   if(study==stable){pending=false;Commit(at,stable?"studying":candidate=="studying"?"other":candidate);}
   else {
    if(!pending){pending=true;pendingAt=at;}
    if(at-pendingAt>=(study?3:2)){
     Commit(pendingAt,stable?"studying":"other");stable=study;pending=false;
     Commit(at,stable?"studying":candidate);
    }
   }
   if(!stable && !pending && Label=="studying")Label="other";
  }
  void Commit(double end,string state)
  {
   if(end<=cursor)return;
   if(state=="studying")StudySeconds+=end-cursor;
   if(Intervals.Count>0 && Intervals[Intervals.Count-1].State==state && Math.Abs(Intervals[Intervals.Count-1].End-cursor)<.00001){var v=Intervals[Intervals.Count-1];v.End=end;Intervals[Intervals.Count-1]=v;}
   else Intervals.Add(new StudyInterval{Start=cursor,End=end,State=state});
   cursor=end;
  }
  public void Break()
  {
   // Unresolved evidence and all time after the last capture remain uncredited.
   samples.Clear();stable=pending=false;last=double.NaN;Label=labelCandidate=null;
  }
 }
}
