using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using CampusCommons.Webcam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LobbyAccountMenuVerification
{
    const string Key = "LobbyAccountMenuVerification";
    const string FixtureUser = "00000000-0000-4000-8000-000000000034";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static AccountGate gate;
    static Fixture transport;
    static Task login;
    static double deadline;

    sealed class Fixture : IAccountTransport
    {
        public int LogoutCount;

        public Task<T> SendAsync<T>(string method, string path, object body, string bearer, CancellationToken cancellation)
            where T : class
        {
            object result = null;
            if (typeof(T) == typeof(AuthResponse))
                result = new AuthResponse
                {
                    user = new AuthUser { id = FixtureUser, email_confirmed_at = "fixture" },
                    access_token = "fixture", refresh_token = "fixture", expires_in = 3600
                };
            else if (typeof(T) == typeof(AccountProfile))
                result = new AccountProfile { user_id = FixtureUser, nickname = "로비메뉴테스트", level = 15 };
            else if (typeof(T) == typeof(StudyReply))
                result = new StudyReply
                {
                    user_id = FixtureUser,
                    day_ms = StudyTime.DayStart(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
                    today_ms = 65000
                };
            else if (path.StartsWith("/auth/v1/logout", StringComparison.Ordinal) && bearer == "fixture")
                LogoutCount++;
            return Task.FromResult((T)result);
        }
    }

    public static void Run()
    {
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        deadline = EditorApplication.timeSinceStartup + 45;
        EditorApplication.update += Tick;
    }

    static async Task Enter()
    {
        await gate.LoginAsync("lobby@example.test", "fixture-password");
    }

    static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "Lobby menu test timed out");
            if (!EditorApplication.isPlaying) return;
            if (login == null)
            {
                gate = AccountGate.Instance;
                if (!gate) return;
                transport = new Fixture();
                gate.Configure(transport);
                login = Enter();
                return;
            }
            if (!login.IsCompleted) return;
            login.GetAwaiter().GetResult();
            var player = UnityEngine.Object.FindAnyObjectByType<Player>();
            if (!player || !player.TryGetComponent(out LibraryInventoryUI inventory) || !Camera.main) return;

            var view = gate.GetComponent<AccountView>();
            var escape = typeof(AccountView).GetMethod("HandleLobbyEscape", Private);
            var logout = typeof(AccountView).GetMethod("LogoutFromLobbyMenu", Private);
            var my = typeof(AccountView).GetMethod("ToggleMy", Private);
            var menu = typeof(AccountGate).GetProperty("LobbyOverlayOwnsInput");
            Check(escape != null && logout != null && menu != null && my != null, "Lobby menu or MY is missing");
            bool PressEscape() => (bool)escape.Invoke(view, null);
            bool OwnsInput() => (bool)menu.GetValue(null);
            bool ToggleMy() => (bool)my.Invoke(view, null);

            inventory.SetOpen(true);
            Check(inventory.OwnsInput, "Inventory did not acquire lobby input");
            Check(!PressEscape() && !ToggleMy() && !OwnsInput(), "The open inventory must keep its input");
            inventory.SetOpen(false);

            var records = StudyRecordService.Instance;
            typeof(StudyRecordService).GetMethod("ToggleHistory",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(records, null);
            Check(PressEscape() && !OwnsInput(), "Esc must close history before opening the lobby menu");

#if CAMPUS_SUNBOX_AVATAR
            var customizer = player.GetComponent<LibraryAvatarCustomizerUI>();
            Check(customizer, "Sunbox customizer missing");
            customizer.Toggle();
            Check(customizer.IsOpen && !PressEscape() && !OwnsInput(), "Esc should be left to the customizer");
            customizer.Close();
#endif

            foreach (var room in new[] { Portal.Space.Quiz, Portal.Space.Mafia, Portal.Space.Escape })
            {
                player.currentSpace = room;
                Check(!PressEscape() && !OwnsInput(), "Lobby menu opened outside the library");
            }
            player.currentSpace = Portal.Space.Library;
            player.SetFirstPerson(false);
            Check(player.TryGetNameplateScreenPoint(out Vector2 nameplate), "Third-person nameplate must follow the avatar");
            Vector3 chest = Camera.main.WorldToScreenPoint(player.transform.position + Vector3.up * 1.35f);
            Check(nameplate.y < Screen.height - chest.y, "Nameplate should appear above the avatar");
            player.SetFirstPerson(true);
            Check(!player.TryGetNameplateScreenPoint(out _), "First-person view should hide its own nameplate");

            Check(ToggleMy() && OwnsInput() && Cursor.visible && records.TodayMilliseconds == 65000,
                "MY did not open with the account's study summary");
            Check(!player.CanUseInventory, "MY must block other lobby inputs while editing");
            Check(PressEscape() && !OwnsInput(), "Esc must close MY before opening the logout menu");

            Check(PressEscape() && OwnsInput() && gate.Ready, "Esc must open a menu before logout");
            Check(!player.CanUseInventory && Cursor.visible && Cursor.lockState == CursorLockMode.None,
                "The menu must own lobby input and make its pointer usable");
            Check(PressEscape() && !OwnsInput() && player.CanUseInventory && player.IsFirstPerson,
                "Esc must close the menu and restore lobby input");
            Check(PressEscape() && OwnsInput(), "Esc must reopen the menu");
            logout.Invoke(view, null);
            Check(!gate.Ready && !gate.Session.IsAuthenticated && gate.Page == AccountGate.Screen.Login,
                "Logout must clear the account and show the sign-in form");
            Check(!player.enabled && !OwnsInput() && transport.LogoutCount == 1,
                "Logout did not release the menu and block world input");
            Debug.Log("LOBBY_ACCOUNT_MENU_PASS: nameplate, MY records, overlay input priority, logout to login");
            Finish(0);
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
