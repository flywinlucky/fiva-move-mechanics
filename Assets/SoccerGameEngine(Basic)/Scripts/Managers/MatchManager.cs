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
            AIChaseSlowdownWhenBehind = 0.87f
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
            AIChaseSlowdownWhenBehind = 0.93f
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
            AIChaseSlowdownWhenBehind = 0.98f
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
