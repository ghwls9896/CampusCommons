using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Account;
using UnityEngine;

namespace CampusCommons.Quiz
{
    [Serializable] public sealed class QuizSeatAdmission
    {
        public string ticket;
        public QuizDuelState state;
    }

    [Serializable] public sealed class QuizDuelState
    {
        public string phase, round_id, message;
        public int self_slot, target_level, remaining_ms;
        public bool can_upload;
        public QuizDuelPlayer[] players;
        public QuizDuelQuestion question;
        public QuizDuelResult[] results;
        public QuizDuelReview[] review;

        public QuizDuelPlayer Self => Array.Find(players, p => p != null && p.slot == self_slot);
    }

    [Serializable] public sealed class QuizDuelPlayer
    {
        public int slot, level;
        public string nickname;
        public bool ready, finished;
    }

    [Serializable] public sealed class QuizDuelQuestion
    {
        public int level;
        public string answer_type, prompt;
        public string[] options;
    }

    [Serializable] public sealed class QuizDuelResult
    {
        public int slot, score, correct, elapsed_ms;
        public string nickname;
        public float accuracy;
        public QuizDuelStage[] stages;
    }

    [Serializable] public sealed class QuizDuelStage
    {
        public int level, score, elapsed_ms;
    }

    [Serializable] public sealed class QuizDuelReview
    {
        public int level, source_page, score;
        public string prompt, answer, model_answer, explanation, feedback;
    }

    [Serializable] internal sealed class QuizDuelAnswer
    {
        public string round_id, text;
        public int level, choice;
    }

    internal static class QuizDuelApi
    {
        static readonly HttpClient Client = new HttpClient(new HttpClientHandler {
            AllowAutoRedirect = false, UseCookies = false
        }) { Timeout = TimeSpan.FromSeconds(45), MaxResponseContentBufferSize = 256 * 1024 };

        internal static async Task<QuizDuelState> Request(string ticket, HttpMethod method, string path,
            byte[] body, string contentType, CancellationToken token)
        {
            using (var request = new HttpRequestMessage(method, AccountSession.QuizDuelBaseUrl + "/v1/duel/" + path))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ticket);
                if (body != null)
                {
                    request.Content = new ByteArrayContent(body);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }
                using (var reply = await Client.SendAsync(request, token))
                {
                    string json = await reply.Content.ReadAsStringAsync();
                    token.ThrowIfCancellationRequested();
                    if (!reply.IsSuccessStatusCode)
                    {
                        QuizErrorEnvelope envelope = null;
                        try { envelope = JsonUtility.FromJson<QuizErrorEnvelope>(json); }
                        catch (ArgumentException) { }
                        throw new QuizDuelFailure((int)reply.StatusCode,
                            envelope?.error?.message ?? "퀴즈 서버에 연결할 수 없습니다.");
                    }
                    if (method == HttpMethod.Delete) return null;
                    var state = JsonUtility.FromJson<QuizDuelState>(json);
                    if (state == null || string.IsNullOrEmpty(state.phase) || state.players == null ||
                        state.self_slot < 0 || state.self_slot > 1 || state.Self == null)
                        throw new QuizDuelFailure(502, "퀴즈 상태를 다시 확인하고 있습니다.");
                    return state;
                }
            }
        }
    }

    internal sealed class QuizDuelFailure : Exception
    {
        public readonly int Status;
        public QuizDuelFailure(int status, string message) : base(message) { Status = status; }
    }
}
