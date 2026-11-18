
using CampusCommons.Account;
using UnityEngine;

namespace CampusCommons.TeamProgression
{
    [DefaultExecutionOrder(-26000)]
    public sealed class ProgressionRewardToast : MonoBehaviour
    {
        public static ProgressionRewardToast Instance
        {
            get;
            private set;
        }

        [Header("Notification Settings")]
        [SerializeField]
        float displaySeconds = 3f;

#if UNITY_EDITOR
        [Header("Editor Test - No Real XP")]
        [Tooltip("Choose which game result to preview.")]
        public GameOutcome previewOutcome =
            GameOutcome.QuizWin;

        [Tooltip("Check during Play to show the test notification.")]
        public bool showTestNotification;
#endif

        string message = "";
        float hideAt;

        GUIStyle messageStyle;
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
                new GameObject("Campus XP notification")
                    .AddComponent<ProgressionRewardToast>();
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

        bool HasActiveAccount()
        {
            AccountGate gate = AccountGate.Instance;
            AccountLevelUnlocks unlocks =
                AccountLevelUnlocks.Instance;

            return gate != null &&
                   gate.Ready &&
                   gate.Session != null &&
                   gate.Session.IsAuthenticated &&
                   unlocks != null &&
                   unlocks.IsReady &&
                   unlocks.CurrentUserId ==
                       gate.Session.UserId;
        }

        void Update()
        {
            if (!HasActiveAccount())
            {
                hideAt = 0f;
                message = "";

#if UNITY_EDITOR
                showTestNotification = false;
#endif
                return;
            }

#if UNITY_EDITOR
            if (showTestNotification)
            {
                showTestNotification = false;
                ShowTestOutcome(previewOutcome);
            }
#endif
        }

#if UNITY_EDITOR
        void ShowTestOutcome(GameOutcome outcome)
        {
            // This calculates the proposed reward only.
            // It never changes the authenticated account.
            int xp =
                LevelProgressionRules.RewardFor(outcome);

            string resultName = OutcomeName(outcome);

            message = resultName + "  +" +
                      xp.ToString("N0") + " XP";

            hideAt = Time.unscaledTime +
                Mathf.Max(0.5f, displaySeconds);

            Debug.Log(
                "[CampusCommons] XP toast preview: " +
                resultName + " +" + xp +
                " XP (NOT awarded or saved).");
        }
#endif

        static string OutcomeName(GameOutcome outcome)
        {
            switch (outcome)
            {
                case GameOutcome.QuizWin:
                    return "ÄûÁî ½Â¸®";

                case GameOutcome.QuizLoss:
                    return "ÄûÁî ÆÐ¹è";

                case GameOutcome.QuizDraw:
                    return "ÄûÁî ¹«½ÂºÎ";

                case GameOutcome.EscapeSuccess:
                    return "Å»Ãâ ¼º°ø";

                case GameOutcome.EscapeFailure:
                    return "Å»Ãâ ½ÇÆÐ";

                default:
                    return "°ÔÀÓ °á°ú";
            }
        }

        void PrepareStyles()
        {
            if (messageStyle != null)
                return;

            Font font =
                Resources.Load<Font>("KIT/NotoSansKR");

            messageStyle =
                new GUIStyle(GUI.skin.label);

            if (font != null)
                messageStyle.font = font;

            messageStyle.fontSize = 17;
            messageStyle.fontStyle = FontStyle.Bold;
            messageStyle.alignment =
                TextAnchor.MiddleCenter;

            messageStyle.normal.textColor =
                new Color(0.45f, 0.88f, 1f);

            detailStyle =
                new GUIStyle(GUI.skin.label);

            if (font != null)
                detailStyle.font = font;

            detailStyle.fontSize = 11;
            detailStyle.alignment =
                TextAnchor.MiddleCenter;

            detailStyle.normal.textColor =
                new Color(0.88f, 0.92f, 1f);
        }

        void OnGUI()
        {
            if (!HasActiveAccount())
                return;

            if (string.IsNullOrEmpty(message) ||
                Time.unscaledTime >= hideAt)
            {
                return;
            }

            PrepareStyles();

            float remaining =
                hideAt - Time.unscaledTime;

            // Fade out during the final half-second.
            float alpha =
                Mathf.Clamp01(remaining / 0.5f);

            float width =
                Mathf.Min(250f, Screen.width - 16f);

            if (width < 170f)
                return;

            float height = 78f;

            // Lower-right, above the bottom UI.
            float x = Screen.width - width - 12f;

            float y = Mathf.Max(
                8f, Screen.height - height - 140f);

            GUI.depth = 5;

            Color previousColor = GUI.color;

            // Background
            GUI.color = new Color(
                0.045f, 0.075f, 0.12f,
                0.95f * alpha);

            GUI.DrawTexture(
                new Rect(x, y, width, height),
                Texture2D.whiteTexture);

            // Cyan accent line
            GUI.color = new Color(
                0.3f, 0.8f, 1f, alpha);

            GUI.DrawTexture(
                new Rect(x, y, 4f, height),
                Texture2D.whiteTexture);

            GUI.color = new Color(
                1f, 1f, 1f, alpha);

            GUI.Label(
                new Rect(
                    x + 8f, y + 10f,
                    width - 16f, 30f),
                message,
                messageStyle);

            GUI.Label(
                new Rect(
                    x + 8f, y + 42f,
                    width - 16f, 23f),
                "TEST ONLY - XP Áö±Þ ¹× ÀúÀå ¾È µÊ",
                detailStyle);

            GUI.color = previousColor;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
