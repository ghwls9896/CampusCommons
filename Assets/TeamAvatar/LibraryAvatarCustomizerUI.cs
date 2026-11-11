#if CAMPUS_SUNBOX_AVATAR

using System.Collections.Generic;
using UnityEngine;
using Sunbox.Avatars;

namespace CampusCommons.GrandLibrary
{
    public class LibraryAvatarCustomizerUI : MonoBehaviour
    {
        private AvatarCustomization avatar;
        private Vector2 scroll;

        public bool IsOpen { get; private set; }

        public void Initialize(GameObject avatarVisual)
        {
            avatar = avatarVisual.GetComponentInChildren
                <AvatarCustomization>(true);

            if (avatar == null)
                Debug.LogError("AvatarCustomization을 찾지 못했습니다.");
        }

        public void Toggle()
        {
            if (avatar != null)
                IsOpen = !IsOpen;
        }

        public void Close()
        {
            IsOpen = false;
        }

        private int Wrap(int value, int count)
        {
            return count <= 0 ? 0 :
                (value % count + count) % count;
        }

        private bool Compatible(
            ClothingItem item,
            AvatarCustomization.AvatarGender gender)
        {
            if (item == null || item.IsEmpty)
                return false;

            return gender == AvatarCustomization.AvatarGender.Male
                ? item.MaleMesh != null
                : item.FemaleMesh != null;
        }

        private List<ClothingItem> GetClothes(
            SlotType slot,
            AvatarCustomization.AvatarGender gender)
        {
            var result = new List<ClothingItem>();
            var items = avatar.AvatarReferences.AvailableClothingItems;

            if (items == null) return result;

            foreach (var item in items)
            {
                if (item != null &&
                    item.SlotType == slot &&
                    Compatible(item, gender))
                    result.Add(item);
            }

            return result;
        }

        private ClothingItem SuitableClothing(
            SlotType slot,
            ClothingItem current,
            AvatarCustomization.AvatarGender gender)
        {
            if (Compatible(current, gender))
                return current;

            var options = GetClothes(slot, gender);
            return options.Count > 0 ? options[0] : null;
        }

        private void ChangeGender(
            AvatarCustomization.AvatarGender gender)
        {
            if (avatar.CurrentGender == gender) return;

            // 새 성별에 사용할 수 있는 옷으로 먼저 교체
            avatar.ClothingItemTop = SuitableClothing(
                SlotType.Top, avatar.ClothingItemTop, gender);

            avatar.ClothingItemBottom = SuitableClothing(
                SlotType.Bottom, avatar.ClothingItemBottom, gender);

            avatar.ClothingItemShoes = SuitableClothing(
                SlotType.Shoes, avatar.ClothingItemShoes, gender);

            if (avatar.ClothingItemHat != null)
                avatar.ClothingItemHat = SuitableClothing(
                    SlotType.Hat, avatar.ClothingItemHat, gender);

            if (avatar.ClothingItemGlasses != null)
                avatar.ClothingItemGlasses = SuitableClothing(
                    SlotType.Glasses, avatar.ClothingItemGlasses, gender);

            avatar.SetGender(gender);
        }

        private void ChangeHair(int direction)
        {
            var hairs = avatar.AvatarReferences.HairItems;
            if (hairs == null || hairs.Length == 0) return;

            avatar.HairStyleIndex = Wrap(
                avatar.HairStyleIndex + direction, hairs.Length);

            avatar.HairMaterialIndex = 0;
            avatar.UpdateCustomization();
        }

        private void ChangeHairColor()
        {
            var hairs = avatar.AvatarReferences.HairItems;
            if (hairs == null || hairs.Length == 0) return;

            int index = Mathf.Clamp(
                avatar.HairStyleIndex, 0, hairs.Length - 1);

            var hair = hairs[index];
            if (hair == null || hair.Variations == null ||
                hair.Variations.Length == 0) return;

            avatar.HairMaterialIndex = Wrap(
                avatar.HairMaterialIndex + 1,
                hair.Variations.Length);

            avatar.UpdateCustomization();
        }

        private void ChangeSkin()
        {
            int count = avatar.CurrentGender ==
                AvatarCustomization.AvatarGender.Male
                    ? avatar.AvatarReferences.MaleSkinMaterials.Length
                    : avatar.AvatarReferences.FemaleSkinMaterials.Length;

            if (count == 0) return;

            avatar.SkinMaterialIndex = Wrap(
                avatar.SkinMaterialIndex + 1, count);

            avatar.UpdateCustomization();
        }

        private void ChangeClothing(
            SlotType slot, int direction)
        {
            var options = GetClothes(slot, avatar.CurrentGender);
            if (options.Count == 0) return;

            var current = avatar.GetClothingItemFromSlot(slot);
            int index = options.IndexOf(current);

            int next = index < 0 ? 0 :
                Wrap(index + direction, options.Count);

            avatar.AttachClothingItem(options[next], 0);
        }

        private void ChangeClothingColor(SlotType slot)
        {
            var item = avatar.GetClothingItemFromSlot(slot);
            if (item == null || item.Variations == null ||
                item.Variations.Length == 0) return;

            int current = avatar.GetClothingItemVariationIndex(slot);
            int next = Wrap(current + 1, item.Variations.Length);

            avatar.SetClothingItemVariation(slot, next);
        }

        private void ClothingRow(string title, SlotType slot)
        {
            var item = avatar.GetClothingItemFromSlot(slot);

            GUILayout.Label(title + ": " +
                (item != null ? item.Name : "없음"));

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("이전"))
                ChangeClothing(slot, -1);

            if (GUILayout.Button("다음"))
                ChangeClothing(slot, 1);

            if (GUILayout.Button("색상"))
                ChangeClothingColor(slot);

            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        private void OnGUI()
        {
            if (!IsOpen || avatar == null ||
                avatar.AvatarReferences == null) return;

            float width = Mathf.Min(360, Screen.width - 20);

            Rect panel = new Rect(
                Mathf.Max(10, Screen.width - width - 10),
                35, width, Mathf.Max(160, Screen.height - 50));

            GUI.Box(panel, "캐릭터 커스터마이징");

            GUILayout.BeginArea(new Rect(
                panel.x + 10, panel.y + 28,
                panel.width - 20, panel.height - 38));

            scroll = GUILayout.BeginScrollView(scroll);

            if (GUILayout.Button("닫기"))
                Close();

            GUILayout.Label("성별 선택");
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("남성"))
                ChangeGender(AvatarCustomization.AvatarGender.Male);

            if (GUILayout.Button("여성"))
                ChangeGender(AvatarCustomization.AvatarGender.Female);

            GUILayout.EndHorizontal();

            if (GUILayout.Button("피부색 변경"))
                ChangeSkin();

            GUILayout.Space(10);
            GUILayout.Label("헤어스타일");
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("이전 헤어"))
                ChangeHair(-1);

            if (GUILayout.Button("다음 헤어"))
                ChangeHair(1);

            GUILayout.EndHorizontal();

            if (GUILayout.Button("머리색 변경"))
                ChangeHairColor();

            GUILayout.Space(10);
            GUILayout.Label("의상");

            ClothingRow("상의", SlotType.Top);
            ClothingRow("하의", SlotType.Bottom);
            ClothingRow("신발", SlotType.Shoes);
            ClothingRow("안경", SlotType.Glasses);
            ClothingRow("모자", SlotType.Hat);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
#else
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
