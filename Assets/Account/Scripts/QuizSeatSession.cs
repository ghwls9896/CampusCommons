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

        static readonly HttpClient SeatClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(90),
            MaxResponseContentBufferSize = 64 * 1024
        };

        public async Task<QuizSeatAdmission> JoinQuizSeatAsync(int slot, CancellationToken ct)
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
                using (var request = new HttpRequestMessage(HttpMethod.Post, QuizDuelBaseUrl + "/v1/duel/seats/" + slot))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
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
                            admission.state == null || admission.state.self_slot != slot)
                            throw new AccountException(502, "invalid_seat");
                        return admission;
                    }
                }
            }
        }
    }
}
