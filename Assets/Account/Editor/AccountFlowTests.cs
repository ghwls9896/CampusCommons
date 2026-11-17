using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.GrandLibrary;
using CampusCommons.Webcam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusCommons.Account.Editor
{
    // Actual player/scene lifecycle with local authentication and profile responses.
    public static class AccountFlowTests
    {
        const string Key = "AccountFlowTest";
        const string UserId = "00000000-0000-4000-8000-000000000008";

        sealed class Fixture : IAccountTransport
        {
            public string Nickname = "이전닉네임";
            public bool RefreshFails;
            public int NicknameWrites;
            public TaskCompletionSource<AccountProfile> DelayedRead = new TaskCompletionSource<AccountProfile>();

            public AccountProfile Profile() => new AccountProfile
            {
                user_id = UserId, nickname = Nickname, level = 15
            };

            public Task<T> SendAsync<T>(string method, string path, object body, string token, CancellationToken ct)
                where T : class
            {
                if (path.Contains("refresh_token") && RefreshFails)
                    throw new AccountException(503, "connection_failed");
                if (body is ReadRequest && DelayedRead != null)
                    return (Task<T>)(object)DelayedRead.Task;

                object value = null;
                if (typeof(T) == typeof(AuthResponse))
                    value = new AuthResponse
                    {
                        access_token = "fixture", refresh_token = "fixture", expires_at = 4000000000,
                        user = new AuthUser { id = UserId, email_confirmed_at = "date" }
                    };
                else if (typeof(T) == typeof(AccountProfile))
                {
                    if (body is ProfileRequest edit)
                    {
                        Nickname = edit.nickname;
                        NicknameWrites++;
                    }
                    value = Profile();
                }
                else if (typeof(T) == typeof(StudyReply))
                    value = new StudyReply
                    {
                        user_id = UserId,
                        day_ms = StudyTime.DayStart(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                    };
                return Task.FromResult((T)value);
            }
        }

        static int step;
        static double deadline;
        static Task pending;
        static Fixture fixture;
        static AccountGate gate;
        static Player player;
        static bool runtimeError;
        static float expiredAt;

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 45;
            Application.logMessageReceived += Log;
            EditorApplication.update += Tick;
        }

        static void Log(string message, string stack, LogType type)
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Exception || type == LogType.Error) runtimeError = true;
        }

        static bool BackdropVisible()
        {
            foreach (var camera in Camera.allCameras)
                if (camera.enabled && camera.cullingMask == 0 && camera.clearFlags == CameraClearFlags.SolidColor)
                    return true;
            return false;
        }

        static void Check(bool ok, string reason)
        {
            if (!ok) throw new InvalidOperationException(reason);
        }

        static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "Account flow timed out");
                if (!EditorApplication.isPlaying || Time.timeSinceLevelLoad < 2) return;
                if (step == 0)
                {
                    gate = AccountGate.Instance;
                    player = UnityEngine.Object.FindAnyObjectByType<Player>();
                    Check(gate && player && gate.Page == AccountGate.Screen.Start, "Account or start screen missing");
                    Check(BackdropVisible() && !player.enabled, "World visible before authentication");
                    fixture = new Fixture();
                    gate.Configure(fixture);
                    pending = gate.LoginAsync("a@example.test", "password12345");
                    Check(!gate.Ready && !player.enabled, "Entered before receiving authenticated profile");
                    step++;
                    return;
                }
                if (step == 1)
                {
                    var reply = fixture.DelayedRead;
                    fixture.DelayedRead = null;
                    reply.SetResult(fixture.Profile());
                    step++;
                    return;
                }
                if (pending != null && !pending.IsCompleted) return;
                pending?.GetAwaiter().GetResult();

                if (step == 2)
                {
                    Check(gate.Ready && gate.Page == AccountGate.Screen.World && player.enabled,
                        "Successful login must enter directly without nickname confirmation");
                    Check(gate.Session.Profile.nickname == "이전닉네임" && fixture.NicknameWrites == 0,
                        "Returning login must reuse its nickname without a write");
                    if (!player.transform.Find("Student character") || !Camera.main) return;
                    Check(!BackdropVisible(), "Login backdrop covers world");
                    var rename = typeof(AccountGate).GetMethod("ChangeNicknameAsync");
                    Check(rename != null, "MY nickname save must be available after entry");
                    pending = (Task)rename.Invoke(gate, new object[] { "새닉네임" });
                }
                else if (step == 3)
                {
                    Check(gate.Ready && player.enabled && gate.Page == AccountGate.Screen.World &&
                        gate.Session.Profile.nickname == "새닉네임" && gate.Session.Profile.level == 15,
                        "Nickname change should preserve entry and level");
                    Check(fixture.NicknameWrites == 1, "Nickname failed to persist");
                    fixture.RefreshFails = true;
                    typeof(AccountSession).GetField("expires", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(gate.Session, 1L);
                    expiredAt = Time.time;
                }
                else if (step == 4)
                {
                    if (Time.time - expiredAt < .3f) return;
                    Check(!gate.Ready && !player.enabled, "Expired token failed to block world");
                    pending = gate.LogoutAsync();
                }
                else if (step == 5)
                {
                    Check(!gate.Ready && !gate.Session.IsAuthenticated && gate.Page == AccountGate.Screen.Login,
                        "Logout must return to the sign-in screen");
                    Check(BackdropVisible() && Cursor.visible, "Sign-in screen lost its backdrop or pointer");
                    fixture = new Fixture { Nickname = "", DelayedRead = null };
                    gate.Configure(fixture);
                    pending = gate.VerifySignupAsync("new@example.test", "123456");
                }
                else
                {
                    Check(gate.Ready && player.enabled && gate.Page == AccountGate.Screen.World,
                        "Verified new account did not enter automatically");
                    Check(gate.Session.Profile.nickname == "학생" && fixture.NicknameWrites == 1,
                        "New account must receive a saved default nickname");
                    Check(!runtimeError, "Runtime errors during account flow");
                    Debug.Log("ACCOUNT_FLOW_PASS: direct login, saved nickname, rename in world, signup default, expiration and logout");
                    Finish(0);
                    return;
                }
                step++;
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
            Application.logMessageReceived -= Log;
            EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
        }
    }
}
