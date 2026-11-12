using System;
using System.Linq;
using CampusCommons.Webcam;
class StudyTests {
 static int n;static void Check(bool v,string m){n++;if(!v)throw new Exception(m);}
 static double[] P(int i){var p=new double[5];p[i]=1;return p;}
 static void Main(){
  var s=new StudyTracker();for(int i=0;i<=10;i++)s.Observe(i,P(0));Check(Math.Abs(s.StudySeconds-10)<.001,"study backcredits first three seconds");
  s.Observe(11,P(1));s.Observe(12,P(0));s.Observe(13,P(0));Check(s.StudySeconds>=12,"single-frame error recovers held time");
  for(int i=14;i<=20;i++)s.Observe(i,P(i%2==0?1:2));Check(s.StudySeconds<18,"mixed nonstudy cannot keep study timer running");
  var before=s.StudySeconds;s.Observe(60,P(0));Check(s.StudySeconds==before,"gap must not become study");for(int i=61;i<=64;i++)s.Observe(i,P(0));Check(s.StudySeconds<=before+4.001,"only post-gap evidence accrues");
  s.Break();before=s.StudySeconds;s.Observe(100,P(0));s.Observe(101,P(0));s.Break();Check(s.StudySeconds==before,"unconfirmed restart excluded");
  s=new StudyTracker();for(int i=0;i<=10;i++)s.Observe(i,P(0));for(int i=11;i<=16;i++)s.Observe(i,P(4));Check(s.Label!="drowsy","sleep display requires eight seconds");
  bool earlySleep=false;s.DrainIntervals(interval=>earlySleep|=interval.State=="drowsy");Check(!earlySleep,"history should not report sleep before the display confirms it");Check(s.StudySeconds<15,"sleep confirmation delay does not count as studying");for(int i=17;i<=23;i++)s.Observe(i,P(4));Check(s.Label=="drowsy","persistent sleep shown");
  before=s.StudySeconds;s.Observe(22,P(0));Check(s.StudySeconds==before,"out of order ignored");
  s=new StudyTracker();for(int i=0;i<=10;i++)s.Observe(i,P(0));
  for(int i=11;i<=20;i++)s.Observe(i,new double[]{.4,.2,.2,.1,.1});
  Check(s.StudySeconds<13,"largest individual study score must not count when combined nonstudy wins");
  var day=new DateTimeOffset(2026,11,12,0,0,0,TimeSpan.FromHours(9)).ToUnixTimeMilliseconds();
  Check(StudyTime.Overlap(day-1000,day+3000,day,day+86400000)==3000,"midnight split");
  Console.WriteLine("STUDY_CORE_PASS "+n);
 }
}
