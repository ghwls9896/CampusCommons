
using CampusCommons.Account;
using CampusCommons.StudyImmersive;
using UnityEngine;

namespace CampusCommons.TeamProgression
{
    [DefaultExecutionOrder(-25000)]
    public sealed class EscapeResultPreviewBridge : MonoBehaviour
    {
        static EscapeResultPreviewBridge instance;

        EscapeGame observedGame;

        bool roundStarted;
        bool notificationSent;
        string localRoundId;

        float nextSearchTime;
        string observedUserId;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic()
        {
            instance = null;
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance == null)
            {
                new GameObject("Campus escape XP bridge")
                    .AddComponent<EscapeResultPreviewBridge>();
            }
        }
#endif

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
#if UNITY_EDITOR
            // 로그인한 계정에서만 테스트
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
                ClearTracking();
                return;
            }

            // 계정이 변경되면 이전 게임 추적 초기화
            if (observedUserId != gate.Session.UserId)
            {
                ClearTracking();
                observedUserId = gate.Session.UserId;
            }

            // 탈출게임 오브젝트 찾기
            if (observedGame == null ||
                Time.unscaledTime >= nextSearchTime)
            {
                nextSearchTime = Time.unscaledTime + 1f;

                EscapeGame found =
                    FindAnyObjectByType<EscapeGame>();

                if (observedGame != found)
                {
                    observedGame = found;
                    roundStarted = false;
                    notificationSent = false;
                }
            }

            if (observedGame == null)
                return;

            // 라운드 시작 전 또는 로비로 복귀한 상태
            if (!observedGame.IsRunning)
            {
                roundStarted = false;
                notificationSent = false;
                return;
            }

            // 실제 탈출 라운드 진행 중
            if (!observedGame.IsEnded)
            {
                if (!roundStarted)
                {
                roundStarted = true;
                notificationSent = false;
                // 탈출게임 한 판에 하나의 고유 ID 생성
                localRoundId = System.Guid.NewGuid().ToString("N");
                }

                return;
            }

            // 이미 처리했거나 정상 시작을 관찰하지 못한 경우
            if (!roundStarted || notificationSent)
                return;

            notificationSent = true;

            GameOutcome outcome;

            if (observedGame.Completed)
            {
                outcome = GameOutcome.EscapeSuccess;
            }
            else if (observedGame.Caught ||
                     observedGame.TimedOut)
            {
                outcome = GameOutcome.EscapeFailure;
            }
            else
            {
                return;
            }

            // 중복 검사 통과 시에만 XP 알림과 HUD 미리보기 실행
ProgressionRewardGateway gateway =
    ProgressionRewardGateway.Instance;

if (gateway == null ||
    !gateway.RegisterLocalPreviewResult(
        localRoundId, outcome))
{
    return;
}

            ProgressionRewardToast toast =
                ProgressionRewardToast.Instance;

            if (toast == null)
            {
                Debug.LogWarning(
                    "[CampusCommons] XP notification not found.");
                return;
            }

            // 기존 테스트 알림을 자동으로 호출
            // 실제 계정 경험치는 절대 변경하지 않음
            toast.previewOutcome = outcome;
            
            // XP 획득 알림과 개발용 레벨 HUD 연결
            LevelProgressionHUD hud =
            LevelProgressionHUD.Instance;
            
            if (hud != null)
            {
            int rewardXp =
            LevelProgressionRules.RewardFor(outcome);
            hud.AddPreviewXp(rewardXp);
            }

            toast.showTestNotification = true;

            Debug.Log(
                "[CampusCommons] Escape result detected: " +
                outcome +
                " (XP preview only, not saved).");
                // 개발용 결과 기록 및 중복 처리 확인

#endif
        }

        void ClearTracking()
        {
            observedGame = null;
            observedUserId = null;

            roundStarted = false;
            notificationSent = false;
            nextSearchTime = 0f;
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
