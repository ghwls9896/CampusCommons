
#if UNITY_EDITOR

using CampusCommons.Account;
using UnityEngine;

namespace CampusCommons.TeamProgression
{
    public sealed class LevelUpPreviewToast : MonoBehaviour
    {
        public static LevelUpPreviewToast Instance
        {
            get;
            private set;
        }

        string levelText = "";
        string unlockText = "";

        float hideAt;

        GUIStyle titleStyle;
        GUIStyle levelStyle;
        GUIStyle unlockStyle;
        GUIStyle detailStyle;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance == null)
            {
                new GameObject("Campus level-up notification")
                    .AddComponent<LevelUpPreviewToast>();
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void ShowLevelUp(
            int previousLevel,
            int newLevel)
        {
            if (newLevel <= previousLevel)
                return;

            levelText =
                "Lv." + previousLevel +
                "  →  Lv." + newLevel;

            string unlocked = "";

            if (previousLevel < 5 && newLevel >= 5)
            {
                unlocked = "파란 아우라";
            }

            if (previousLevel < 10 && newLevel >= 10)
            {
                if (unlocked.Length > 0)
                    unlocked += " · ";

                unlocked += "마스코트 망토";
            }

            if (previousLevel < 15 && newLevel >= 15)
            {
                if (unlocked.Length > 0)
                    unlocked += " · ";

                unlocked += "금빛 날개";
            }

            unlockText = unlocked.Length > 0
                ? unlocked + " 해금!"
                : "새로운 레벨에 도달했습니다!";

            hideAt = Time.unscaledTime + 3.5f;

            Debug.Log(
                "[CampusCommons] Preview level up: " +
                levelText +
                " (NOT SAVED)"
            );
        }

        void PrepareStyles()
        {
            if (titleStyle != null)
                return;

            Font font =
                Resources.Load<Font>("KIT/NotoSansKR");

            titleStyle = MakeStyle(
                font, 24,
                new Color(1f, 0.81f, 0.35f),
                true);

            levelStyle = MakeStyle(
                font, 21,
                new Color(0.45f, 0.87f, 1f),
                true);

            unlockStyle = MakeStyle(
                font, 14,
                Color.white,
                true);

            detailStyle = MakeStyle(
                font, 10,
                new Color(0.70f, 0.76f, 0.84f),
                false);
        }

        static GUIStyle MakeStyle(
            Font font,
            int size,
            Color color,
            bool bold)
        {
            GUIStyle style =
                new GUIStyle(GUI.skin.label);

            if (font != null)
                style.font = font;

            style.fontSize = size;

            style.fontStyle = bold
                ? FontStyle.Bold
                : FontStyle.Normal;

            style.alignment =
                TextAnchor.MiddleCenter;

            style.normal.textColor = color;

            return style;
        }

        void DrawRectangle(
            Rect rect,
            Color color)
        {
            Color old = GUI.color;

            GUI.color = color;

            GUI.DrawTexture(
                rect,
                Texture2D.whiteTexture);

            GUI.color = old;
        }

        void OnGUI()
        {
            if (Time.unscaledTime >= hideAt)
                return;

            if (string.IsNullOrEmpty(levelText))
                return;

            // 로그인 상태 확인
            AccountGate gate = AccountGate.Instance;
            AccountLevelUnlocks unlocks =
                AccountLevelUnlocks.Instance;

            LevelProgressionHUD hud =
                LevelProgressionHUD.Instance;

            if (gate == null ||
                !gate.Ready ||
                gate.Session == null ||
                !gate.Session.IsAuthenticated ||
                unlocks == null ||
                !unlocks.IsReady ||
                unlocks.CurrentUserId != gate.Session.UserId ||
                hud == null ||
                !hud.previewXpInEditor)
            {
                return;
            }

            PrepareStyles();

            float width =
                Mathf.Min(340f, Screen.width - 24f);

            float height = 158f;

            if (width < 230f ||
                Screen.height < height + 24f)
                return;

            float x = (Screen.width - width) / 2f;

            float y = Mathf.Clamp(
                Screen.height * 0.28f,
                12f,
                Screen.height - height - 12f);

            float remaining =
                hideAt - Time.unscaledTime;

            float alpha =
                Mathf.Clamp01(remaining / 0.5f);

            int oldDepth = GUI.depth;
            Color oldColor = GUI.color;

            GUI.depth = -20000;

            // 배경
            DrawRectangle(
                new Rect(x, y, width, height),
                new Color(
                    0.045f, 0.08f, 0.15f,
                    0.96f * alpha));

            // 금색 윗줄
            DrawRectangle(
                new Rect(x, y, width, 4f),
                new Color(
                    1f, 0.78f, 0.26f, alpha));

            GUI.color =
                new Color(1f, 1f, 1f, alpha);

            // LEVEL UP!
            GUI.Label(
                new Rect(
                    x + 12f, y + 15f,
                    width - 24f, 35f),
                "LEVEL UP!",
                titleStyle);

            // 레벨 변화
            GUI.Label(
                new Rect(
                    x + 12f, y + 54f,
                    width - 24f, 31f),
                levelText,
                levelStyle);

            // 해금 문구
            GUI.Label(
                new Rect(
                    x + 10f, y + 95f,
                    width - 20f, 24f),
                unlockText,
                unlockStyle);

            // 테스트 안내
            GUI.Label(
                new Rect(
                    x + 10f, y + 129f,
                    width - 20f, 18f),
                "개발용 XP 미리보기 · 실제 계정 변경 없음",
                detailStyle);

            GUI.color = oldColor;
            GUI.depth = oldDepth;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}

#endif
