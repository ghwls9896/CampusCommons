using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusCommons.Mafia.Editor
{
    public static class MafiaRoomSmoke
    {
        const string Key = "CampusMafiaRoomSmoke";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static int step;
        static double deadline;
        static Task pending;
        static Player player;
        static MafiaPanel panel;

        sealed class Fixture : IAccountTransport
        {
            public Task<T> SendAsync<T>(string method, string path, object body, string token, CancellationToken ct) where T : class
            {
                if (typeof(T) == typeof(AuthResponse))
                    return Task.FromResult(new AuthResponse
                    {
                        access_token = "test", refresh_token = "test", expires_at = 4000000000,
                        user = new AuthUser { id = "mafia-smoke", email_confirmed_at = "fixture" }
                    } as T);
                if (typeof(T) == typeof(AccountProfile))
                    return Task.FromResult(new AccountProfile { user_id = "mafia-smoke", nickname = "점검", level = 1 } as T);
                return Task.FromResult<T>(null);
            }
        }

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
            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "Mafia smoke test timed out");
                if (!EditorApplication.isPlaying || Time.timeSinceLevelLoad < 2 || pending?.IsCompleted == false) return;
                if (pending?.IsFaulted == true) throw pending.Exception;
                var gate = AccountGate.Instance;
                if (step == 0)
                {
                    player = UnityEngine.Object.FindAnyObjectByType<Player>();
                    panel = UnityEngine.Object.FindAnyObjectByType<MafiaPanel>();
                    Check(gate && player && panel && !gate.Ready && !player.enabled, "Unexpected pre-login state");
                    gate.Configure(new Fixture());
                    pending = gate.LoginAsync("mafia@example.test", "fixture-password");
                }
                else if (step == 1) pending = gate.EnterAsync("점검");
                else if (step == 2)
                {
                    Check(gate.Ready && player.enabled, "Account did not restore player input");
                    var door = UnityEngine.Object.FindObjectsByType<LobbyDoor>().First(d => d.room && d.room.destination == Portal.Space.Mafia);
                    var controller = player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    player.transform.position = door.transform.position + Vector3.left * .7f;
                    controller.enabled = true;
                    Physics.SyncTransforms();
                    Check(door.TryEnter(player), "Actual lobby door did not enter mafia room");
                }
                else if (step == 3)
                {
                    if (!(bool)typeof(MafiaPanel).GetField("panelOpen", Private).GetValue(panel)) return;
                    Check(!player.enabled && Cursor.visible, "Panel did not own its input");
                    typeof(MafiaPanel).GetMethod("ClosePanel", Private).Invoke(panel, null);
                    Check(player.enabled, "Closing panel failed to release input");
                    Check(player.RequestRoomExit() && player.ConfirmRoomExit(), "Could not return to the lounge");
                }
                else
                {
                    if ((bool)typeof(MafiaPanel).GetField("inRoom", Private).GetValue(panel)) return;
                    Check(player.currentSpace == Portal.Space.Library && player.enabled, "Lounge input was blocked");
                    Debug.Log("MAFIA_ROOM_SCENE_PASS: fixture login, 3D door, panel input, lounge exit");
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

        static void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }

        static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
        }
    }
}
