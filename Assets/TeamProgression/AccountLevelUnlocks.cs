using System;
using CampusCommons.Account;
using UnityEngine;

namespace CampusCommons.TeamProgression
{
    /// <summary>
    /// Read-only bridge from the authenticated account to local cosmetic unlock flags.
    /// It never grants XP, writes levels, or trusts a level supplied by gameplay/UI.
    /// </summary>
    [DefaultExecutionOrder(-30000)]
    public sealed class AccountLevelUnlocks : MonoBehaviour
    {
        public static AccountLevelUnlocks Instance { get; private set; }

        public event Action Changed;

        // No authenticated world entry => no unlocked cosmetics, even if a previous
        // account had a high level. CurrentLevel is 0 in that state.
        public bool IsReady { get; private set; }
        public int CurrentLevel { get; private set; }
        public string CurrentUserId { get; private set; }
        // 캐릭터 외형 표시 기준 (XP 계산과 무관)
private const int MinCosmeticLevel = 1;
private const int MaxCosmeticLevel = 15;

private const int AuraLevel = 5;
private const int CapeLevel = 10;
private const int SpecialLevel = 15;

        
public bool AuraUnlocked =>
    IsReady && CurrentLevel >= AuraLevel;

public bool CapeUnlocked =>
    IsReady && CurrentLevel >= CapeLevel;

public bool SpecialEffectUnlocked =>
    IsReady && CurrentLevel >= SpecialLevel;


        AccountGate gate;
        AccountSession session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic()
        {
            Instance = null;
        }

        // AccountGate is installed BeforeSceneLoad. This listener is installed
        // AfterSceneLoad, with no Player, scene or prefab modifications.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance == null)
                new GameObject("Campus level unlocks").AddComponent<AccountLevelUnlocks>();
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

        void Start()
        {
            BindGateIfAvailable();
        }

        void Update()
        {
            // Retry only when the account bootstrap has not completed, or when
            // an editor fixture replaced AccountGate's AccountSession.
            if (gate == null)
                BindGateIfAvailable();
            else if (gate.Session != session)
                BindSession();
        }

        void BindGateIfAvailable()
        {
            AccountGate candidate = AccountGate.Instance;
            if (candidate == null)
                return;

            if (gate != candidate)
            {
                Unbind();
                gate = candidate;
                gate.EntryReady += OnEntryReady;
                gate.EntryRevoked += OnEntryRevoked;
            }

            BindSession();
        }

        void BindSession()
        {
            if (session != null)
                session.Changed -= OnSessionChanged;

            session = gate != null ? gate.Session : null;

            if (session != null)
                session.Changed += OnSessionChanged;

            RefreshFromAuthenticatedSession();
        }

        void OnEntryReady(AccountProfile profile)
        {
            // Configure() may have replaced the session in an editor fixture.
            if (gate != null && gate.Session != session)
                BindSession();

            ApplyTrustedProfile(profile);
        }

        void OnEntryRevoked()
        {
            Clear();
        }

        void OnSessionChanged()
        {
            RefreshFromAuthenticatedSession();
        }

        void RefreshFromAuthenticatedSession()
        {
            if (gate == null || !gate.Ready || session == null ||
                !session.IsAuthenticated)
            {
                Clear();
                return;
            }

            ApplyTrustedProfile(session.Profile);
        }

        void ApplyTrustedProfile(AccountProfile profile)
        {
            if (gate == null || !gate.Ready || session == null ||
                !session.IsAuthenticated || profile == null ||
                string.IsNullOrEmpty(profile.user_id) ||
                profile.user_id != session.UserId ||
                profile.level < MinCosmeticLevel ||
                profile.level > MaxCosmeticLevel)
            {
                Clear();
                return;
            }

            bool changed = !IsReady ||
                           CurrentLevel != profile.level ||
                           CurrentUserId != profile.user_id;

            IsReady = true;
            CurrentLevel = profile.level;
            CurrentUserId = profile.user_id;

            if (changed)
            {
                Debug.Log("[CampusCommons] Account level loaded: Lv." +
                          CurrentLevel + " (cosmetic display only).");
                Changed?.Invoke();
            }
        }

        void Clear()
        {
            bool changed = IsReady || CurrentLevel != 0 ||
                           !string.IsNullOrEmpty(CurrentUserId);

            IsReady = false;
            CurrentLevel = 0;
            CurrentUserId = null;

            if (changed)
            {
                Debug.Log("[CampusCommons] Account level cleared (cosmetic unlocks locked).");
                Changed?.Invoke();
            }
        }

        void Unbind()
        {
            if (session != null)
            {
                session.Changed -= OnSessionChanged;
                session = null;
            }

            if (gate != null)
            {
                gate.EntryReady -= OnEntryReady;
                gate.EntryRevoked -= OnEntryRevoked;
                gate = null;
            }
        }

        void OnDestroy()
        {
            Unbind();
            if (Instance == this)
                Instance = null;
        }
    }
}
