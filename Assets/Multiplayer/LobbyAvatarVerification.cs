#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
#if CAMPUS_SUNBOX_AVATAR
using Sunbox.Avatars;
#endif

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

                try
                {
                    foreach (var size in new[] { new Vector2(741, 580), new Vector2(1280, 720), new Vector2(1920, 1080) })
                    {
                        var status = LibraryLobby.StatusRect(size.x, size.y, 122, Portal.Space.Library);
                        float scale = Mathf.Min(size.x / 1280, size.y / 720);
                        var title = new Rect((size.x - 1280 * scale) / 2 + 20 * scale,
                            (size.y - 720 * scale) / 2 + 20 * scale, 335 * scale, 77 * scale);
                        Require(status.x <= 16 && status.yMax < size.y / 3 && !status.Overlaps(title),
                            "Lobby population must stay at upper left without overlapping the library title.");
                    }
                    CheckRoster(avatars, player);
                    CheckQuizRoom(avatars, player);
                }
                finally { avatars.Clear(); }
                Debug.Log("PASS lobby/quiz avatar visibility, names, chair pose, room changes and departure");
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
            CheckNameplates(avatars, welcome.self_id, other);
            CheckAppearance(avatars, welcome.self_id, other, avatar, player);
            avatars.Apply(welcome.self_id, new[] { welcome.players[0] });
            Require(!avatar.gameObject.activeSelf, "Departed avatar remained visible.");
        }

        static void CheckQuizRoom(LobbyAvatars avatars, Player player)
        {
            var portal = Array.Find(Object.FindObjectsByType<Portal>(FindObjectsSortMode.None),
                point => point.destination == Portal.Space.Quiz);
            Require(player.TryUsePortal(portal), "The actual quiz portal must admit the local player.");
            var chair = GameObject.Find("Quiz Seat 02").GetComponent<Seat>();
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = chair.transform.position + new Vector3(0, -.6f, -.8f);
            body.enabled = true;
            Physics.SyncTransforms();
            Require(player.TrySit(chair), "Could not sit on the actual quiz chair.");
            var seated = avatars.Sample();
            Require(seated.motion == "idle", "A seated player must send a supported idle pose.");
            player.Stand();

            var quizPeer = new LobbyPeer { id = "quiz", nickname = "퀴즈 친구", level = 4, pose = avatars.Sample() };
            var lobbyPeer = new LobbyPeer { id = "lobby", nickname = "로비 친구", pose = new LobbyMove { x = 0, motion = "idle" } };
            var mafiaPeer = new LobbyPeer { id = "mafia", nickname = "마피아 친구", pose = new LobbyMove { x = 100, motion = "idle" } };
            var peers = new[] { quizPeer, lobbyPeer, mafiaPeer };
            Require(avatars.Apply("self", peers) == 1, "Quiz room must show only the other quiz participant.");
            var remote = Participants()[0];
            var prefabVisual = remote.GetComponent<LibraryAvatarMotion>() ? remote.GetChild(0) : null;
            var leg = remote.Find("Left leg");
            Vector3 standingOffset = prefabVisual ? prefabVisual.localPosition : Vector3.zero;
            quizPeer.pose = seated;
            avatars.Apply("self", peers);
            avatars.Tick();
            Require(prefabVisual ? prefabVisual.localPosition.y < standingOffset.y - .2f :
                leg && Quaternion.Angle(leg.localRotation, Quaternion.identity) > 65f,
                "A participant on the real quiz chair must visibly sit.");
            CheckAppearance(avatars, "self", quizPeer, remote, player);

            var camera = new GameObject("Quiz nameplate test camera").AddComponent<Camera>();
            try
            {
                camera.transform.position = remote.position + new Vector3(0, 1.3f, -4);
                var names = new List<string>();
                avatars.DrawVisibleNameplates(camera, (point, name, level) => names.Add(name));
                Require(names.Count == 1 && names[0] == quizPeer.nickname,
                    "Only the quiz participant's nameplate should be projected.");
            }
            finally { Object.Destroy(camera.gameObject); }

            quizPeer.pose = avatars.Sample();
            avatars.Apply("self", peers);
            avatars.Tick();
            Require(prefabVisual ? prefabVisual.localPosition == standingOffset :
                leg && Quaternion.Angle(leg.localRotation, Quaternion.identity) < .01f,
                "A participant who has left the chair must resume standing.");

            Require(player.RequestRoomExit() && player.ConfirmRoomExit(), "Could not return to the library.");
            Require(avatars.Apply("self", peers) == 1 && !remote.gameObject.activeSelf,
                "Returning to the library must replace quiz participants with lobby participants.");
        }

        static void CheckAppearance(LobbyAvatars avatars, string self, LobbyPeer peer, Transform remote, Player player)
        {
#if CAMPUS_SUNBOX_AVATAR
            var local = player.GetComponentInChildren<AvatarCustomization>(true);
            var target = remote.GetComponentInChildren<AvatarCustomization>(true);
            Require(local && target, "Both visuals must use the installed avatar catalog.");
            var look = avatars.SampleAppearance();
            look.body = 1 - look.body;
            var gender = (AvatarCustomization.AvatarGender)look.body;
            bool male = gender == AvatarCustomization.AvatarGender.Male;
            var wardrobe = target.AvatarReferences.AvailableClothingItems;
            var top = Array.Find(wardrobe, item => item && item.SlotType == SlotType.Top && !item.IsEmpty &&
                (male ? item.MaleMesh : item.FemaleMesh) && item.Variations.Length > 1);
            Require(top, "Fixture avatar catalog needs a top with two colors.");
            look.items[0] = top.Name;
            look.colors[0] = 0;
            // Use the same serializer as the actual snapshot before updating an existing remote.
            peer.appearance = JsonUtility.FromJson<LobbyAppearance>(JsonUtility.ToJson(look));
            avatars.Apply(self, new[] { peer });
            Require(target.CurrentGender == gender && target.ClothingItemTop == top, "Remote body and clothing must change in place.");
            look.colors[0] = 1;
            peer.appearance = JsonUtility.FromJson<LobbyAppearance>(JsonUtility.ToJson(look));
            avatars.Apply(self, new[] { peer });
            var clothes = Array.Find(target.CurrentBase.GetComponentsInChildren<UClothingItem>(),
                item => item.ClothingItem == top && item.IsEquipped);
            Require(clothes && clothes.GetComponent<Renderer>().sharedMaterial == top.Variations[1],
                "The stationary participant's actual clothing material must reflect its selected color.");
            Require(local.CurrentGender != gender, "Remote changes must not change the local avatar.");
#else
            peer.appearance = new LobbyAppearance {
                body = 1, items = new[] { "Blouse", "", "", "", "" }, colors = new int[LobbyAppearance.SlotCount]
            };
            avatars.Apply(self, new[] { peer });
            Require(remote.GetComponentsInChildren<Renderer>().Length > 0, "The fallback avatar must survive a styled peer update.");
#endif
        }

        static void CheckNameplates(LobbyAvatars avatars, string self, LobbyPeer other)
        {
            var second = JsonUtility.FromJson<LobbyPeer>(
                "{\"id\":\"c\",\"nickname\":\"다른 학생\",\"level\":15,\"slot\":2," +
                "\"pose\":{\"x\":1,\"y\":0.1,\"z\":-8,\"motion\":\"idle\"}}");
            var camera = new GameObject("Nameplate test camera").AddComponent<Camera>();
            try
            {
                camera.transform.position = new Vector3(0, 1.8f, -14);
                camera.transform.rotation = Quaternion.identity;
                var observed = new Dictionary<string, int>();
                Action<Vector2, string, int> collect = (point, nickname, level) => observed.Add(nickname, level);
                var peers = new[] { other, second };
                avatars.Apply(self, peers);
                avatars.DrawVisibleNameplates(camera, collect);
                Require(observed.Count == 2 && observed["학생"] == 0 && observed["다른 학생"] == 15,
                    "Two nameplates must keep each account's own nickname and optional level.");

                observed.Clear();
                other.level = 3;
                other.nickname = "바뀐이름";
                avatars.Apply(self, peers);
                avatars.DrawVisibleNameplates(camera, collect);
                Require(observed.Count == 2 && observed["바뀐이름"] == 3 && observed["다른 학생"] == 15,
                    "Profile changes must update the existing nameplate.");
                observed.Clear();
                camera.transform.rotation = Quaternion.Euler(0, 180, 0);
                avatars.DrawVisibleNameplates(camera, collect);
                Require(observed.Count == 0, "Nameplates behind the camera must stay hidden.");
            }
            finally { Object.Destroy(camera.gameObject); }
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
