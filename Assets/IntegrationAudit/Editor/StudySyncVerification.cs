using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Account;
using CampusCommons.Webcam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StudySyncVerification
{
    const string Key = "StudySyncVerification";
    const string UserId = "11111111-1111-4111-8111-111111111111";
    static Fixture transport;
    static int stage;
    static double deadline;

    sealed class Fixture : IAccountTransport
    {
        public int Reads, Rejected, Accepted;
        public long RemoteStudy = 1000;

        public Task<T> SendAsync<T>(string method, string path, object body, string bearer, CancellationToken ct) where T : class
        {
            object reply;
            if (typeof(T) == typeof(AuthResponse))
                reply = new AuthResponse { user = new AuthUser { id = UserId, email_confirmed_at = "fixture" },
                    access_token = "fixture", refresh_token = "fixture", expires_in = 3600 };
            else if (typeof(T) == typeof(AccountProfile))
                reply = new AccountProfile { user_id = UserId, nickname = "테스트", level = 1 };
            else
            {
                if (path != "/functions/v1/study" || bearer != "fixture")
                    throw new Exception("Study API must use the authenticated account transport");
                var response = new StudyReply { user_id = UserId, day_ms = StudyTime.DayStart(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) };
                if (body is StudySyncRequest write)
                {
                    if (Rejected == 0)
                    {
                        Rejected++;
                        throw new AccountException(409, "record_conflict");
                    }
                    Accepted++;
                    RemoteStudy += write.segments.Where(x => x.state == "studying").Sum(x => x.end_ms - x.start_ms);
                    response.session_id = write.session_id;
                    response.ack_sequence = write.segments.Last().sequence;
                }
                else Reads++;
                response.today_ms = RemoteStudy;
                reply = response;
            }
            return Task.FromResult((T)reply);
        }
    }

    public static void Run()
    {
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        deadline = EditorApplication.timeSinceStartup + 20;
        EditorApplication.update += Tick;
    }

    static async void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            var service = StudyRecordService.Instance;
            if (!service || !AccountGate.Instance) return;
            if (EditorApplication.timeSinceStartup > deadline)
                throw new Exception(stage == 2 ? "Manual sync never fetched the changed remote total" :
                    "A conflicting session blocked a subsequent valid upload");
            if (stage == 0)
            {
                stage = 1;
                transport = new Fixture();
                var account = AccountGate.Instance;
                account.Configure(transport);
                await account.LoginAsync("study@example.test", "fixture");
                await account.EnterAsync("테스트");
            }
            else if (stage == 1 && service.TodayMilliseconds == 1000)
            {
                transport.RemoteStudy = 2000;
                service.RetrySync();
                stage = 2;
                deadline = EditorApplication.timeSinceStartup + 3;
            }
            else if (stage == 2 && service.TodayMilliseconds == 2000)
            {
                for (int session = 0; session < 2; session++)
                {
                    if (!service.Begin()) throw new Exception("Study record store did not start");
                    double now = Time.realtimeSinceStartupAsDouble;
                    for (int second = 0; second <= 3; second++)
                        service.Observe(now + second, new double[] { 1, 0, 0, 0, 0 });
                    service.Finish();
                }
                stage = 3;
                deadline = EditorApplication.timeSinceStartup + 3;
            }
            else if (stage == 3 && transport.Accepted == 1 && service.TodayMilliseconds == 5000)
            {
                if (transport.Reads < 2 || transport.Rejected != 1)
                    throw new Exception("Unexpected refresh or conflict retry");
                Debug.Log("STUDY_SYNC_PLAY_PASS: refreshed another device's total, preserved conflict, saved following session");
                Finish(0);
            }
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Finish(1);
        }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
