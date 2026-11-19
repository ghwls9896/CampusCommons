using System;
using System.IO;
using System.Threading.Tasks;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.Multiplayer
{
    [DefaultExecutionOrder(300)]
    public sealed class LibraryLobby : MonoBehaviour
    {
        const string LibraryScene = "Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity";
        readonly LobbyAvatars avatars = new LobbyAvatars();
        LobbyChannel connection;
        AccountGate gate;
        Player player;
        string user, nickname, selfId, status;
        LobbyPeer[] roster;
        Portal.Space visibleSpace;
        bool visiting, blocked, firstEntrance, enteringWorld;
        float retryAt, nextSample, retryDelay;
        long revision;
        int profileLevel;
        GUIStyle statusStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var service = new GameObject("Public library lobby");
            DontDestroyOnLoad(service);
            service.AddComponent<LibraryLobby>();
        }

        void LateUpdate()
        {
            if (gate != AccountGate.Instance)
            {
                if (gate) gate.EntryRevoked -= ForgetAccount;
                ForgetAccount();
                gate = AccountGate.Instance;
                if (gate) gate.EntryRevoked += ForgetAccount;
            }
            if (!gate || !gate.LobbyEnabled || !gate.Ready || !gate.Session.IsAuthenticated)
            {
                if (user != null) ForgetAccount();
                return;
            }

            var profile = gate.Session.Profile;
            if (profile == null || string.IsNullOrWhiteSpace(profile.nickname)) return;
            if (user != profile.user_id)
            {
                ForgetAccount();
                user = profile.user_id;
                nickname = profile.nickname;
                profileLevel = profile.level;
            }
            if (!player && SceneManager.GetActiveScene().path == LibraryScene)
                player = FindAnyObjectByType<Player>();
            if (enteringWorld && player && player.isActiveAndEnabled && avatars.Bind(player))
            {
                enteringWorld = false;
                // Token expiry can interrupt a private room. A fresh login starts in the library.
                if (player.currentSpace == Portal.Space.Quiz || player.currentSpace == Portal.Space.Mafia)
                {
                    if (player.IsExitConfirmOpen || player.RequestRoomExit()) player.ConfirmRoomExit();
                }
            }
            if (!InSharedSpace())
            {
                if (visiting) LeaveVisit();
                return;
            }
            if (!avatars.Bind(player)) return;
            if (nickname != profile.nickname || profileLevel != profile.level)
            {
                LeaveVisit(); // The server reads both public profile fields on admission.
                nickname = profile.nickname;
                profileLevel = profile.level;
            }

            if (roster != null && visibleSpace != player.currentSpace)
                ShowRoom();
            visiting = true;
            if (connection == null && !blocked && Time.unscaledTime >= retryAt)
                _ = JoinAsync();
            if (selfId != null && Time.unscaledTime >= nextSample)
            {
                nextSample = Time.unscaledTime + .101f;
                connection.Publish(avatars.Sample());
            }
            avatars.Tick();
        }

        bool InSharedSpace()
        {
            return player && player.isActiveAndEnabled &&
                (player.currentSpace == Portal.Space.Library || player.currentSpace == Portal.Space.Quiz) &&
                player.gameObject.scene.path == LibraryScene && SceneManager.GetActiveScene().path == LibraryScene;
        }

        async Task JoinAsync()
        {
            var joining = new LobbyChannel();
            connection = joining;
            status = "공용 로비 연결 중…";
            int code = await joining.RunAsync(gate.Session, user, frame =>
            {
                if (connection == joining && gate.Ready && gate.Session.UserId == user && InSharedSpace())
                    Apply(frame);
            });
            if (connection != joining) return;
            connection = null;
            ClearRoster();
            switch (code)
            {
                case 4400:
                    blocked = true;
                    status = "이 계정은 다른 창에서 로비에 접속 중입니다";
                    return;
                case 4401:
                case 4403:
                    blocked = true;
                    status = "로비 입장을 위해 다시 로그인해 주세요";
                    return;
                case 1003:
                case 1008:
                case 1009:
                    blocked = true;
                    status = "로비 연결 규격을 확인해 주세요";
                    return;
                case 4409:
                    retryDelay = 10;
                    status = "공용 로비 만원 (8/8) · 빈자리 대기 중";
                    break;
                default:
                    retryDelay = Mathf.Clamp(retryDelay * 2, 2, 30);
                    status = "로비 연결이 끊겼습니다 · 재접속 대기 중";
                    break;
            }
            retryAt = Time.unscaledTime + retryDelay;
        }

        void Apply(LobbyFrame frame)
        {
            if (frame == null || frame.players == null || frame.players.Length < 1 || frame.players.Length > 8)
                throw new InvalidDataException();
            bool welcome = frame.type == "welcome";
            if ((selfId == null) != welcome || (!welcome && frame.type != "snapshot"))
                throw new InvalidDataException();
            if (!welcome && frame.revision <= revision) return;
            string id = welcome ? frame.self_id : selfId;
            if (string.IsNullOrEmpty(id)) throw new InvalidDataException();
            var own = Array.Find(frame.players, peer => peer != null && peer.id == id);
            if (own == null || own.slot < 0 || own.slot >= 8) throw new InvalidDataException();

            revision = frame.revision;
            selfId = id;
            if (welcome)
            {
                if (firstEntrance && player.currentSpace == Portal.Space.Library) avatars.PlaceAtEntry(own.slot);
                firstEntrance = false;
                retryDelay = 0;
                nextSample = 0;
            }
            roster = frame.players;
            ShowRoom();
        }

        void ShowRoom()
        {
            if (visibleSpace != player.currentSpace) avatars.Clear();
            visibleSpace = player.currentSpace;
            int present = 1 + avatars.Apply(selfId, roster);
            status = visibleSpace == Portal.Space.Quiz ? $"퀴즈룸  {present}명" : $"공용 로비  {present} / 8";
        }

        void ClearRoster()
        {
            roster = null;
            selfId = null;
            revision = -1;
            avatars.Clear();
        }

        void LeaveVisit()
        {
            var old = connection;
            connection = null;
            old?.Stop();
            ClearRoster();
            visiting = false;
            retryAt = retryDelay = 0;
        }

        void ForgetAccount()
        {
            LeaveVisit();
            player = null;
            user = nickname = status = null;
            blocked = false;
            firstEntrance = enteringWorld = true;
        }

        void OnGUI()
        {
            if (!visiting || !gate || !gate.Ready || !InSharedSpace() || AccountGate.LobbyOverlayOwnsInput ||
                Event.current.type != EventType.Repaint) return;
            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(GUI.skin.box) {
                    font = Resources.Load<Font>("KIT/NotoSansKR"), fontSize = 14,
                    alignment = TextAnchor.MiddleCenter, richText = false
                };
                statusStyle.normal.textColor = Color.white;
            }
            var content = new GUIContent(status);
            float width = statusStyle.CalcSize(content).x + 16;
            // Leave the MY button and the library title panel accessible.
            var rect = new Rect((Screen.width - width) / 2, 12, width, 30);
            statusStyle.Draw(rect, content, false, false, false, false);
        }

        internal void DrawVisibleNameplates(Action<Vector2, string, int> draw)
        {
            if (visiting && selfId != null && InSharedSpace())
                avatars.DrawVisibleNameplates(Camera.main, draw);
        }

        void OnDestroy()
        {
            if (gate) gate.EntryRevoked -= ForgetAccount;
            ForgetAccount();
        }

        void OnApplicationQuit() => ForgetAccount();
    }
}
