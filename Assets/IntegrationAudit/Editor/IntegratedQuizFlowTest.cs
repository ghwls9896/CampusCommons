using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using CampusCommons.Quiz;
using CampusCommons.Webcam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;

// Local integration regression: real scene and lifecycle, fixture authentication, no external requests/camera.
public static class IntegratedQuizFlowTest
{
    const string Key = "IntegratedQuizFlowTest";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static int step;
    static double deadline;
    static float at;
    static Task pending;
    static bool runtimeError;
    static AccountGate gate;
    static Player player;
    static QuizRoom quiz;
    static LobbyStudyWebcam study;
    static QuizSession session;
    static Seat seat;
    sealed class Fixture : IAccountTransport
    {
        public Task<T> SendAsync<T>(string method, string path, object body, string token, CancellationToken ct) where T : class
        {
            object value = null;
            if (typeof(T) == typeof(AuthResponse)) value = new AuthResponse { access_token = "fixture", refresh_token = "fixture", expires_at = 4000000000, user = new AuthUser { id = "integration-user", email_confirmed_at = "date" } };
            if (typeof(T) == typeof(AccountProfile)) value = new AccountProfile { user_id = "integration-user", nickname = "통합검사", level = 1 };
            return Task.FromResult((T)value);
        }
    }
    public static void Run()
    {
        SessionState.EraseString("LocalIntegrationAudit");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");
        EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] static void Resume()
    {
        EditorApplication.delayCall += () => {
            if (!SessionState.GetBool(Key, false)) return;
            step = 0; pending = null; runtimeError = false; deadline = EditorApplication.timeSinceStartup + 60;
            Application.logMessageReceived += OnLog; EditorApplication.update += Tick;
        };
    }
    static void OnLog(string m, string stack, LogType type)
    {
        if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Error || type == LogType.Exception) runtimeError = true;
    }
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static void Next() { step++; at = Time.time; }
    static void Move(Vector3 position)
    {
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false; player.transform.position = position; controller.enabled = true;
        Physics.SyncTransforms();
    }
    static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "Combined quiz flow timed out");
            if (!EditorApplication.isPlaying || Time.timeSinceLevelLoad < 2) return;
            study = UnityEngine.Object.FindAnyObjectByType<LobbyStudyWebcam>();
            if (study) study.SendMessage("OnApplicationFocus", false);
            if (pending != null && !pending.IsCompleted) return;
            if (pending != null && pending.IsFaulted) throw pending.Exception;
            if (step > 0 && Time.time - at < .3f) return;
            if (step == 0)
            {
                gate = AccountGate.Instance; player = UnityEngine.Object.FindAnyObjectByType<Player>();
                quiz = player.GetComponent<QuizRoom>();
                Check(gate && quiz && study, "Account/quiz/webcam bootstraps missing");
                Check(!gate.Ready && !player.enabled && !quiz.enabled, "Pre-auth input not blocked");
                Check((bool)typeof(QuizRoom).GetField("configured", Private).GetValue(quiz), "Local quiz deployment configuration missing");
                typeof(QuizRoom).GetField("server", Private).SetValue(quiz, "");
                session = (QuizSession)typeof(QuizRoom).GetField("session", Private).GetValue(quiz);
                gate.Configure(new Fixture()); pending = gate.LoginAsync("integration@example.test", "fixture-password"); Next();
            }
            else if (step == 1) { pending = gate.EnterAsync("통합검사"); Next(); }
            else if (step == 2)
            {
                Check(gate.Ready && player.enabled && quiz.enabled, "Login did not release quiz/player");
                var door = UnityEngine.Object.FindObjectsByType<LobbyDoor>().First(d => d.room && d.room.destination == Portal.Space.Quiz);
                Move(door.transform.position + Vector3.left * .7f);
                Check(door.TryEnter(player), "Actual quiz lobby door rejected entry"); Next();
            }
            else if (step == 3)
            {
                if (Time.time - at < .8f) return;
                Check(player.currentSpace == Portal.Space.Quiz && quiz.BlocksMovement, "Door did not enter quiz");
                Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, "Quiz input cursor captured");
                var fixtureMethod = typeof(CampusCommons.Quiz.Editor.QuizRoomTest).GetMethod("Fixture", BindingFlags.NonPublic | BindingFlags.Static);
                session.Begin((QuizSet)fixtureMethod.Invoke(null, null), Time.realtimeSinceStartupAsDouble);
                Check(session.SubmitChoice(0, Time.realtimeSinceStartupAsDouble), "Fixture quiz answer rejected");
                // Unsent request: revoking authentication must dispose it without networking.
                typeof(QuizRoom).GetField("request", Private).SetValue(quiz, UnityWebRequest.Get("https://example.invalid"));
                typeof(QuizRoom).GetField("working", Private).SetValue(quiz, true);
                pending = gate.LogoutAsync(); Next();
            }
            else if (step == 4)
            {
                Check(!player.enabled && !quiz.enabled && !gate.Ready, "Logout retained quiz input");
                Check(session.Data == null && typeof(QuizRoom).GetField("request", Private).GetValue(quiz) == null, "Logout retained answers or pending request");
                Check(!player.GetComponent<CharacterController>().enabled, "Logout re-enabled controller");
                pending = gate.LoginAsync("integration@example.test", "fixture-password"); Next();
            }
            else if (step == 5) { pending = gate.EnterAsync("통합검사"); Next(); }
            else if (step == 6)
            {
                Check(quiz.enabled && gate.Ready && session.Data == null, "Relogin did not reset quiz");
                Check(player.RequestRoomExit() && player.ConfirmRoomExit(), "Cannot return to lobby after relogin"); Next();
            }
            else if (step == 7)
            {
                Check(player.currentSpace == Portal.Space.Library && !quiz.BlocksMovement, "Quiz still owns lobby input");
                seat = UnityEngine.Object.FindObjectsByType<Seat>().First(s => !s.occupant && Mathf.Abs(s.transform.position.x) < 30);
                Move(seat.transform.position - Vector3.up * .6f);
                Check(player.TrySit(seat), "Cannot sit after leaving quiz"); Next();
            }
            else if (step == 8)
            {
                Check(study.IsAvailable && !study.IsRunning, "Webcam not available/manual-start guard lost");
                study.EndStudy(); Check(!seat.occupant && !study.IsAvailable && !study.IsRunning, "End did not release seat"); Next();
            }
            else
            {
                Check(!runtimeError, "Runtime errors during combined flow");
                Debug.Log("INTEGRATED_QUIZ_ACCOUNT_WEBCAM_PASS: pre-auth block, real door, logout cancels quiz, relogin, lobby, seat, end/stand; fixtures; no live camera/network");
                Finish(0);
            }
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    static void Finish(int code)
    {
        SessionState.SetBool(Key, false); Application.logMessageReceived -= OnLog; EditorApplication.update -= Tick; EditorApplication.Exit(code);
    }
}
