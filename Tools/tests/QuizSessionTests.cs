using System;
using CampusCommons.Quiz;

public static class QuizSessionTests
{
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static QuizSet Set()
    {
        var data = new QuizSet { questions = new QuizQuestion[3] };
        for (int i = 0; i < 3; i++) data.questions[i] = new QuizQuestion {
            level = i + 1, prompt = "문제 " + i, answer_type = new[]{"multiple_choice","short_answer","written"}[i],
            options = i==0?new[] { "가", "나", "다", "라" }:new string[0], model_answer="가", grading_criteria=i==0?new QuizGradingCriterion[0]:new[]{new QuizGradingCriterion {description="핵심 의미",max_points=10}},
            correct_index = i==0?0:-1, explanation = "해설", source_page = i + 1, source_quote = "근거"
        };
        return data;
    }
    public static int Main()
    {
        var session = new QuizSession();
        Check(!session.SubmitChoice(0,0),"Cannot answer before loading");
        session.Begin(Set(),100);
        Check(session.TimeLimit==10 && session.Remaining(100)==10,"First deadline10 seconds");
        Check(!session.Tick(109.99),"Wait until deadline");
        Check(session.SubmitChoice(0,109) && session.Index==1 && !session.Finished,"Submit immediately advances");
        Check(session.TimeLimit==15 && session.Responses[0]=="가" && !session.SubmitChoice(0,109),"No repeated choice submission");
        Check(session.SubmitText("선입선출",2,110) && session.Index==2,"Short answer immediately advances");
        Check(!session.SubmitText("double click",2,110),"Stale level cannot answer next question");
        Check(session.TimeLimit==20 && session.Tick(130,-1,"스택은 후입선출, 큐는 선입선출"),"20 second timeout submits current draft");
        Check(session.Finished && session.Responses[2].Contains("큐") && session.TimedOut[2],"Keep auto-submitted draft for final review");
        Check(session.ElapsedSeconds[0]==9 && session.ElapsedSeconds[1]==1 && session.ElapsedSeconds[2]==20 && session.TotalElapsed==30,"Record exact solve duration and clamp timeout to deadline");
        Check(session.NeedsGrading && !session.Tick(200),"AI grading only after final question");
        Check(session.ApplyGrades(new QuizGradeSet { grades=new[]{new QuizGrade {level=2,score=7,criterion_scores=new[]{7},feedback="핵심 일부 충족"},new QuizGrade {level=3,score=4,criterion_scores=new[]{4},feedback="핵심 일부 누락"}}}),"Apply semantic grades");
        Check(session.Score==21 && !session.NeedsGrading,"Final aggregate score");
        Check(!session.ApplyGrades(new QuizGradeSet {grades=new[]{new QuizGrade {level=2,score=11,criterion_scores=new[]{11},feedback="bad"},new QuizGrade {level=3,score=4,criterion_scores=new[]{4},feedback="ok"}}}) && session.Score==21,"Reject invalid marks without overwriting valid result");
        session.Begin(Set(),0);
        Check(session.SubmitChoice(0,10) && session.Index==1 && session.TimedOut[0],"Deadline submit auto-records current choice");
        Check(session.Tick(25) && session.Index==2 && session.Responses[1]=="","15 second empty timeout");
        Check(session.Tick(45) && session.Finished && session.Score==10 && !session.NeedsGrading,"All blank timeouts finish once without AI");
        session.Begin(Set(),0);Check(session.Tick(10,0) && session.Score==10,"Previously selected choice auto-submits at timeout");
        session.Begin(Set(),0);session.SubmitChoice(0,1);
        Check(session.SubmitText("마감 직전 입력한 답",2,16) && session.Index==2 && session.TimedOut[1] && session.Responses[1]=="마감 직전 입력한 답","Deadline edge preserves draft and reports transition for UI reset");
        Check(!session.SubmitText("같은 버튼의 두 번째 클릭",2,16) && session.Index==2,"Deadline transition cannot skip written question");
        session.Reset(); Check(session.Current==null && !session.Finished && session.Score==0,"Leave clears state");
        var bad = Set(); bad.questions[1].level = 1;
        Check(!bad.IsValid(out _), "Duplicate level rejected");
        bad = Set(); bad.questions[0].options[1] = "가";
        Check(!bad.IsValid(out _), "Duplicate choices rejected");
        bad = Set(); bad.questions[0].correct_index = 4;
        Check(!bad.IsValid(out _), "Bad answer index rejected");
        bad = Set(); bad.questions[0].source_page = 0;
        Check(!bad.IsValid(out _), "Source required");
        bad = Set(); bad.questions[2].explanation = " ";
        Check(!bad.IsValid(out _), "Explanation required");
        Check(QuizConnection.IsValidUrl("https://example.onrender.com", out _), "HTTPS accepted");
        Check(!QuizConnection.IsValidUrl("http://example.com", out _), "Remote HTTP refused");
        Check(!QuizConnection.IsValidUrl("https://user:password@example.com", out _), "Credentials in URL refused");
        Check(!QuizConnection.IsValidUrl("https://example.com?key=secret", out _), "Query in base URL refused");
        var deployment = new QuizDeployment { server_url = "https://example.onrender.com", access_token = "team-test-code" };
        Check(deployment.IsValid(), "Bundled deployment can connect without player input");
        deployment.access_token = "";
        Check(!deployment.IsValid(), "Missing deployment code must not send an anonymous request");
        deployment.access_token = "code\r\nInjected: header";
        Check(!deployment.IsValid(), "Deployment code cannot inject a request header");
        deployment.access_token = new string('x', 129);
        Check(!deployment.IsValid(), "Overlong deployment code rejected");
        deployment.access_token = "AQ.provider-key-do-not-ship";
        Check(!deployment.IsValid(), "Gemini auth key must never be used as a client access code");
        deployment.access_token = "AIza-provider-key-do-not-ship";
        Check(!deployment.IsValid(), "Gemini standard key must never be used as a client access code");
        deployment.access_token = "team-test-code"; deployment.server_url = "http://example.com";
        Check(!deployment.IsValid(), "Configured endpoint must still enforce HTTPS");
        Console.WriteLine("QUIZ_SESSION_PASS " + checks + " checks"); return 0;
    }
}
