using System;
using CampusCommons.Webcam;
public static class StudySessionChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        var s=new StudySession();
        Check(!s.Begin(false,true),"Cannot start away from a seat");
        Check(!s.Begin(true,false),"Cannot start without focus");
        Check(s.Begin(true,true),"Seated start");int epoch=s.Epoch;
        Check(s.Apply(epoch,"drowsy"),"Accept current result");
        Check(s.Label=="자는중","Requested label");
        s.Observe(false,true);Check(!s.Running,"Standing ends capture");
        Check(!s.Apply(epoch,"studying"),"Reject late response");
        s.Observe(true,true);Check(!s.Running,"Reseating does not restart");
        Check(s.Begin(true,true),"Restart");s.Observe(true,false);
        Check(s.Running,"Focus loss preserves capture");
        Check(StudySession.LabelFor("away")=="자리비움","Away label");
        Check(StudySession.LabelFor("phone")=="휴대폰 사용중","Phone label");
        Check(StudySession.LabelFor("other")=="다른활동중","Other label");
        Check(StudySession.LabelFor("studying")=="공부중","Study label");
        s.Begin(true,true);s.FrameSeen(10);
        Check(!s.CameraStalled(14),"Short frame gap allowed");
        Check(s.CameraStalled(16),"Disconnected camera detected");
        s.Stop();Check(!s.CameraStalled(30),"Stopped session has no stale camera");
        Console.WriteLine("STUDY_SESSION_PASS");
    }
}
