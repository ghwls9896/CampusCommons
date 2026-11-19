#if UNITY_EDITOR
using System;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CampusCommons.Multiplayer
{
    public static class LobbyAvatarVerification
    {
        const string Key = "CampusCommons.Multiplayer.VerifyAvatars";
        static double deadline;

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
            EditorApplication.update += Verify;
        }

        static void Verify()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Timed out waiting for the local library avatar.");
                if (!EditorApplication.isPlaying || !AccountGate.Instance) return;
                // Exercise graphics without opening a lobby connection or logging in.
                var world = AccountGate.Instance.World;
                if (world.enabled)
                {
                    world.enabled = false;
                    world.Release();
                    return;
                }
                var player = Object.FindAnyObjectByType<Player>();
                var avatars = new LobbyAvatars();
                if (!avatars.Bind(player)) return;

                try { CheckRoster(avatars, player); }
                finally { avatars.Clear(); }
                Debug.Log("PASS lobby avatar placement, identity, initial poses, controls and departure");
                Finish(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Finish(1);
            }
        }

        static void CheckRoster(LobbyAvatars avatars, Player player)
        {
            var before = player.transform.position;
            avatars.PlaceAtEntry(1);
            var placed = player.transform.position;
            Require(placed.x > before.x + .5f && placed.z > before.z + .5f, "Slot did not separate the local student at the entrance.");
            avatars.PlaceAtEntry(7);
            Require(Vector3.Distance(placed, player.transform.position) < .001f, "Reconnection changed the local position.");
            Require(avatars.Sample().motion == "idle", "First pose should be idle.");

            var welcome = JsonUtility.FromJson<LobbyFrame>(
                "{\"type\":\"welcome\",\"revision\":1,\"self_id\":\"a\",\"players\":[" +
                "{\"id\":\"a\",\"nickname\":\"학생\",\"slot\":1,\"pose\":{\"motion\":\"idle\"}}," +
                "{\"id\":\"b\",\"nickname\":\"학생\",\"slot\":0,\"pose\":null}]}");
            avatars.Apply(welcome.self_id, welcome.players);
            Require(Participants().Length == 0, "Welcome created a duplicate self or a remote at the origin.");

            var other = welcome.players[1];
            other.pose = new LobbyMove { x = -1, y = .1f, z = -8, yaw = 230, motion = "idle" };
            avatars.Apply(welcome.self_id, welcome.players);
            var remotes = Participants();
            Require(remotes.Length == 1, "A distinct player using the same nickname must be visible once.");
            var avatar = remotes[0];
            Require(Vector3.Distance(avatar.position, new Vector3(-1, .1f, -8)) < .001f, "Incorrect first position.");
            Require(avatar.GetComponentsInChildren<Renderer>(true).Length > 0, "Remote mesh is missing.");
            Require(!avatar.GetComponentInChildren<Player>(true) && !avatar.GetComponentInChildren<Camera>(true) &&
                !avatar.GetComponentInChildren<Collider>(true) && !avatar.GetComponentInChildren<AudioListener>(true),
                "Remote avatar duplicated local gameplay, camera, physics, or audio.");
            avatars.Apply(welcome.self_id, new[] { welcome.players[0] });
            Require(!avatar.gameObject.activeSelf, "Departed avatar remained visible.");
        }

        static Transform[] Participants() => Array.FindAll(
            Object.FindObjectsByType<Transform>(FindObjectsSortMode.None),
            transform => transform.name == "Lobby participant");

        static void Require(bool passed, string message)
        {
            if (!passed) throw new InvalidOperationException(message);
        }

        static void Finish(int exitCode)
        {
            EditorApplication.update -= Verify;
            SessionState.EraseBool(Key);
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            else EditorApplication.ExitPlaymode();
        }
    }
}
#endif
