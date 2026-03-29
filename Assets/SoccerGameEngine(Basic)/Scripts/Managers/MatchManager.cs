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

        [SerializeField]
        MatchDifficultyProfile _casualProfile = new MatchDifficultyProfile
        {
            PassMaxMultiplier = 1.35f,
            PassMinMultiplier = 0.70f,
            AICarrierLeadTime = 0.20f,
            AICarrierSideStepDistance = 1.50f,
            AIBehindDotThreshold = -0.35f,
            AIBehindStickBreakDistance = 1.80f,
            AIChaseSlowdownWhenBehind = 0.84f,
            AIReactionDelayMin = 0.18f,
            AIReactionDelayMax = 0.40f,
            AIErrorChanceBase = 0.12f,
            AIPressureErrorBoost = 0.22f,
            AIDecisionHesitationChance = 0.18f,
            AIUnderPressureDribbleSlowdown = 0.82f,
            AIDefensiveGapChance = 0.20f,
            AIPlayerAdvantageRadius = 2.30f,
            AIPlayerInterceptionAssist = 0.18f,
            AIBadTouchChance = 0.14f
        };

        [SerializeField]
        MatchDifficultyProfile _normalProfile = new MatchDifficultyProfile
        {
            PassMaxMultiplier = 1.15f,
            PassMinMultiplier = 0.85f,
            AICarrierLeadTime = 0.32f,
            AICarrierSideStepDistance = 1.15f,
            AIBehindDotThreshold = -0.30f,
            AIBehindStickBreakDistance = 1.25f,
            AIChaseSlowdownWhenBehind = 0.91f,
            AIReactionDelayMin = 0.10f,
            AIReactionDelayMax = 0.22f,
            AIErrorChanceBase = 0.06f,
            AIPressureErrorBoost = 0.12f,
            AIDecisionHesitationChance = 0.08f,
            AIUnderPressureDribbleSlowdown = 0.90f,
            AIDefensiveGapChance = 0.10f,
            AIPlayerAdvantageRadius = 1.80f,
            AIPlayerInterceptionAssist = 0.08f,
            AIBadTouchChance = 0.08f
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
            AIBadTouchChance = 0.03f
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

        public override void Awake()
        {
            base.Awake();

            FSM = GetComponent<MatchManagerFSM>();
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

        private void Update()
        {
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
