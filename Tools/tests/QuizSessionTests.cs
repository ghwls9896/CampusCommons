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
            level = i + 1, prompt = "문제 " + i, options = new[] { "가", "나", "다", "라" },
            correct_index = i, explanation = "해설", source_page = i + 1, source_quote = "근거"
        };
        return data;
    }
    public static int Main()
    {
        var session = new QuizSession();
        Check(!session.Submit(0) && !session.Next(), "Cannot answer before loading");
        session.Begin(Set());
        Check(session.Current.level == 1 && !session.Submitted && session.Score == 0, "Starts at level one");
        Check(!session.Next() && !session.Submit(4), "Cannot skip or submit out of range");
        Check(session.Submit(0) && session.Submitted && session.Score == 1, "Grade first answer");
        Check(!session.Submit(1) && session.Score == 1, "Double click must not regrade");
        Check(session.Next() && session.Current.level == 2 && !session.Submitted, "Advance and hide next answer");
        Check(session.Submit(0) && session.Score == 1, "Wrong answer does not score");
        Check(session.Next() && session.Submit(2) && session.Score == 2, "Third question grades");
        Check(session.Next() && session.Finished && !session.Submit(2) && !session.Next(), "Finish once");
        Check(session.Answers[0] == 0 && session.Answers[1] == 0 && session.Answers[2] == 2, "Keep answers for review");
        session.Reset(); Check(session.Current == null && !session.Finished && session.Score == 0, "Leave clears state");
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
