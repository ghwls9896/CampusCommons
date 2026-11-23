using System;
using UnityEngine;
#if CAMPUS_SUNBOX_AVATAR
using Sunbox.Avatars;
#endif

namespace CampusCommons.Multiplayer
{
    [Serializable]
    internal sealed class LobbyAppearance
    {
        public string type = "appearance";
        public int body, skin, hair, hair_color;
        public string[] items;
        public int[] colors;

        internal const int SlotCount = 5;
        internal bool HasSlots => items?.Length == SlotCount && colors?.Length == SlotCount;

        internal bool Matches(LobbyAppearance other)
        {
            if (other == null || !HasSlots || !other.HasSlots || body != other.body ||
                skin != other.skin || hair != other.hair || hair_color != other.hair_color) return false;
            for (int i = 0; i < SlotCount; i++)
                if (items[i] != other.items[i] || colors[i] != other.colors[i]) return false;
            return true;
        }
    }

    internal sealed class LobbyAvatarStyle
    {
#if CAMPUS_SUNBOX_AVATAR
        static readonly SlotType[] Slots = { SlotType.Top, SlotType.Bottom, SlotType.Shoes, SlotType.Glasses, SlotType.Hat };
        readonly AvatarCustomization avatar;

        internal LobbyAvatarStyle(Transform root) => avatar = root.GetComponentInChildren<AvatarCustomization>(true);

        internal LobbyAppearance Capture()
        {
            if (!avatar || !avatar.AvatarReferences) return null;
            var look = new LobbyAppearance {
                body = avatar.CurrentGender == AvatarCustomization.AvatarGender.Female ? 1 : 0,
                skin = ClampIndex(avatar.SkinMaterialIndex, 256),
                hair = ClampIndex(avatar.HairStyleIndex, 256),
                hair_color = ClampIndex(avatar.HairMaterialIndex, 256),
                items = new string[Slots.Length],
                colors = new int[Slots.Length],
            };
            for (int i = 0; i < Slots.Length; i++)
            {
                var item = avatar.GetClothingItemFromSlot(Slots[i]);
                string name = item && !item.IsEmpty ? item.Name : "";
                if (!IsWireName(name)) name = "";
                look.items[i] = name;
                look.colors[i] = name.Length > 0 ? ClampIndex(avatar.GetClothingItemVariationIndex(Slots[i]), 256) : 0;
            }
            return look;
        }

        internal bool Apply(LobbyAppearance look)
        {
            if (!avatar || !avatar.AvatarReferences || look == null || !look.HasSlots || look.body < 0 || look.body > 1)
                return false;
            var gender = (AvatarCustomization.AvatarGender)look.body;
            var references = avatar.AvatarReferences;
            bool male = gender == AvatarCustomization.AvatarGender.Male;
            var skin = male ? references.MaleSkinMaterials : references.FemaleSkinMaterials;
            var hairs = references.HairItems;
            if (skin == null || skin.Length == 0 || hairs == null || hairs.Length == 0) return false;

            avatar.SkinMaterialIndex = ClampIndex(look.skin, skin.Length);
            avatar.HairStyleIndex = ClampIndex(look.hair, hairs.Length);
            var hair = hairs[avatar.HairStyleIndex];
            avatar.HairMaterialIndex = ClampIndex(look.hair_color, hair ? hair.Variations?.Length ?? 0 : 0);
            for (int i = 0; i < Slots.Length; i++)
            {
                var item = FindItem(look.items[i], Slots[i], male);
                int color = ClampIndex(look.colors[i], item ? item.Variations?.Length ?? 0 : 0);
                switch (Slots[i])
                {
                    case SlotType.Top: avatar.ClothingItemTop = item; avatar.ClothingItemTopVariationIndex = color; break;
                    case SlotType.Bottom: avatar.ClothingItemBottom = item; avatar.ClothingItemBottomVariationIndex = color; break;
                    case SlotType.Shoes: avatar.ClothingItemShoes = item; avatar.ClothingItemShoesVariationIndex = color; break;
                    case SlotType.Glasses: avatar.ClothingItemGlasses = item; avatar.ClothingItemGlassesVariationIndex = color; break;
                    case SlotType.Hat: avatar.ClothingItemHat = item; avatar.ClothingItemHatVariationIndex = color; break;
                }
            }

            if (avatar.CurrentGender != gender) avatar.SetGender(gender);
            else
            {
                avatar.UpdateClothing();
                avatar.UpdateCustomization();
            }
            return true;
        }

        ClothingItem FindItem(string name, SlotType slot, bool male)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var items = avatar.AvatarReferences.AvailableClothingItems;
            if (items != null)
                foreach (var item in items)
                    if (Usable(item, slot, male) && string.Equals(item.Name, name, StringComparison.Ordinal)) return item;
            var fallback = avatar.GetClothingItemFromSlot(slot);
            return Usable(fallback, slot, male) ? fallback : null;
        }

        static bool Usable(ClothingItem item, SlotType slot, bool male) =>
            item && !item.IsEmpty && item.SlotType == slot && (male ? item.MaleMesh : item.FemaleMesh);

        static int ClampIndex(int index, int count) => index >= 0 && index < count ? index : 0;

        static bool IsWireName(string name)
        {
            if (name == null || name.Length > 64) return false;
            foreach (char c in name)
                if (!(c >= 'A' && c <= 'Z') && !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') &&
                    c != ' ' && c != '_' && c != '-') return false;
            return true;
        }
#else
        internal LobbyAvatarStyle(Transform root) { }
        internal LobbyAppearance Capture() => null;
        internal bool Apply(LobbyAppearance look) => false;
#endif
    }
}
