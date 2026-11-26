using CampusCommons.Account;
using UnityEngine;

namespace CampusCommons.TeamProgression
{
    /// <summary>
    /// Lv.5-15 cosmetics are owned according to the authenticated, server-read level.
    /// Each level grants exactly one distinct inventory entry. Nothing auto-equips.
    /// The equipped bit mask is a local presentation preference, NOT an item grant,
    /// proof of ownership, server purchase, XP update, or multiplayer sync.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CosmeticEquipmentState : MonoBehaviour
    {
        public const int FirstRewardLevel = 5;
        public const int LastRewardLevel = 15;
        public const int RewardCount = LastRewardLevel - FirstRewardLevel + 1; // 11

        public static CosmeticEquipmentState Instance { get; private set; }

        private string currentUserId;
        private int accountLevel;
        private int equippedMask;
        private int editorPreviewMask;
        private bool editorPreviewActive;
        private int editorPreviewLevel;

        public string Feedback { get; private set; } =
            "아이템을 마우스 오른쪽 버튼으로 클릭하면 장착/해제됩니다.";

        private static readonly string[] Names =
        {
            "푸른 마법진", "마력 팔찌", "청록 파티클", "이동 잔광",
            "부유 수정", "마스코트 망토", "문양 발광", "별빛 문양",
            "금빛 파티클", "작은 날개", "최종 날개"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Instance == null)
            {
                new GameObject("Campus cosmetic equipment")
                    .AddComponent<CosmeticEquipmentState>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            SyncAccount();
        }

        private static bool ValidReward(int level)
        {
            return level >= FirstRewardLevel && level <= LastRewardLevel;
        }

        private static int RewardBit(int level)
        {
            return 1 << (level - FirstRewardLevel);
        }

        public static string RewardName(int level)
        {
            return ValidReward(level) ? Names[level - FirstRewardLevel] : "";
        }

        private static string PreferenceKey(string userId)
        {
            return "CampusCommons.CosmeticEquipment.v1." + userId;
        }

        private void ClearSession()
        {
            currentUserId = null;
            accountLevel = 0;
            equippedMask = 0;
            editorPreviewMask = 0;
            editorPreviewActive = false;
            editorPreviewLevel = 0;
        }

        private void SyncAccount()
        {
            AccountGate gate = AccountGate.Instance;
            AccountLevelUnlocks unlocks = AccountLevelUnlocks.Instance;

            if (gate == null || !gate.Ready || gate.Session == null ||
                !gate.Session.IsAuthenticated || unlocks == null ||
                !unlocks.IsReady || string.IsNullOrEmpty(unlocks.CurrentUserId) ||
                unlocks.CurrentUserId != gate.Session.UserId)
            {
                if (currentUserId != null)
                    ClearSession();
                return;
            }

            if (currentUserId != unlocks.CurrentUserId)
            {
                currentUserId = unlocks.CurrentUserId;
                accountLevel = 0; // Do not compare levels across different accounts.
                equippedMask = PlayerPrefs.GetInt(PreferenceKey(currentUserId), 0);
                editorPreviewMask = 0;
                editorPreviewActive = false;
                Feedback = "레벨 보상을 확인한 후 우클릭하여 장착하세요.";
            }

            // Ownership is always derived from the verified level, never PlayerPrefs.
            int latestLevel = Mathf.Clamp(unlocks.CurrentLevel, 1, 15);
            if (accountLevel > 0 && latestLevel > accountLevel)
            {
                int oldCount = Mathf.Clamp(accountLevel - FirstRewardLevel + 1, 0, RewardCount);
                int newCount = Mathf.Clamp(latestLevel - FirstRewardLevel + 1, 0, RewardCount);
                int added = newCount - oldCount;
                if (added == 1)
                    Feedback = "새 보상: Lv." + latestLevel + " " +
                        RewardName(latestLevel) + " (우클릭하여 장착)";
                else if (added > 1)
                    Feedback = "새 레벨 보상 " + added + "개가 인벤토리에 추가되었습니다.";
            }
            accountLevel = latestLevel;

            bool wantsPreview = false;
            int requestedPreviewLevel = accountLevel;
#if UNITY_EDITOR
            LevelCosmeticVisuals visuals = LevelCosmeticVisuals.Instance;
            if (visuals != null && visuals.previewInEditor)
            {
                wantsPreview = true;
                requestedPreviewLevel = Mathf.Clamp(visuals.previewLevel, 1, 15);
            }
#endif
            if (wantsPreview && !editorPreviewActive)
            {
                editorPreviewMask = equippedMask;
                Feedback = "에디터 미리보기: 장착 상태는 계정에 저장되지 않습니다.";
            }
            else if (!wantsPreview && editorPreviewActive)
            {
                Feedback = "실제 계정의 장착 상태로 돌아왔습니다.";
            }

            editorPreviewActive = wantsPreview;
            editorPreviewLevel = requestedPreviewLevel;
        }

        public int AvailableLevel
        {
            get
            {
                SyncAccount();
                if (currentUserId == null) return 0;
                return editorPreviewActive ? editorPreviewLevel : accountLevel;
            }
        }

        public bool IsEditorPreview
        {
            get { SyncAccount(); return editorPreviewActive; }
        }

        public bool OwnsReward(int rewardLevel)
        {
            return ValidReward(rewardLevel) && AvailableLevel >= rewardLevel;
        }

        public int OwnedCount
        {
            get { return Mathf.Clamp(AvailableLevel - FirstRewardLevel + 1, 0, RewardCount); }
        }

        public bool Equipped(int rewardLevel)
        {
            if (!OwnsReward(rewardLevel)) return false;
            int mask = editorPreviewActive ? editorPreviewMask : equippedMask;
            return (mask & RewardBit(rewardLevel)) != 0;
        }

        public static bool IsEquipped(int rewardLevel)
        {
            return Instance != null && Instance.Equipped(rewardLevel);
        }

        public bool ToggleReward(int rewardLevel)
        {
            if (!OwnsReward(rewardLevel))
            {
                Feedback = "아직 해금되지 않은 아이템입니다.";
                return false;
            }

            int mask = editorPreviewActive ? editorPreviewMask : equippedMask;
            int bit = RewardBit(rewardLevel);
            bool willEquip = (mask & bit) == 0;

            // Lv.11 is a decoration attached to the Lv.10 cape itself.
            if (willEquip && rewardLevel == 11 && (mask & RewardBit(10)) == 0)
            {
                Feedback = "Lv.10 마스코트 망토를 먼저 장착하세요.";
                return false;
            }

            if (willEquip)
            {
                mask |= bit;
                // Only one pair of wings should be visible at once.
                if (rewardLevel == 15) mask &= ~RewardBit(14);
                if (rewardLevel == 14) mask &= ~RewardBit(15);
            }
            else
            {
                mask &= ~bit;
                // Removing the cape also removes its glowing embroidery.
                if (rewardLevel == 10) mask &= ~RewardBit(11);
            }

            if (editorPreviewActive)
            {
                editorPreviewMask = mask;
            }
            else
            {
                equippedMask = mask;
                PlayerPrefs.SetInt(PreferenceKey(currentUserId), equippedMask);
                PlayerPrefs.Save();
            }

            Feedback = "Lv." + rewardLevel + " " + RewardName(rewardLevel) +
                (willEquip ? " 장착 완료" : " 장착 해제");
            return true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
