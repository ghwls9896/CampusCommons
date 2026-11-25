#if CAMPUS_SUNBOX_AVATAR

using System;
using UnityEngine;
using CampusCommons.Account;
using Sunbox.Avatars;

namespace CampusCommons.GrandLibrary
{
    // First-lobby-entry customization. No shared Player.cs or account API changes.
    [DefaultExecutionOrder(-100)]
    public class LibraryAvatarCustomizerUI : MonoBehaviour
    {
        private const int SaveVersion = 1;
        private const string SavePrefix = "CampusCommons.AvatarInitialLook.v1.";

        [Serializable]
        private sealed class AvatarLook
        {
            public int version = SaveVersion;
            public int gender;
            public int skin;
            public int hairStyle;
            public int hairColor;
        }

        private AvatarCustomization avatar;
        private string activeUserId;
        private bool setupRequired;
        private bool initializedForUser;
        private Vector2 scroll;

        public bool IsOpen { get; private set; }

        public void Initialize(GameObject avatarVisual)
        {
            avatar = avatarVisual != null
                ? avatarVisual.GetComponentInChildren<AvatarCustomization>(true)
                : null;

            if (avatar == null)
                Debug.LogWarning("[CampusCommons] Initial avatar setup: Sunbox avatar not found.");
        }

        // Player.cs still calls Toggle() for C; deliberately disable that shortcut.
        // The setup can only open automatically after authenticated lobby entry.
        public void Toggle() { }

        // ESC must not silently finish an incomplete initial setup.
        public void Close()
        {
            if (!setupRequired)
                IsOpen = false;
        }

        private static string SaveKey(string userId)
        {
            return SavePrefix + userId;
        }

        private void ResetForLogout()
        {
            activeUserId = null;
            initializedForUser = false;
            setupRequired = false;
            IsOpen = false;
        }

        private void Update()
        {
            AccountGate gate = AccountGate.Instance;
            if (gate == null || !gate.Ready || gate.Session == null ||
                !gate.Session.IsAuthenticated ||
                gate.Session.Profile == null ||
                string.IsNullOrEmpty(gate.Session.UserId) ||
                gate.Session.Profile.user_id != gate.Session.UserId)
            {
                if (initializedForUser || IsOpen)
                    ResetForLogout();
                return;
            }

            if (avatar == null || avatar.AvatarReferences == null)
                return;

            string userId = gate.Session.UserId;
            if (initializedForUser && activeUserId == userId)
                return;

            activeUserId = userId;
            initializedForUser = true;

            string key = SaveKey(userId);
            AvatarLook saved = null;
            if (PlayerPrefs.HasKey(key))
            {
                try
                {
                    saved = JsonUtility.FromJson<AvatarLook>(PlayerPrefs.GetString(key));
                    if (saved == null || saved.version != SaveVersion)
                        saved = null;
                }
                catch (Exception)
                {
                    saved = null;
                }
            }

            if (saved != null)
            {
                ApplySavedLook(saved);
                setupRequired = false;
                IsOpen = false;
            }
            else
            {
                // The baseline clothing is assigned before the user chooses a look.
                ApplyBasicClothes(avatar.CurrentGender);
                setupRequired = true;
                IsOpen = true;
            }
        }

        private bool IsCompatible(ClothingItem item,
            AvatarCustomization.AvatarGender gender)
        {
            if (item == null || item.IsEmpty)
                return false;

            return gender == AvatarCustomization.AvatarGender.Male
                ? item.MaleMesh != null
                : item.FemaleMesh != null;
        }

        private ClothingItem GetBasicClothing(SlotType slot,
            AvatarCustomization.AvatarGender gender)
        {
            var references = avatar.AvatarReferences;
            var available = references != null ? references.AvailableClothingItems : null;
            if (available == null)
                return null;

            ClothingItem fallback = null;
            foreach (var item in available)
            {
                if (item == null || item.SlotType != slot ||
                    !IsCompatible(item, gender))
                    continue;

                // Prefer one clothing definition compatible with both genders.
                if (IsCompatible(item, AvatarCustomization.AvatarGender.Male) &&
                    IsCompatible(item, AvatarCustomization.AvatarGender.Female))
                    return item;

                if (fallback == null)
                    fallback = item;
            }

            return fallback;
        }

        private void ApplyBasicClothes(AvatarCustomization.AvatarGender gender)
        {
            if (avatar == null || avatar.AvatarReferences == null)
                return;

            ClothingItem top = GetBasicClothing(SlotType.Top, gender);
            ClothingItem bottom = GetBasicClothing(SlotType.Bottom, gender);
            ClothingItem shoes = GetBasicClothing(SlotType.Shoes, gender);

            // Match the existing Sunbox change-gender order: clothing before SetGender.
            if (top != null)
                avatar.ClothingItemTop = top;
            if (bottom != null)
                avatar.ClothingItemBottom = bottom;
            if (shoes != null)
                avatar.ClothingItemShoes = shoes;

            // Hats/glasses are future shop items, not starter customization choices.
            avatar.ClothingItemHat = null;
            avatar.ClothingItemGlasses = null;

            if (avatar.CurrentGender != gender)
                avatar.SetGender(gender);

            avatar.UpdateCustomization();
        }

        private static int Wrap(int value, int count)
        {
            return count > 0 ? (value % count + count) % count : 0;
        }

        private int GetSkinCount()
        {
            var refs = avatar.AvatarReferences;
            if (refs == null)
                return 0;

            if (avatar.CurrentGender == AvatarCustomization.AvatarGender.Male)
                return refs.MaleSkinMaterials != null ? refs.MaleSkinMaterials.Length : 0;

            return refs.FemaleSkinMaterials != null ? refs.FemaleSkinMaterials.Length : 0;
        }

        private int GetHairStyleCount()
        {
            var refs = avatar.AvatarReferences;
            return refs != null && refs.HairItems != null ? refs.HairItems.Length : 0;
        }

        private int GetHairColorCount(int hairIndex)
        {
            var hairs = avatar.AvatarReferences != null
                ? avatar.AvatarReferences.HairItems : null;
            if (hairs == null || hairIndex < 0 || hairIndex >= hairs.Length ||
                hairs[hairIndex] == null || hairs[hairIndex].Variations == null)
                return 0;

            return hairs[hairIndex].Variations.Length;
        }

        private void ApplySavedLook(AvatarLook saved)
        {
            var gender = saved.gender == 1
                ? AvatarCustomization.AvatarGender.Female
                : AvatarCustomization.AvatarGender.Male;

            ApplyBasicClothes(gender);
            int skinCount = GetSkinCount();
            int hairCount = GetHairStyleCount();

            if (skinCount > 0)
                avatar.SkinMaterialIndex = Mathf.Clamp(saved.skin, 0, skinCount - 1);

            if (hairCount > 0)
            {
                avatar.HairStyleIndex = Mathf.Clamp(saved.hairStyle, 0, hairCount - 1);
                int colors = GetHairColorCount(avatar.HairStyleIndex);
                avatar.HairMaterialIndex = colors > 0
                    ? Mathf.Clamp(saved.hairColor, 0, colors - 1) : 0;
            }

            avatar.UpdateCustomization();
        }

        private void SelectGender(AvatarCustomization.AvatarGender gender)
        {
            if (avatar.CurrentGender == gender)
                return;

            ApplyBasicClothes(gender);
            avatar.SkinMaterialIndex = 0;
            avatar.HairMaterialIndex = 0;
            avatar.UpdateCustomization();
        }

        private void NextSkin()
        {
            int count = GetSkinCount();
            if (count <= 0) return;
            avatar.SkinMaterialIndex = Wrap(avatar.SkinMaterialIndex + 1, count);
            avatar.UpdateCustomization();
        }

        private void NextHair(int direction)
        {
            int count = GetHairStyleCount();
            if (count <= 0) return;
            avatar.HairStyleIndex = Wrap(avatar.HairStyleIndex + direction, count);
            avatar.HairMaterialIndex = 0;
            avatar.UpdateCustomization();
        }

        private void NextHairColor()
        {
            int count = GetHairColorCount(avatar.HairStyleIndex);
            if (count <= 0) return;
            avatar.HairMaterialIndex = Wrap(avatar.HairMaterialIndex + 1, count);
            avatar.UpdateCustomization();
        }

        private void FinishSetup()
        {
            AccountGate gate = AccountGate.Instance;
            if (!setupRequired || avatar == null || gate == null || !gate.Ready ||
                string.IsNullOrEmpty(activeUserId) ||
                gate.Session == null || gate.Session.UserId != activeUserId)
                return;

            AvatarLook look = new AvatarLook
            {
                version = SaveVersion,
                gender = avatar.CurrentGender == AvatarCustomization.AvatarGender.Female ? 1 : 0,
                skin = avatar.SkinMaterialIndex,
                hairStyle = avatar.HairStyleIndex,
                hairColor = avatar.HairMaterialIndex
            };

            PlayerPrefs.SetString(SaveKey(activeUserId), JsonUtility.ToJson(look));
            PlayerPrefs.Save();
            setupRequired = false;
            IsOpen = false;
        }

        private void OnGUI()
        {
            if (!IsOpen || !setupRequired || avatar == null ||
                avatar.AvatarReferences == null)
                return;

            float width = Mathf.Min(410f, Screen.width - 24f);
            float px = Mathf.Max(12f, Screen.width - width - 15f);
            // Avoid the existing account HUD at the upper-right corner.
            float py = Screen.height >= 580 ? 166f : 90f;
            float height = Mathf.Max(220f, Screen.height - py - 16f);

            GUI.Box(new Rect(px, py, width, height), "최초 캐릭터 설정");
            GUILayout.BeginArea(new Rect(px + 16f, py + 32f,
                width - 32f, height - 44f));

            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("로비 입장을 환영합니다!");
            GUILayout.Label("기본 외형을 선택한 뒤 '설정 완료'를 눌러 주세요.");
            GUILayout.Space(12f);

            GUILayout.Label("성별");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("남성", GUILayout.Height(36f)))
                SelectGender(AvatarCustomization.AvatarGender.Male);
            if (GUILayout.Button("여성", GUILayout.Height(36f)))
                SelectGender(AvatarCustomization.AvatarGender.Female);
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);

            if (GUILayout.Button("피부색 변경", GUILayout.Height(34f)))
                NextSkin();

            GUILayout.Space(10f);
            GUILayout.Label("헤어스타일");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("이전 헤어", GUILayout.Height(34f)))
                NextHair(-1);
            if (GUILayout.Button("다음 헤어", GUILayout.Height(34f)))
                NextHair(1);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("머리색 변경", GUILayout.Height(34f)))
                NextHairColor();

            GUILayout.Space(14f);
            GUILayout.Label("상의·하의·기본 신발은 고정됩니다.");
            GUILayout.Label("모자·안경·다른 신발은 추후 상점에서 구매합니다.");
            GUILayout.Space(14f);

            if (GUILayout.Button("설정 완료 · 로비 시작", GUILayout.Height(42f)))
                FinishSetup();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}

#else

// A project without Sunbox still compiles and the shared Player C-key handler
// has no effect. The full initial setup needs the actual Sunbox avatar.
using UnityEngine;
namespace CampusCommons.GrandLibrary
{
    public class LibraryAvatarCustomizerUI : MonoBehaviour
    {
        public bool IsOpen => false;
        public void Initialize(GameObject avatar) { }
        public void Toggle() { }
        public void Close() { }
    }
}
#endif
