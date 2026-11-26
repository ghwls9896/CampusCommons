#if CAMPUS_SUNBOX_AVATAR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.GrandLibrary;
using CampusCommons.Webcam;
using Sunbox.Avatars;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusCommons.Account.Editor
{
    public static class AvatarAccountFlowTest
    {
        const string Key = "CampusCommons.AvatarAccountFlow";
        const string First = "00000000-0000-4000-8000-000000000061";
        const string Second = "00000000-0000-4000-8000-000000000062";
        static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [Serializable]
        sealed class AvatarUpdate
        {
            public AvatarUserMetadata data;
        }

        sealed class Fixture : IAccountTransport
        {
            public readonly Dictionary<string, AvatarUserMetadata> saved = new Dictionary<string, AvatarUserMetadata>();
            public int writes;
            public bool offline;

            AuthUser User(string id)
            {
                saved.TryGetValue(id, out var metadata);
                return new AuthUser { id = id, email_confirmed_at = "date", user_metadata = metadata };
            }

            public Task<T> SendAsync<T>(string method, string path, object body, string token, CancellationToken ct)
                where T : class
            {
                ct.ThrowIfCancellationRequested();
                object reply = null;
                if (body is Credentials login)
                {
                    var id = login.email.StartsWith("first", StringComparison.Ordinal) ? First : Second;
                    reply = new AuthResponse { user = User(id), access_token = id, refresh_token = id, expires_at = 4000000000 };
                }
                else if (typeof(T) == typeof(AccountProfile))
                    reply = new AccountProfile { user_id = token, nickname = "외형 검사", level = 2 };
                else if (typeof(T) == typeof(StudyReply))
                    reply = new StudyReply { user_id = token, day_ms = StudyTime.DayStart(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) };
                else if (method == "PUT" && path == "/auth/v1/user")
                {
                    if (offline) throw new AccountException(503, "connection_failed");
                    var update = JsonUtility.FromJson<AvatarUpdate>(JsonUtility.ToJson(body));
                    if (update.data?.campus_avatar_v1 == null) throw new Exception("Avatar metadata was not serialized");
                    saved[token] = update.data;
                    writes++;
                    reply = User(token);
                }
                return Task.FromResult((T)reply);
            }
        }

        static Fixture fixture;
        static Task pending;
        static int step, nextFrame, defaultHair, selectedHair, offlineHair;
        static AvatarCustomization.AvatarGender defaultBody;
        static string selectedShirt;
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
            deadline = EditorApplication.timeSinceStartup + 55;
            EditorApplication.update += Tick;
        }

        static void Check(bool condition, string failure)
        {
            if (!condition) throw new Exception(failure);
        }

        static string PendingKey(string id) => "CampusCommons.Avatar.Pending.v1." + id;
        static void Change(LibraryAvatarCustomizerUI ui, string method, params object[] args) =>
            typeof(LibraryAvatarCustomizerUI).GetMethod(method, PrivateInstance).Invoke(ui, args);

        static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "Avatar account flow timed out at " + step);
                if (!EditorApplication.isPlaying || Time.frameCount < nextFrame) return;
                if (pending != null)
                {
                    if (!pending.IsCompleted) return;
                    pending.GetAwaiter().GetResult();
                    pending = null;
                    nextFrame = Time.frameCount + 3;
                    return;
                }
                var gate = AccountGate.Instance;
                if (!gate) return;
                if (step == 0)
                {
                    DeletePending();
                    fixture = new Fixture();
                    gate.Configure(fixture);
                    pending = gate.LoginAsync("first@example.test", "fixture");
                    step++;
                    return;
                }

                var ui = UnityEngine.Object.FindAnyObjectByType<LibraryAvatarCustomizerUI>();
                var avatar = ui ? ui.GetComponentInChildren<AvatarCustomization>(true) : null;
                if (!avatar) return;
                switch (step++)
                {
                    case 1:
                        defaultBody = avatar.CurrentGender;
                        defaultHair = avatar.HairStyleIndex;
                        Change(ui, "ChangeGender", defaultBody == AvatarCustomization.AvatarGender.Male
                            ? AvatarCustomization.AvatarGender.Female : AvatarCustomization.AvatarGender.Male);
                        Change(ui, "ChangeHair", 1);
                        Change(ui, "ChangeClothing", SlotType.Top, 1);
                        selectedHair = avatar.HairStyleIndex;
                        selectedShirt = avatar.ClothingItemTop.Name;
                        Check(selectedHair != defaultHair, "Sunbox hair fixture did not change");
                        pending = gate.LogoutAsync();
                        break;
                    case 2:
                        Check(fixture.writes == 1 && fixture.saved[First].campus_avatar_v1.hair == selectedHair,
                            "Logging out before the debounce finished did not save the final look exactly once");
                        PlayerPrefs.DeleteKey(PendingKey(First));
                        pending = gate.LoginAsync("second@example.test", "fixture");
                        break;
                    case 3:
                        Check(avatar.HairStyleIndex == defaultHair && avatar.CurrentGender == defaultBody,
                            "A different account inherited the first account's look");
                        fixture.offline = true;
                        Change(ui, "ChangeHair", 1);
                        offlineHair = avatar.HairStyleIndex;
                        pending = gate.LogoutAsync();
                        break;
                    case 4:
                        Check(PlayerPrefs.HasKey(PendingKey(Second)) && !fixture.saved.ContainsKey(Second),
                            "An interrupted save was lost instead of being kept for the same account");
                        fixture.offline = false;
                        pending = gate.LoginAsync("first@example.test", "fixture");
                        break;
                    case 5:
                        Check(avatar.CurrentGender != defaultBody && avatar.HairStyleIndex == selectedHair &&
                            avatar.ClothingItemTop.Name == selectedShirt, "Login did not restore the saved Sunbox appearance");
                        pending = gate.LogoutAsync();
                        break;
                    case 6:
                        pending = gate.LoginAsync("second@example.test", "fixture");
                        break;
                    case 7:
                        Check(avatar.CurrentGender == defaultBody && avatar.HairStyleIndex == offlineHair,
                            "The unconfirmed appearance did not restore for the matching account");
                        pending = gate.LogoutAsync();
                        break;
                    case 8:
                        Check(fixture.writes == 2 && fixture.saved[Second].campus_avatar_v1.hair == offlineHair,
                            "The unconfirmed appearance did not retry successfully");
                        Debug.Log("AVATAR_ACCOUNT_FLOW_PASS");
                        Finish(0);
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(1);
            }
        }

        static void DeletePending()
        {
            PlayerPrefs.DeleteKey(PendingKey(First));
            PlayerPrefs.DeleteKey(PendingKey(Second));
        }

        static void Finish(int code)
        {
            DeletePending();
            SessionState.EraseBool(Key);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
        }
    }
}
#endif
