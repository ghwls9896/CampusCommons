using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Quiz;
using UnityEngine;

namespace CampusCommons.Account
{
    public sealed partial class AccountSession
    {
        // Derive the HTTP origin from the same fixed deployment as the signed-in lobby.
        public static string QuizDuelBaseUrl =>
            new UriBuilder(LobbyEndpoint.Scheme == "wss" ? "https" : "http", LobbyEndpoint.Host, LobbyEndpoint.Port)
                .Uri.GetLeftPart(UriPartial.Authority);

        static readonly HttpClient SeatClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            Timeout = TimeSpan.FromSeconds(90),
            MaxResponseContentBufferSize = 64 * 1024
        };

        public Task<QuizSeatAdmission> JoinQuizSeatAsync(int slot, CancellationToken ct) =>
            AuthorizeQuizSeatAsync(slot, null, ct);

        public async Task<long> RenewQuizSeatAsync(int slot, string ticket, CancellationToken ct)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                return (await AuthorizeQuizSeatAsync(slot, ticket, timeout.Token)).account_expires_at;
            }
        }

        async Task<QuizSeatAdmission> AuthorizeQuizSeatAsync(int slot, string ticket, CancellationToken ct)
        {
            if (slot != 0 && slot != 1) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!HasSession || RecoveryPending || Profile == null || Profile.user_id != UserId ||
                string.IsNullOrWhiteSpace(Profile.nickname))
                throw new AccountException(401, "not_signed_in");

            int expectedGeneration = generation;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token))
            {
                await EnsureFreshAsync(linked.Token);
                Current(expectedGeneration, linked.Token);
                if (!IsAuthenticated) throw new AccountException(401, "not_signed_in");
                string path = ticket == null ? "seats/" + slot : "seat/renew";
                using (var request = new HttpRequestMessage(HttpMethod.Post, QuizDuelBaseUrl + "/v1/duel/" + path))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
                    if (ticket != null) request.Headers.Add("X-Quiz-Seat-Ticket", ticket);
                    using (var response = await SeatClient.SendAsync(request, linked.Token))
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        Current(expectedGeneration, linked.Token);
                        if (!response.IsSuccessStatusCode)
                        {
                            string code = "seat_unavailable";
                            try { code = JsonUtility.FromJson<QuizErrorEnvelope>(body)?.error?.code ?? code; }
                            catch (ArgumentException) { }
                            throw new AccountException((int)response.StatusCode, code);
                        }
                        var admission = JsonUtility.FromJson<QuizSeatAdmission>(body);
                        if (admission == null || string.IsNullOrWhiteSpace(admission.ticket) ||
                            admission.state == null || admission.state.self_slot != slot ||
                            (ticket != null && (admission.ticket != ticket || admission.account_expires_at <= 0)))
                            throw new AccountException(502, "invalid_seat");
                        return admission;
                    }
                }
            }
        }
    }
}
