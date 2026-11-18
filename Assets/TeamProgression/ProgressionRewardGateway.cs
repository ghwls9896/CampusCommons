
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Account;
using UnityEngine;

namespace CampusCommons.TeamProgression
{
    // 서버가 검증하고 반환할 응답 형식의 초안.
    // 실제 API 계약 확정 시 서버 담당자와 맞춰야 함.
    [Serializable]
    public sealed class VerifiedXpReceipt
    {
        public string user_id;
        public string source_event_id;
        public int awarded_xp;
        public int new_level;
        public long total_xp;
        public bool duplicate;
    }

    // 미래 서버 연동 지점.
    // 반드시 서버에서 검증한 이벤트 ID만 사용해야 함.
    // 클라이언트가 XP 지급량을 지정하지 않음.
    public interface IProgressionAwardApi
    {
        Task<VerifiedXpReceipt> ConfirmVerifiedActivityAsync(
            string source,
            string serverVerifiedEventId,
            CancellationToken cancellationToken);
    }

    [DefaultExecutionOrder(-25500)]
    public sealed class ProgressionRewardGateway : MonoBehaviour
    {
        public static ProgressionRewardGateway Instance
        {
            get;
            private set;
        }

        // 로컬 중복 확인용. 서버 지급 이력이 아님.
        readonly HashSet<string> recordedResults =
            new HashSet<string>();

        string trackedUserId;
        string lastLocalEventId;
        GameOutcome lastOutcome;

        IProgressionAwardApi serverApi;

#if UNITY_EDITOR
        [Header("Local duplicate test")]
        [Tooltip("After an escape result, check this to test duplicate blocking.")]
        public bool testLastResultAgain;
#endif

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
                new GameObject("Campus reward gateway")
                    .AddComponent<ProgressionRewardGateway>();
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

        static string CurrentAuthenticatedUser()
        {
            AccountGate gate = AccountGate.Instance;
            AccountLevelUnlocks unlocks =
                AccountLevelUnlocks.Instance;

            if (gate == null ||
                !gate.Ready ||
                gate.Session == null ||
                !gate.Session.IsAuthenticated ||
                unlocks == null ||
                !unlocks.IsReady)
            {
                return null;
            }

            string userId = gate.Session.UserId;

            if (string.IsNullOrEmpty(userId) ||
                unlocks.CurrentUserId != userId)
            {
                return null;
            }

            return userId;
        }

        void SyncAccount()
        {
            string current = CurrentAuthenticatedUser();

            if (trackedUserId == current)
                return;

            // 로그아웃이나 계정 변경 시 이전 기록 제거
            recordedResults.Clear();
            lastLocalEventId = null;
            trackedUserId = current;
        }

        void Update()
        {
            SyncAccount();

#if UNITY_EDITOR
            if (testLastResultAgain)
            {
                testLastResultAgain = false;

                if (!string.IsNullOrEmpty(lastLocalEventId))
                {
                    RegisterLocalPreviewResult(
                        lastLocalEventId,
                        lastOutcome);
                }
                else
                {
                    Debug.Log(
                        "[CampusCommons] No previous result to test.");
                }
            }
#endif
        }

        // 실제 XP를 지급하지 않는 결과 기록 기능
        public bool RegisterLocalPreviewResult(
            string localRoundId,
            GameOutcome outcome)
        {
#if !UNITY_EDITOR
            return false;
#else
            SyncAccount();

            if (string.IsNullOrEmpty(trackedUserId))
            {
                Debug.LogWarning(
                    "[CampusCommons] Reward preview ignored: " +
                    "no authenticated account.");

                return false;
            }

            Guid parsedId;

            if (string.IsNullOrEmpty(localRoundId) ||
                !Guid.TryParse(localRoundId, out parsedId))
            {
                Debug.LogWarning(
                    "[CampusCommons] Invalid local round ID.");

                return false;
            }

            string source;

            switch (outcome)
            {
                case GameOutcome.EscapeSuccess:
                case GameOutcome.EscapeFailure:
                    source = "escape";
                    break;

                case GameOutcome.QuizWin:
                case GameOutcome.QuizLoss:
                case GameOutcome.QuizDraw:
                    source = "quiz";
                    break;

                default:
                    return false;
            }

            // 같은 라운드에서는 성공/실패 여부가 달라져도
            // 한 번만 기록
            string key =
                source + ":" + parsedId.ToString("N");

            if (!recordedResults.Add(key))
            {
                Debug.Log(
                    "[CampusCommons] Duplicate local result blocked. " +
                    "No server request.");

                return false;
            }

            lastLocalEventId = localRoundId;
            lastOutcome = outcome;

            int expectedXp =
                LevelProgressionRules.RewardFor(outcome);

            Debug.Log(
                "[CampusCommons] Reward intent prepared: " +
                source + " / " + outcome +
                " / expected " + expectedXp + " XP. " +
                "PREVIEW ONLY - NOT SAVED.");

            return true;
#endif
        }

        // 서버 기능이 합의·배포된 후에만
        // 검증된 서버 어댑터를 등록할 것
        public void RegisterServerAdapter(
            IProgressionAwardApi adapter)
        {
            serverApi = adapter;
        }

        // 미래 연동용. 현재 어댑터가 없으므로
        // 서버에 아무 요청도 보내지 않음.
        //
        // localRoundId를 여기 전달하면 안 됨.
        // 서버가 검증한 활동 ID만 전달해야 함.
        public async Task<bool> ConfirmServerVerifiedRewardAsync(
            string source,
            string serverVerifiedEventId,
            CancellationToken cancellationToken =
                default(CancellationToken))
        {
            if (serverApi == null)
            {
                Debug.LogWarning(
                    "[CampusCommons] XP server adapter is disabled.");

                return false;
            }

            SyncAccount();

            string expectedUserId = trackedUserId;

            if (string.IsNullOrEmpty(expectedUserId) ||
                string.IsNullOrEmpty(serverVerifiedEventId) ||
                (source != "quiz" && source != "escape"))
            {
                return false;
            }

            AccountGate gate = AccountGate.Instance;
            AccountSession session = gate.Session;

            VerifiedXpReceipt receipt =
                await serverApi.ConfirmVerifiedActivityAsync(
                    source,
                    serverVerifiedEventId,
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return false;

            // 요청 중 로그인 계정이 바뀌었는지 확인
            if (gate != AccountGate.Instance ||
                gate.Session != session ||
                CurrentAuthenticatedUser() != expectedUserId)
            {
                return false;
            }

            // 서버 응답을 검증한 후에만 계정 정보를 갱신
            if (receipt == null ||
                receipt.user_id != expectedUserId ||
                receipt.source_event_id != serverVerifiedEventId ||
                receipt.new_level < 1 ||
                receipt.new_level > 15 ||
                receipt.awarded_xp < 0 ||
                receipt.total_xp < 0)
            {
                Debug.LogWarning(
                    "[CampusCommons] Invalid XP server receipt.");

                return false;
            }

            // 기존 계정 API에서 다시 조회.
            // 실제 레벨 외형은 AccountLevelUnlocks가 반영함.
            await session.ReadProfileAsync(cancellationToken);

            return CurrentAuthenticatedUser() ==
                   expectedUserId;
        }

        void OnDestroy()
        {
            recordedResults.Clear();
            serverApi = null;

            if (Instance == this)
                Instance = null;
        }
    }
}
