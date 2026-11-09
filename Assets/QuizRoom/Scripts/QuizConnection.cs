using System;

namespace CampusCommons.Quiz
{
    public static class QuizConnection
    {
        public static bool IsValidUrl(string url, out string error)
        {
            error = "서버 주소는 https://이름.onrender.com 형식으로 입력해 주세요.";
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                uri.AbsolutePath != "/") return false;
            error = null; return true;
        }
    }
    [Serializable] public class QuizErrorEnvelope { public QuizServerError error; }
    [Serializable] public class QuizServerError { public string code; public string message; }
}
