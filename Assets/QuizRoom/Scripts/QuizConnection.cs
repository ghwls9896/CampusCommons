using System;

namespace CampusCommons.Quiz
{
    [Serializable]
    public class QuizDeployment
    {
        public string server_url = "";
        public string access_token = "";
        public bool IsValid()
        {
            return QuizConnection.IsValidUrl(server_url, out _) &&
                !string.IsNullOrWhiteSpace(access_token) && access_token.Length <= 128 &&
                !access_token.StartsWith("AIza", StringComparison.Ordinal) &&
                !access_token.StartsWith("AQ.", StringComparison.Ordinal) &&
                !Array.Exists(access_token.ToCharArray(), c => c < 33 || c > 126);
        }
    }

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
