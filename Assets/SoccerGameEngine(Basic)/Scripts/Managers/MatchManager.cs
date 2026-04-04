using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.Attack.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using Patterns.Singleton;
using RobustFSM.Interfaces;
using System;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Managers
{
    public enum MatchDifficulty
    {
        Casual,
        Normal,
        Hard
    }

    [Serializable]
    public struct MatchDifficultyProfile
    {
        [Range(0.8f, 2.0f)]
        public float PassMaxMultiplier;

        [Range(0.4f, 1.2f)]
        public float PassMinMultiplier;

        [Range(0.05f, 1.2f)]
        public float AICarrierLeadTime;

        [Range(0.25f, 3.0f)]
        public float AICarrierSideStepDistance;

        [Range(-1.0f, 0.0f)]
        public float AIBehindDotThreshold;

        [Range(0.25f, 3.0f)]
        public float AIBehindStickBreakDistance;

        [Range(0.6f, 1.0f)]
        public float AIChaseSlowdownWhenBehind;

        [Range(0f, 0.6f)]
        public float AIReactionDelayMin;

        [Range(0f, 0.8f)]
        public float AIReactionDelayMax;

        [Range(0f, 0.6f)]
        public float AIErrorChanceBase;

        [Range(0f, 0.6f)]
        public float AIPressureErrorBoost;

        [Range(0f, 0.5f)]
        public float AIDecisionHesitationChance;

        [Range(0.6f, 1f)]
        public float AIUnderPressureDribbleSlowdown;

        [Range(0f, 0.5f)]
        public float AIDefensiveGapChance;

        [Range(0.5f, 4f)]
        public float AIPlayerAdvantageRadius;

        [Range(0f, 0.4f)]
        public float AIPlayerInterceptionAssist;

        [Range(0f, 0.5f)]
        public float AIBadTouchChance;

        [Range(0.4f, 1.1f)]
        public float AITackleEngageDistanceScale;

        [Range(0f, 0.35f)]
        public float UserDuelControlBonus;

        [Range(0f, 0.65f)]
        public float AIDefendMissChance;

        [Range(0.5f, 0.95f)]
        public float UserTakeTapMinWinChance;

        [Range(0.05f, 0.7f)]
        public float GKReactionDelayMin;

        [Range(0.08f, 0.9f)]
        public float GKReactionDelayMax;

        [Range(0f, 0.35f)]
        public float GKMistakeChance;

        [Range(0f, 0.6f)]
        public float GKReboundChance;

        [Range(0.1f, 2.5f)]
        public float GKPositioningResponsiveness;

        [Range(0f, 0.4f)]
        public float GKWrongDiveChance;

        [Range(0f, 0.25f)]
        public float GKShotForgiveness;
    }

    [RequireComponent(typeof(MatchManagerFSM))]
    public class MatchManager : Singleton<MatchManager>
    {
        [Header("Difficulty")]
        [SerializeField]
        MatchDifficulty _difficulty = MatchDifficulty.Casual;

        [Header("Debug")]
        [SerializeField]
        bool _enableGoalkeeperDebug;

        [Header("Dynamic Difficulty (DDA)")]
        [SerializeField]
        bool _enableDynamicDifficulty = true;

        [SerializeField]
        [Range(0.2f, 8f)]
        float _ddaSmoothingSpeed = 2.25f;

        [SerializeField]
        [Range(4f, 35f)]
        float _ddaNoTouchPenaltySeconds = 13f;

        [SerializeField]
        [Range(0f, 0.6f)]
        float _ddaMaxReactionDelayBoost = 0.24f;

        [SerializeField]
        [Range(0f, 0.4f)]
        float _ddaMaxMistakeBoost = 0.18f;

        [SerializeField]
        [Range(0f, 0.4f)]
        float _ddaMaxPlayerAssistBoost = 0.20f;

        [Header("AI Attacker Spectacle")]
        [SerializeField]
        [Range(6f, 8f)]
        float mercyZoneRadius = 7f;

        [SerializeField]
        [Range(0f, 1f)]
        float longShotProbability = 0.75f;

        [SerializeField]
        [Range(0f, 1f)]
        float postHitChance = 0.22f;

        [SerializeField]
        [Range(0f, 0.8f)]
        float aiHesitationTime = 0.22f;

        [SerializeField]
        MatchDifficultyProfile _casualProfile = new MatchDifficultyProfile
        {
            PassMaxMultiplier = 1.25f,
            PassMinMultiplier = 0.78f,
            AICarrierLeadTime = 0.26f,
            AICarrierSideStepDistance = 1.35f,
            AIBehindDotThreshold = -0.35f,
            AIBehindStickBreakDistance = 1.55f,
            AIChaseSlowdownWhenBehind = 0.90f,
            AIReactionDelayMin = 0.12f,
            AIReactionDelayMax = 0.28f,
            AIErrorChanceBase = 0.09f,
            AIPressureErrorBoost = 0.17f,
            AIDecisionHesitationChance = 0.12f,
            AIUnderPressureDribbleSlowdown = 0.86f,
            AIDefensiveGapChance = 0.13f,
            AIPlayerAdvantageRadius = 2.05f,
            AIPlayerInterceptionAssist = 0.10f,
            AIBadTouchChance = 0.10f,
            AITackleEngageDistanceScale = 0.68f,
            UserDuelControlBonus = 0.14f,
            AIDefendMissChance = 0.30f,
            UserTakeTapMinWinChance = 0.85f,
            GKReactionDelayMin = 0.16f,
            GKReactionDelayMax = 0.32f,
            GKMistakeChance = 0.12f,
            GKReboundChance = 0.24f,
            GKPositioningResponsiveness = 1.00f,
            GKWrongDiveChance = 0.14f,
            GKShotForgiveness = 0.07f
        };

        [SerializeField]
        MatchDifficultyProfile _normalProfile = new MatchDifficultyProfile
        {
            PassMaxMultiplier = 1.05f,
            PassMinMultiplier = 0.92f,
            AICarrierLeadTime = 0.40f,
            AICarrierSideStepDistance = 1.00f,
            AIBehindDotThreshold = -0.30f,
            AIBehindStickBreakDistance = 1.00f,
            AIChaseSlowdownWhenBehind = 0.95f,
            AIReactionDelayMin = 0.06f,
            AIReactionDelayMax = 0.14f,
            AIErrorChanceBase = 0.04f,
            AIPressureErrorBoost = 0.08f,
            AIDecisionHesitationChance = 0.05f,
            AIUnderPressureDribbleSlowdown = 0.94f,
            AIDefensiveGapChance = 0.06f,
            AIPlayerAdvantageRadius = 1.55f,
            AIPlayerInterceptionAssist = 0.04f,
            AIBadTouchChance = 0.05f,
            AITackleEngageDistanceScale = 0.78f,
            UserDuelControlBonus = 0.10f,
            AIDefendMissChance = 0.20f,
            UserTakeTapMinWinChance = 0.80f,
            GKReactionDelayMin = 0.08f,
            GKReactionDelayMax = 0.18f,
            GKMistakeChance = 0.07f,
            GKReboundChance = 0.18f,
            GKPositioningResponsiveness = 1.28f,
            GKWrongDiveChance = 0.07f,
            GKShotForgiveness = 0.04f
        };

        [SerializeField]
        MatchDifficultyProfile _hardProfile = new MatchDifficultyProfile
        {
            PassMaxMultiplier = 1.00f,
            PassMinMultiplier = 1.00f,
            AICarrierLeadTime = 0.50f,
            AICarrierSideStepDistance = 0.85f,
            AIBehindDotThreshold = -0.20f,
            AIBehindStickBreakDistance = 0.90f,
            AIChaseSlowdownWhenBehind = 0.98f,
            AIReactionDelayMin = 0.04f,
            AIReactionDelayMax = 0.12f,
            AIErrorChanceBase = 0.02f,
            AIPressureErrorBoost = 0.06f,
            AIDecisionHesitationChance = 0.03f,
            AIUnderPressureDribbleSlowdown = 0.96f,
            AIDefensiveGapChance = 0.03f,
            AIPlayerAdvantageRadius = 1.40f,
            AIPlayerInterceptionAssist = 0.02f,
            AIBadTouchChance = 0.03f,
            AITackleEngageDistanceScale = 0.88f,
            UserDuelControlBonus = 0.06f,
            AIDefendMissChance = 0.12f,
            UserTakeTapMinWinChance = 0.75f,
            GKReactionDelayMin = 0.08f,
            GKReactionDelayMax = 0.15f,
            GKMistakeChance = 0.06f,
            GKReboundChance = 0.22f,
            GKPositioningResponsiveness = 1.45f,
            GKWrongDiveChance = 0.06f,
            GKShotForgiveness = 0.04f
        };

        [SerializeField]
        float _distancePassMax = 15f;

        [SerializeField]
        float _distancePassMin = 5f;

        [SerializeField]
        float _distanceShotValidMax = 30f;

        [SerializeField]
        float _distanceTendGoal = 3f;

        [SerializeField]
        float _distanceThreatMax = 1f;

        [SerializeField]
        float _distanceThreatMin = 5f;

        [SerializeField]
        float _distanceThreatTrack = 1f;

        [SerializeField]
        float _distanceWonderMax = 15f;

        [SerializeField]
        float _velocityPassArrive = 15f;

        [SerializeField]
        float _velocityShotArrive = 30f;

        [SerializeField]
        float _power = 30f;

        [SerializeField]
        float _speed = 3.5f;

        [SerializeField]
        Team _teamAway;

        [SerializeField]
        Team _teamHome;

        [SerializeField]
        Transform _rootTeam;

        [SerializeField]
        Transform _transformCentreSpot;

        [Header("Match Format")]
        [SerializeField]
        int _regulationDurationSeconds = 180;

        /// <summary>
        /// A reference to how long each half length is in actual time(m)
        /// </summary>
        public float ActualHalfLength { get; set; } = 3f;

        /// <summary>
        /// A reference to the normal half length
        /// </summary>
        public float NormalHalfLength { get; set; } = 45;

        /// <summary>
        /// A reference to the next time that we have to stop the game
        /// </summary>
        public float NextStopTime { get; set; }

        /// <summary>
        /// A reference to the current game half in play
        /// </summary>
        public int CurrentHalf { get; set; }

        /// <summary>
        /// True while the match is in golden-goal mode.
        /// </summary>
        public bool IsSuddenDeath { get; set; }

        /// <summary>
        /// Property to get or set this instance's fsm
        /// </summary>
        public IFSM FSM { get; set; }

        /// <summary>
        /// A reference to the match status of this instance
        /// </summary>
        public MatchStatuses MatchStatus { get; set; }

        /// <summary>
        /// Event raised when this instance is instructed to go to the second half
        /// </summary>
        public Action OnContinueToSecondHalf;

        /// <summary>
        /// Event raised when we enter the wait for kick-off state 
        /// </summary>
        public Action OnEnterWaitForKickToComplete;

        /// <summary>
        /// Event raised when we enter the wait for match on state
        /// </summary>
        public Action OnEnterWaitForMatchOnInstruction;

        /// <summary>
        /// Event raised when we exist the halftime
        /// </summary>
        public Action OnExitHalfTime;

        /// <summary>
        /// Event raised when this instance exists the match over state
        /// </summary>
        public Action OnExitMatchOver { get; set; }

        /// <summary>
        /// Event raised when we exits the wait for kick-off state 
        /// </summary>
        public Action OnExitWaitForKickToComplete;

        /// <summary>
        /// Event raised when we exist the wait for match on state
        /// </summary>
        public Action OnExitWaitForMatchOnInstruction;

        /// <summary>
        /// Event raised when this instance finishes broadcasting an event
        /// </summary>
        public Action OnFinishBroadcastHalfStart;

        /// <summary>
        /// Event raised when this instance finishes broadcasting the half-time-start event
        /// </summary>
        public Action OnFinishBroadcastHalfTimeStart;

        /// <summary>
        /// Event raised when this instance finishes broadcasting the match start event
        /// </summary>
        public Action OnFinishBroadcastMatchStart;

        /// <summary>
        /// Raised when match play starts
        /// </summary>
        public Action OnMatchPlayStart;

        /// <summary>
        /// Raised when regulation ends in a draw and the decisive round is about to start.
        /// </summary>
        public Action OnEnterSuddenDeath;

        /// <summary>
        /// Raised when match play stops
        /// </summary>
        public Action OnMatchPlayStop;

        /// <summary>
        /// Action to be raised when the match is stopped
        /// </summary>
        public Action OnStopMatch;

        /// <summary>
        /// Event raised to instruct this instance to switch to match on state
        /// </summary>
        public Action OnMesssagedToSwitchToMatchOn;

        /// <summary>
        /// Action to be raised when the kick-off needs to be taken
        /// </summary>
        public Action OnBroadcastTakeKickOff;

        /// <summary>
        /// Goal scored delegate
        /// </summary>
        /// <param name="message"></param>
        public delegate void GoalScored(string message);

        /// <summary>
        /// Match end delegate
        /// </summary>
        /// <param name="message"></param>
        public delegate void BroadcastHalfStart(string message);

        /// <summary>
        /// Half time start delegate
        /// </summary>
        /// <param name="message"></param>
        public delegate void BroadcastHalfTimeStart(string message);

        /// <summary>
        /// Half time start delegate
        /// </summary>
        /// <param name="message"></param>
        public delegate void EnterHalfTime(string message);

        /// <summary>
        /// Match start delegate
        /// </summary>
        /// <param name="message"></param>
        public delegate void BroadcastMatchStart(string message);

        /// <summary>
        /// Match end delegate
        /// </summary>
        /// <param name="message"></param>
        public delegate void MatchOver(string message);

        /// <summary>
        /// Tick delegate
        /// </summary>
        /// <param name="half">the current half</param>
        /// <param name="minutes">the current minutes</param>
        /// <param name="seconds">the currenct seconds</param>
        public delegate void Tick(int half, int minutes, int seconds);
        public delegate void PostHit(Vector3 worldPoint);

        /// <summary>
        /// Event that is raised when a goal is scored
        /// </summary>
        public GoalScored OnGoalScored;

        /// <summary>
        /// Event raised when the half starts
        /// </summary>
        public BroadcastHalfStart OnBroadcastHalfStart;

        /// <summary>
        /// Event raised when the half time starts
        /// </summary>
        public BroadcastHalfTimeStart OnBroadcastHalfTimeStart;

        /// <summary>
        /// Event raised when we enter half time
        /// </summary>
        public EnterHalfTime OnEnterHalfTime;

        /// <summary>
        /// Event to be raised when a match ends
        /// </summary>
        public MatchOver OnMatchOver;

        /// <summary>
        /// Event to be raised when a broadcast of match starts is started
        /// </summary>
        public BroadcastMatchStart OnBroadcastMatchStart;

        /// <summary>
        /// The OnTick event
        /// </summary>
        public Tick OnTick;

        public PostHit OnPostHit;

        float _userNoTouchTimer;
        float _ddaStrength;
        float _playerPerformanceScore = 0.5f;
        bool _wasUserTeamInPossession;
        int _userSuccessfulPasses;
        int _userBallLosses;
        int _userShotsOnTarget;
        int _consecutiveUserMistakes;
        int _aiDangerousShotCounter;
        int _nextAIPostShotTrigger = 4;

        public override void Awake()
        {
            base.Awake();

            FSM = GetComponent<MatchManagerFSM>();
            _nextAIPostShotTrigger = UnityEngine.Random.Range(4, 6);
        }

        private void OnValidate()
        {
            ClampDifficultyProfile(ref _casualProfile);
            ClampDifficultyProfile(ref _normalProfile);
            ClampDifficultyProfile(ref _hardProfile);

            if (Application.isPlaying)
                ApplyDifficultyToActiveTeams();
        }

        void ClampDifficultyProfile(ref MatchDifficultyProfile profile)
        {
            profile.PassMaxMultiplier = Mathf.Max(0.1f, profile.PassMaxMultiplier);
            profile.PassMinMultiplier = Mathf.Max(0.1f, profile.PassMinMultiplier);
            profile.AICarrierLeadTime = Mathf.Max(0.01f, profile.AICarrierLeadTime);
            profile.AICarrierSideStepDistance = Mathf.Max(0.1f, profile.AICarrierSideStepDistance);
            profile.AIBehindDotThreshold = Mathf.Clamp(profile.AIBehindDotThreshold, -1f, 0f);
            profile.AIBehindStickBreakDistance = Mathf.Max(0.1f, profile.AIBehindStickBreakDistance);
            profile.AIChaseSlowdownWhenBehind = Mathf.Clamp(profile.AIChaseSlowdownWhenBehind, 0.6f, 1f);
            profile.AIReactionDelayMin = Mathf.Clamp(profile.AIReactionDelayMin, 0f, 0.6f);
            profile.AIReactionDelayMax = Mathf.Clamp(profile.AIReactionDelayMax, 0f, 0.8f);
            if (profile.AIReactionDelayMax < profile.AIReactionDelayMin)
                profile.AIReactionDelayMax = profile.AIReactionDelayMin;

            profile.AIErrorChanceBase = Mathf.Clamp01(profile.AIErrorChanceBase);
            profile.AIPressureErrorBoost = Mathf.Clamp01(profile.AIPressureErrorBoost);
            profile.AIDecisionHesitationChance = Mathf.Clamp01(profile.AIDecisionHesitationChance);
            profile.AIUnderPressureDribbleSlowdown = Mathf.Clamp(profile.AIUnderPressureDribbleSlowdown, 0.6f, 1f);
            profile.AIDefensiveGapChance = Mathf.Clamp(profile.AIDefensiveGapChance, 0f, 0.5f);
            profile.AIPlayerAdvantageRadius = Mathf.Clamp(profile.AIPlayerAdvantageRadius, 0.5f, 4f);
            profile.AIPlayerInterceptionAssist = Mathf.Clamp(profile.AIPlayerInterceptionAssist, 0f, 0.4f);
            profile.AIBadTouchChance = Mathf.Clamp(profile.AIBadTouchChance, 0f, 0.5f);
            profile.AITackleEngageDistanceScale = Mathf.Clamp(profile.AITackleEngageDistanceScale, 0.4f, 1.1f);
            profile.UserDuelControlBonus = Mathf.Clamp(profile.UserDuelControlBonus, 0f, 0.35f);
            profile.AIDefendMissChance = Mathf.Clamp(profile.AIDefendMissChance, 0f, 0.65f);
            profile.UserTakeTapMinWinChance = Mathf.Clamp(profile.UserTakeTapMinWinChance, 0.5f, 0.95f);
            profile.GKReactionDelayMin = Mathf.Clamp(profile.GKReactionDelayMin, 0.05f, 0.7f);
            profile.GKReactionDelayMax = Mathf.Clamp(profile.GKReactionDelayMax, 0.08f, 0.9f);
            if (profile.GKReactionDelayMax < profile.GKReactionDelayMin)
                profile.GKReactionDelayMax = profile.GKReactionDelayMin;

            profile.GKMistakeChance = Mathf.Clamp(profile.GKMistakeChance, 0f, 0.35f);
            profile.GKReboundChance = Mathf.Clamp(profile.GKReboundChance, 0f, 0.6f);
            profile.GKPositioningResponsiveness = Mathf.Clamp(profile.GKPositioningResponsiveness, 0.1f, 2.5f);
            profile.GKWrongDiveChance = Mathf.Clamp(profile.GKWrongDiveChance, 0f, 0.4f);
            profile.GKShotForgiveness = Mathf.Clamp(profile.GKShotForgiveness, 0f, 0.25f);
        }

        void ApplyDifficultyToActiveTeams()
        {
            float passMax = DifficultyDistancePassMax;
            float passMin = DifficultyDistancePassMin;

            ApplyDifficultyToTeam(_teamAway, passMax, passMin);
            ApplyDifficultyToTeam(_teamHome, passMax, passMin);
        }

        void ApplyDifficultyToTeam(Team team, float passMax, float passMin)
        {
            if (team == null)
                return;

            team.DistancePassMax = passMax;
            team.DistancePassMin = passMin;

            if (team.Players == null)
                return;

            foreach (TeamPlayer teamPlayer in team.Players)
            {
                if (teamPlayer == null || teamPlayer.Player == null)
                    continue;

                teamPlayer.Player.DistancePassMax = passMax;
                teamPlayer.Player.DistancePassMin = passMin;
            }
        }

        public MatchDifficultyProfile CurrentDifficultyProfile
        {
            get
            {
                if (_difficulty == MatchDifficulty.Hard)
                    return _hardProfile;

                if (_difficulty == MatchDifficulty.Normal)
                    return _normalProfile;

                return _casualProfile;
            }
        }

        public float DifficultyDistancePassMax
        {
            get
            {
                MatchDifficultyProfile profile = CurrentDifficultyProfile;
                return _distancePassMax * profile.PassMaxMultiplier;
            }
        }

        public float DifficultyDistancePassMin
        {
            get
            {
                MatchDifficultyProfile profile = CurrentDifficultyProfile;
                return _distancePassMin * profile.PassMinMultiplier;
            }
        }

        public MatchDifficultyProfile RuntimeDifficultyProfile
        {
            get
            {
                MatchDifficultyProfile profile = CurrentDifficultyProfile;
                if (!_enableDynamicDifficulty)
                    return profile;

                float assist = Mathf.Clamp01(_ddaStrength);
                assist = Mathf.Min(assist, GetDifficultyAssistCap());
                float challenge = 1f - assist;

                profile.AIReactionDelayMin += Mathf.Lerp(-0.02f, _ddaMaxReactionDelayBoost, assist);
                profile.AIReactionDelayMax += Mathf.Lerp(-0.02f, _ddaMaxReactionDelayBoost + 0.08f, assist);

                profile.AIErrorChanceBase += Mathf.Lerp(-0.02f, _ddaMaxMistakeBoost, assist);
                profile.AIPressureErrorBoost += Mathf.Lerp(-0.03f, _ddaMaxMistakeBoost, assist);
                profile.AIDecisionHesitationChance += Mathf.Lerp(-0.02f, _ddaMaxMistakeBoost * 0.8f, assist);
                profile.AIBadTouchChance += assist * (_ddaMaxMistakeBoost * 0.85f);
                profile.AIDefensiveGapChance += assist * (_ddaMaxMistakeBoost * 0.75f);

                profile.AIUnderPressureDribbleSlowdown -= assist * 0.12f;
                profile.AIChaseSlowdownWhenBehind -= assist * 0.08f;
                profile.AIPlayerInterceptionAssist += assist * _ddaMaxPlayerAssistBoost;
                profile.AIPlayerAdvantageRadius += assist * 0.8f;
                profile.AITackleEngageDistanceScale -= assist * 0.18f;
                profile.UserDuelControlBonus += assist * 0.12f;
                profile.AIDefendMissChance += assist * 0.20f;
                profile.UserTakeTapMinWinChance += assist * 0.08f;

                // Goalkeeper auto-balance: help struggling players score, tighten subtly when dominating.
                profile.GKReactionDelayMin += assist * 0.08f;
                profile.GKReactionDelayMax += assist * 0.12f;
                profile.GKMistakeChance += assist * 0.08f;
                profile.GKReboundChance += assist * 0.10f;
                profile.GKWrongDiveChance += assist * 0.06f;
                profile.GKShotForgiveness += assist * 0.08f;
                profile.GKPositioningResponsiveness -= assist * 0.20f;

                profile.GKMistakeChance -= challenge * 0.03f;
                profile.GKReboundChance -= challenge * 0.03f;
                profile.GKWrongDiveChance -= challenge * 0.02f;
                profile.GKPositioningResponsiveness += challenge * 0.10f;

                // If player dominates, tighten AI slightly but keep behavior fair.
                profile.AIErrorChanceBase -= challenge * 0.01f;
                profile.AIPressureErrorBoost -= challenge * 0.015f;
                profile.AITackleEngageDistanceScale += challenge * 0.05f;
                profile.UserDuelControlBonus -= challenge * 0.03f;
                profile.AIDefendMissChance -= challenge * 0.04f;
                profile.UserTakeTapMinWinChance -= challenge * 0.04f;

                ClampDifficultyProfile(ref profile);
                return profile;
            }
        }

        public float PlayerPerformanceScore => Mathf.Clamp01(_playerPerformanceScore);
        public float DdaAssistStrength => Mathf.Clamp01(_ddaStrength);
        public float MercyZoneRadius => Mathf.Clamp(mercyZoneRadius, 6f, 8f);
        public float LongShotProbability => Mathf.Clamp01(longShotProbability);
        public float PostHitChance => Mathf.Clamp01(postHitChance);
        public float AiHesitationTime => Mathf.Clamp(aiHesitationTime, 0f, 0.8f);

        public void NotifyUserSuccessfulPass()
        {
            _userSuccessfulPasses++;
            _consecutiveUserMistakes = Mathf.Max(0, _consecutiveUserMistakes - 1);
        }

        public void NotifyUserShotOnTarget()
        {
            _userShotsOnTarget++;
            _consecutiveUserMistakes = Mathf.Max(0, _consecutiveUserMistakes - 1);
        }

        public void NotifyUserBallLoss()
        {
            _userBallLosses++;
            _consecutiveUserMistakes++;
        }

        public void ResetDynamicDifficultySession()
        {
            _userNoTouchTimer = 0f;
            _ddaStrength = 0f;
            _playerPerformanceScore = 0.5f;
            _wasUserTeamInPossession = false;
            _userSuccessfulPasses = 0;
            _userBallLosses = 0;
            _userShotsOnTarget = 0;
            _consecutiveUserMistakes = 0;
            _aiDangerousShotCounter = 0;
            _nextAIPostShotTrigger = UnityEngine.Random.Range(4, 6);
        }

        public bool RegisterAIDangerousShotAndCheckPostTrigger()
        {
            _aiDangerousShotCounter++;

            bool thresholdTrigger = _aiDangerousShotCounter >= _nextAIPostShotTrigger;
            if (thresholdTrigger)
            {
                _aiDangerousShotCounter = 0;
                _nextAIPostShotTrigger = UnityEngine.Random.Range(4, 6);
                return true;
            }

            return UnityEngine.Random.value <= PostHitChance;
        }

        public void NotifyAIPostHit(Vector3 worldPoint)
        {
            PostHit temp = OnPostHit;
            if (temp != null)
                temp.Invoke(worldPoint);
        }

        void UpdateDynamicDifficulty(float deltaTime)
        {
            if (!_enableDynamicDifficulty || deltaTime <= 0f)
                return;

            Team userTeam = UserControlledTeam;
            if (userTeam == null)
                return;

            bool userTeamInPossession = Ball.Instance != null
                && Ball.Instance.Owner != null
                && Ball.Instance.Owner.IsTeamInControl == userTeam.IsUserControlled;

            bool aiTeamInPossession = Ball.Instance != null
                && Ball.Instance.Owner != null
                && !userTeamInPossession;

            if (userTeamInPossession)
                _userNoTouchTimer = 0f;
            else
                _userNoTouchTimer += deltaTime;

            if (_wasUserTeamInPossession && aiTeamInPossession)
                NotifyUserBallLoss();

            _wasUserTeamInPossession = userTeamInPossession;

            float passScore = Mathf.Clamp01(_userSuccessfulPasses / 10f);
            float shotsScore = Mathf.Clamp01(_userShotsOnTarget / 4f);
            float ballLossPenalty = Mathf.Clamp01(_userBallLosses / 7f);
            float noTouchPenalty = Mathf.Clamp01(_userNoTouchTimer / Mathf.Max(1f, _ddaNoTouchPenaltySeconds));
            float mistakesPenalty = Mathf.Clamp01(_consecutiveUserMistakes / 5f);

            int userGoals = userTeam.Goals;
            int aiGoals = userTeam == TeamAway ? TeamHome.Goals : TeamAway.Goals;
            float scoreDiffNormalized = Mathf.Clamp((userGoals - aiGoals) / 3f, -1f, 1f);
            float scoreDiffScore = (scoreDiffNormalized + 1f) * 0.5f;

            float targetPerformance =
                (passScore * 0.26f)
                + (shotsScore * 0.24f)
                + (scoreDiffScore * 0.28f)
                + ((1f - ballLossPenalty) * 0.10f)
                + ((1f - noTouchPenalty) * 0.06f)
                + ((1f - mistakesPenalty) * 0.06f);

            targetPerformance = Mathf.Clamp01(targetPerformance);

            float lerpT = 1f - Mathf.Exp(-Mathf.Max(0.2f, _ddaSmoothingSpeed) * deltaTime);
            _playerPerformanceScore = Mathf.Lerp(_playerPerformanceScore, targetPerformance, lerpT);

            // Low performance => more assist. High performance => less assist.
            float targetAssist = 1f - _playerPerformanceScore;
            targetAssist = Mathf.Min(targetAssist, GetDifficultyAssistCap());
            _ddaStrength = Mathf.Lerp(_ddaStrength, targetAssist, lerpT);
        }

        float GetDifficultyAssistCap()
        {
            if (_difficulty == MatchDifficulty.Hard)
                return 0.25f;

            if (_difficulty == MatchDifficulty.Normal)
                return 0.45f;

            return 0.65f;
        }

        Team UserControlledTeam
        {
            get
            {
                if (_teamAway != null && _teamAway.IsUserControlled)
                    return _teamAway;

                if (_teamHome != null && _teamHome.IsUserControlled)
                    return _teamHome;

                return null;
            }
        }

        private void Update()
        {
            UpdateDynamicDifficulty(Time.deltaTime);

#if UNITY_EDITOR
            // Editor-only debug shortcut for quickly toggling possession.
            if (Input.GetKeyDown(KeyCode.P))
            {
                if(TeamAway.FSM.IsCurrentState<AttackMainState>())
                {
                    ActionUtility.Invoke_Action(TeamHome.OnGainPossession);
                }
                else if (TeamHome.FSM.IsCurrentState<AttackMainState>())
                {
                    ActionUtility.Invoke_Action(TeamAway.OnGainPossession);
                }
            }
#endif
        }
        public void Instance_OnContinueToSecondHalf()
        {
            ActionUtility.Invoke_Action(OnContinueToSecondHalf);
        }

        /// <summary>
        /// Raises the event that this instance has been messaged to switch to match on
        /// </summary>
        public void Instance_OnMessagedSwitchToMatchOn()
        {
            ActionUtility.Invoke_Action(OnMesssagedToSwitchToMatchOn);
        }

        public float DistanceThreatMin
        {
            get => _distanceThreatMin;
            set => _distanceThreatMin = value;
        }

        public float DistanceThreatMax
        {
            get => _distanceThreatMax;
            set => _distanceThreatMax = value;
        }

        public float Power { get => _power; }
        public float Speed { get => _speed; }

        public Team TeamAway { get => _teamAway; }
        public Team TeamHome { get => _teamHome; }
        public int RegulationDurationSeconds
        {
            get => Mathf.Max(1, _regulationDurationSeconds);
            set => _regulationDurationSeconds = Mathf.Max(1, value);
        }

        public bool IsScoreDraw
        {
            get
            {
                if (_teamAway == null || _teamHome == null)
                    return true;

                return _teamAway.Goals == _teamHome.Goals;
            }
        }

        /// <summary>
        /// Property to access the team root transform
        /// </summary>
        public Transform RootTeam { get => _rootTeam; }
        public float DistancePassMax { get => _distancePassMax; set => _distancePassMax = value; }
        public float DistancePassMin { get => _distancePassMin; set => _distancePassMin = value; }
        public MatchDifficulty Difficulty { get => _difficulty; set => _difficulty = value; }
        public bool EnableGoalkeeperDebug { get => _enableGoalkeeperDebug; set => _enableGoalkeeperDebug = value; }
        public Transform TransformCentreSpot { get => _transformCentreSpot; set => _transformCentreSpot = value; }
        public float DistanceWonderMax { get => _distanceWonderMax; set => _distanceWonderMax = value; }
        public float DistanceShotValidMax { get => _distanceShotValidMax; set => _distanceShotValidMax = value; }
        public float VelocityPassArrive { get => _velocityPassArrive; set => _velocityPassArrive = value; }
        public float VelocityShotArrive { get => _velocityShotArrive; set => _velocityShotArrive = value; }
        public float DistanceTendGoal { get => _distanceTendGoal; set => _distanceTendGoal = value; }
        public float DistanceThreatTrack { get => _distanceThreatTrack; set => _distanceThreatTrack = value; }
    }
}
