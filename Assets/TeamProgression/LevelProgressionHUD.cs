
using CampusCommons.Account;
using UnityEngine;

namespace CampusCommons.TeamProgression
{
    [DefaultExecutionOrder(-27000)]
    public sealed class LevelProgressionHUD : MonoBehaviour
    {
        public static LevelProgressionHUD Instance
        {
            get;
            private set;
        }

        [Header("HUD Settings")]
        [SerializeField] bool showHud = true;

#if UNITY_EDITOR
        [Header("Editor XP Preview - NOT SAVED")]
        [Tooltip("Only changes this HUD. Never changes account XP or level.")]
        public bool previewXpInEditor;

        [Range(0, 10500)]
        public int previewTotalXp;
#endif

        GUIStyle titleStyle;
        GUIStyle levelStyle;
        GUIStyle normalStyle;
        GUIStyle smallStyle;
        GUIStyle noteStyle;

        readonly Color white =
            new Color(0.96f, 0.98f, 1f, 1f);

        readonly Color muted =
            new Color(0.68f, 0.76f, 0.85f, 1f);

        readonly Color cyan =
            new Color(0.30f, 0.80f, 1f, 1f);

        readonly Color disabled =
            new Color(0.42f, 0.47f, 0.54f, 1f);

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
                new GameObject("Campus progression HUD")
                    .AddComponent<LevelProgressionHUD>();
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

        void Update()
        {
            // L키로 HUD 켜기 / 끄기
            if (Input.GetKeyDown(KeyCode.L))
                showHud = !showHud;
        }



#if UNITY_EDITOR
public void AddPreviewXp(int amount)
{
    // 실제 계정 경험치는 변경하지 않음
    if (!previewXpInEditor || amount <= 0)
        return;

    int previousXp = previewTotalXp;

    int previousLevel =
        LevelProgressionRules.LevelForTotalXp(
            previousXp);

    previewTotalXp = Mathf.Clamp(
        previewTotalXp + amount,
        0,
        10500
    );

    int newLevel =
        LevelProgressionRules.LevelForTotalXp(
            previewTotalXp);

    Debug.Log(
        "[CampusCommons] Preview XP: " +
        previousXp + " -> " + previewTotalXp +
        " (NOT SAVED)"
    );

    // 레벨이 실제로 증가했을 때만 축하 알림
    if (newLevel > previousLevel)
    {
        LevelUpPreviewToast toast =
            LevelUpPreviewToast.Instance;

        if (toast != null)
        {
            toast.ShowLevelUp(
                previousLevel,
                newLevel
            );
        }
    }
}
#endif



        void PrepareStyles()
        {
            if (titleStyle != null)
                return;

            Font font =
                Resources.Load<Font>("KIT/NotoSansKR");

            titleStyle = CreateStyle(
                font, 11, white, true);

            levelStyle = CreateStyle(
                font, 23, white, true);

            normalStyle = CreateStyle(
                font, 11, white, false);

            smallStyle = CreateStyle(
                font, 10, muted, false);

            noteStyle = CreateStyle(
                font, 9, muted, false);
        }

        static GUIStyle CreateStyle(
            Font font,
            int fontSize,
            Color color,
            bool bold)
        {
            GUIStyle style =
                new GUIStyle(GUI.skin.label);

            if (font != null)
                style.font = font;

            style.fontSize = fontSize;
            style.fontStyle = bold
                ? FontStyle.Bold
                : FontStyle.Normal;

            style.normal.textColor = color;
            style.alignment = TextAnchor.MiddleLeft;
            style.clipping = TextClipping.Clip;

            return style;
        }

        void DrawFill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;

            GUI.DrawTexture(
                rect, Texture2D.whiteTexture);

            GUI.color = old;
        }

        void DrawText(
            float x, float y,
            float width, float height,
            string value, GUIStyle style)
        {
            GUI.Label(
                new Rect(x, y, width, height),
                value, style);
        }

        void DrawReward(
            Rect rect,
            string label,
            bool unlocked)
        {
            DrawFill(
                rect,
                unlocked
                    ? new Color(0.10f, 0.30f, 0.42f, 0.95f)
                    : new Color(0.13f, 0.17f, 0.23f, 0.95f));

            var style = new GUIStyle(smallStyle);
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor =
                unlocked ? cyan : disabled;

            GUI.Label(rect, label, style);
        }


        void OnGUI()
        {
            if (!showHud)
                return;

            AccountGate gate = AccountGate.Instance;
            AccountLevelUnlocks unlocks =
                AccountLevelUnlocks.Instance;

            if (gate == null ||
                !gate.Ready ||
                gate.Session == null ||
                !gate.Session.IsAuthenticated ||
                unlocks == null ||
                !unlocks.IsReady ||
                unlocks.CurrentUserId != gate.Session.UserId)
            {
                return;
            }

            PrepareStyles();

            int realLevel = unlocks.CurrentLevel;
            int displayLevel = realLevel;

            bool sample = false;
            long totalXp = 0;

#if UNITY_EDITOR
    if (previewXpInEditor)
    {
        sample = true;
        totalXp =
            Mathf.Clamp(previewTotalXp, 0, 10500);

        displayLevel =
            LevelProgressionRules.LevelForTotalXp(
                totalXp);
    }
#endif

            float progress = 0f;

            string xpText = "누적 XP: 서버 미연동";
            string nextText;

            if (displayLevel >=
                LevelProgressionRules.MaxLevel)
            {
                nextText = "최고 레벨 Lv.15";

                if (sample)
                {
                    progress = 1f;
                    xpText =
                        "미리보기 XP: " +
                        totalXp.ToString("N0");
                }
            }
            else
            {
                long nextXp =
                    LevelProgressionRules
                        .TotalXpRequiredForLevel(
                            displayLevel + 1);

                nextText =
                    "Lv." + (displayLevel + 1) +
                    " 기준: " +
                    nextXp.ToString("N0") + " XP";

                if (sample)
                {
                    long startXp =
                        LevelProgressionRules
                            .TotalXpRequiredForLevel(
                                displayLevel);

                    long range = nextXp - startXp;

                    progress = range > 0
                        ? Mathf.Clamp01(
                            (float)(totalXp - startXp) / range)
                        : 0f;

                    xpText =
                        "미리보기 XP: " +
                        totalXp.ToString("N0") +
                        " / " +
                        nextXp.ToString("N0");
                }
            }

            GUI.depth = 10;

            // ===================================
            // HUD 크기 및 위치
            // ===================================

            float width = Mathf.Min(
                260f, Screen.width - 20f);

            float height = 140f;

            float x = 10f;

            float y = Mathf.Max(
                8f,
                Screen.height - height - 8f);

            if (width < 220f)
                return;

            // 배경
            DrawFill(
                new Rect(x, y, width, height),
                new Color(0.045f, 0.075f, 0.12f, 0.94f));

            // 왼쪽 파란 테두리
            DrawFill(
                new Rect(x, y, 3f, height),
                cyan);

            // 타이틀
            DrawText(
                x + 12f, y + 5f,
                width - 24f, 17f,
                "CAMPUS COMMONS | LEVEL",
                titleStyle);

            // 레벨
            DrawText(
                x + 12f, y + 25f,
                88f, 30f,
                "Lv." + displayLevel,
                levelStyle);

            DrawText(
                x + 92f, y + 29f,
                45f, 23f,
                "/ 15",
                smallStyle);

            // 테스트 / 실제 계정 표시
            string modeText = sample
                ? "TEST ONLY"
                : "ACCOUNT";

            var modeStyle = new GUIStyle(smallStyle);
            modeStyle.alignment =
                TextAnchor.MiddleRight;

            modeStyle.normal.textColor =
                sample
                    ? new Color(1f, 0.76f, 0.35f)
                    : cyan;

            DrawText(
                x + width - 96f,
                y + 30f,
                84f, 20f,
                modeText,
                modeStyle);

            // 경험치
            DrawText(
                x + 12f, y + 56f,
                width - 24f, 15f,
                xpText,
                normalStyle);

            // 경험치 바
            Rect barBackground = new Rect(
                x + 12f,
                y + 76f,
                width - 24f,
                7f);

            DrawFill(
                barBackground,
                new Color(0.24f, 0.29f, 0.34f, 1f));

            if (sample && progress > 0f)
            {
                DrawFill(
                    new Rect(
                        barBackground.x,
                        barBackground.y,
                        barBackground.width * progress,
                        barBackground.height),
                    cyan);
            }

            // 다음 레벨
            DrawText(
                x + 12f, y + 86f,
                width - 24f, 15f,
                nextText,
                smallStyle);

            // 레벨별 보상
            float rewardWidth =
                (width - 32f) / 3f;

            float rewardY = y + 104f;

            DrawReward(
                new Rect(
                    x + 12f,
                    rewardY,
                    rewardWidth,
                    20f),
                "Lv.5 아우라",
                displayLevel >= 5);

            DrawReward(
                new Rect(
                    x + 16f + rewardWidth,
                    rewardY,
                    rewardWidth,
                    20f),
                "Lv.10 망토",
                displayLevel >= 10);

            DrawReward(
                new Rect(
                    x + 20f + rewardWidth * 2f,
                    rewardY,
                    rewardWidth,
                    20f),
                "Lv.15 날개",
                displayLevel >= 15);

            // 하단 안내
            string footer = sample
                ? "XP 미리보기 | 실제 Lv." +
                  realLevel + " | 저장 안 됨"
                : "L: HUD 숨기기 | XP 연동 대기";

            DrawText(
                x + 12f, y + 126f,
                width - 24f, 12f,
                footer,
                noteStyle);
        }


        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
