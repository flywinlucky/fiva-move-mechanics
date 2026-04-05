using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.Tackled.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.Attack.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.Defend.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using RobustFSM.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Entities
{
    [RequireComponent(typeof(TeamFSM))]
    public class Team : MonoBehaviour
    {
        [Header("Control Variables")]

        [SerializeField]
        bool _isUserControlled;

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

        [Header("Closest Player Switch")]
        [SerializeField]
        [Range(0f, 2f)]
        float _closestPlayerSwitchCooldown = 1f;

        [SerializeField]
        [Range(0f, 2f)]
        float _closestPlayerSwitchMinDistanceGain = 0.35f;

        [SerializeField]
        [Range(0f, 1f)]
        float _closestPlayerTieEpsilon = 0.12f;

        [Header("Entities")]
        [SerializeField]
        Formation _formation;

        [SerializeField]
        Goal _goal;

        [SerializeField]
        Team _opponent;

        [SerializeField]
        Transform _kickOffRefDirection;

        [SerializeField]
        Transform _playerSupprtSpots;

        [SerializeField]
        Transform _rootPlayers;

        public bool IsUserControlled { get => _isUserControlled; }
        public bool IsManualControlEnabled { get; private set; } = true;

        public bool HasInitialKickOff { get; set; }

        public bool HasKickOff { get; set; }

        public int Goals { get; set; }

        public IFSM FSM { get; set; }

        Player _controllingPlayer;
        public Player ControllingPlayer
        {
            get => _controllingPlayer;
            set
            {
                _controllingPlayer = IsTeamMember(value) ? value : null;
                ApplySingleUserControlSelection(_controllingPlayer);
            }
        }
        public List<TeamPlayer> Players = new List<TeamPlayer>();

        public Action OnGainPossession;
        public Action OnLostPossession;
        public Action OnInit;
        public Action OnOppFinishedInit;
        public Action OnMessagedToStop;
        public Action OnInstructPlayersToWait;
        public Action OnTakeKickOff;
        public Action OnMessagedToTakeKickOff;

        float _nextClosestPlayerSwitchTime;

        public delegate void BallLaunched(float flightTime, float velocity, Vector3 initial, Vector3 target);

        public BallLaunched OnBallLaunched;

        private void Awake()
        {
            //initialize some variables
            FSM = GetComponent<TeamFSM>();

            //set-up some variables
            HasKickOff = HasInitialKickOff;
            _nextClosestPlayerSwitchTime = 0f;
        }

        public void Init(float distancePassMax, 
            float distancePassMin, 
            float distanceShotValidMax,
            float distanceTendGoal,
            float distanceThreatMax, 
            float distanceThreatMin, 
            float distanceThreatTrack,
            float distanceWonderMax,
            float velocityPassArrive,
            float velocityShotArrive,
            float power, 
            float speed)
        {
            _distancePassMax = distancePassMax;
            _distancePassMin = distancePassMin;
            _distanceShotValidMax = distanceShotValidMax;
            _distanceTendGoal = distanceTendGoal;
            _distanceThreatTrack = distanceThreatTrack;
            _distanceWonderMax = distanceWonderMax;
            _velocityPassArrive = velocityPassArrive;
            _velocityShotArrive = velocityShotArrive;
            _distanceThreatMax = distanceThreatMax;
            _distanceThreatMin = distanceThreatMin;
            _speed = speed;
            _power = power;
        }

        public void Invoke_OnBallLaunched(float flightTime, float velocity, Vector3 initial, Vector3 target)
        {
            BallLaunched temp = OnBallLaunched;
            if (temp != null)
                temp.Invoke(flightTime,
                    velocity,
                    initial,
                    target);
        }

        public void Invoke_OnOppFinishedInit()
        {
            ActionUtility.Invoke_Action(OnOppFinishedInit);
        }

        /// <summary>
        /// Invokes the OnStop action of this instance. Register this method to any event
        /// that the team needs to be aware of for it to go to prepare-for-kick-off state
        /// </summary>
        public void Invoke_OnMessagedToTakeKickOff()
        {
            ActionUtility.Invoke_Action(OnMessagedToTakeKickOff);
        }

        /// <summary>
        /// Invokes the OnStop action of this instance. Register this method to any event
        /// that the team needs to be aware of for it to go to wait state
        /// </summary>
        public void Invoke_OnMessagedToStop()
        {
            ActionUtility.Invoke_Action(OnMessagedToStop);
        }

        public void Invoke_OnLostPossession()
        {
            ActionUtility.Invoke_Action(OnLostPossession);
        }

        public void Invoke_OnGainPossession(Player player)
        {
            // set the controlling player
            ControllingPlayer = player;

            // raise the event that I have gained possession
            ActionUtility.Invoke_Action(OnGainPossession);
        }

        public void OnOppScoredAGoal()
        {
            // set has kick-off
            HasKickOff = true;
        }

        public void OnTeamScoredAGoal()
        {
            // unset has kick-off
            HasKickOff = false;

            // increment number of goals scored
            ++Goals;
        }

        public TeamPlayer GetClosestPlayerToPoint(Vector3 position)
        {
            if (Players == null || Players.Count == 0)
                return null;

            TeamPlayer bestPreferred = null;
            float bestPreferredSqr = float.MaxValue;
            int bestPreferredId = int.MaxValue;

            TeamPlayer bestInField = null;
            float bestInFieldSqr = float.MaxValue;
            int bestInFieldId = int.MaxValue;

            TeamPlayer bestAny = null;
            float bestAnySqr = float.MaxValue;
            int bestAnyId = int.MaxValue;

            for (int i = 0; i < Players.Count; i++)
            {
                TeamPlayer tm = Players[i];
                if (tm == null || tm.Player == null)
                    continue;

                Player player = tm.Player;
                float sqrDistance = (player.Position - position).sqrMagnitude;
                int instanceId = player.GetInstanceID();

                if (IsBetterClosestCandidate(sqrDistance, instanceId, bestAnySqr, bestAnyId))
                {
                    bestAny = tm;
                    bestAnySqr = sqrDistance;
                    bestAnyId = instanceId;
                }

                if (player.PlayerType != PlayerTypes.InFieldPlayer)
                    continue;

                if (IsBetterClosestCandidate(sqrDistance, instanceId, bestInFieldSqr, bestInFieldId))
                {
                    bestInField = tm;
                    bestInFieldSqr = sqrDistance;
                    bestInFieldId = instanceId;
                }

                bool isTackled = player.InFieldPlayerFSM != null
                    && player.InFieldPlayerFSM.CurrentState != null
                    && player.InFieldPlayerFSM.IsCurrentState<TackledMainState>();

                if (!isTackled && IsBetterClosestCandidate(sqrDistance, instanceId, bestPreferredSqr, bestPreferredId))
                {
                    bestPreferred = tm;
                    bestPreferredSqr = sqrDistance;
                    bestPreferredId = instanceId;
                }
            }

            if (bestPreferred != null)
                return bestPreferred;

            if (bestInField != null)
                return bestInField;

            return bestAny;
        }

        bool IsBetterClosestCandidate(float candidateSqr, int candidateId, float bestSqr, int bestId)
        {
            if (candidateSqr < bestSqr)
                return true;

            if (Mathf.Approximately(candidateSqr, bestSqr))
                return candidateId < bestId;

            return false;
        }

        public TeamPlayer GetTeamPlayer(Player player)
        {
            if (player == null)
                return null;

            if (Players == null)
                return null;

            for (int i = 0; i < Players.Count; i++)
            {
                TeamPlayer teamPlayer = Players[i];
                if (teamPlayer != null && teamPlayer.Player == player)
                    return teamPlayer;
            }

            return null;
        }

        public bool ShouldSwitchClosestPlayer(TeamPlayer current, TeamPlayer candidate, Vector3 position)
        {
            if (candidate == null || candidate.Player == null)
                return false;

            if (current == null || current.Player == null)
                return true;

            if (current == candidate)
                return false;

            float currentSqr = (current.Player.Position - position).sqrMagnitude;
            float candidateSqr = (candidate.Player.Position - position).sqrMagnitude;

            // Only switch if the new candidate is meaningfully closer.
            float requiredGain = Mathf.Max(0f, _closestPlayerSwitchMinDistanceGain);
            float requiredGainSqr = requiredGain * requiredGain;

            if (candidateSqr + requiredGainSqr < currentSqr)
                return true;

            // For near ties, keep current to avoid flickering control.
            float tieEpsilon = Mathf.Max(0f, _closestPlayerTieEpsilon);
            float tieEpsilonSqr = tieEpsilon * tieEpsilon;
            if (Mathf.Abs(candidateSqr - currentSqr) <= tieEpsilonSqr)
                return false;

            return candidateSqr < currentSqr;
        }

        public float ClosestPlayerSwitchCooldownSeconds => Mathf.Max(0f, _closestPlayerSwitchCooldown);

        public Formation Formation
        {
            get
            {
                return _formation;
            }
        }

        public Goal Goal
        {
            get
            {
                return _goal;
            }
        }

        public Team Opponent
        {
            get
            {
                return _opponent;
            }
        }

        public Transform RootPlayers
        {
            get
            {
                return _rootPlayers;
            }
        }

        public Transform KickOffRefDirection { get => _kickOffRefDirection; set => _kickOffRefDirection = value; }
        public Transform PlayerSupportSpots { get => _playerSupprtSpots; set => _playerSupprtSpots = value; }

        public float DistancePassMax { get => _distancePassMax; set => _distancePassMax = value; }
        public float DistancePassMin { get => _distancePassMin; set => _distancePassMin = value; }
        public float Power { get => _power; set => _power = value; }
        public float Speed { get => _speed; set => _speed = value; }

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
        public float DistanceShotValidMax { get => _distanceShotValidMax; set => _distanceShotValidMax = value; }
        public float DistanceTendGoal { get => _distanceTendGoal; set => _distanceTendGoal = value; }
        public float DistanceThreatTrack { get => _distanceThreatTrack; set => _distanceThreatTrack = value; }
        public float DistanceWonderMax { get => _distanceWonderMax; set => _distanceWonderMax = value; }
        public float VelocityPassArrive { get => _velocityPassArrive; set => _velocityPassArrive = value; }
        public float VelocityShotArrive { get => _velocityShotArrive; set => _velocityShotArrive = value; }

        public void Invoke_OnPlayerChaseBall(Player chasingPlayer)
        {
            if (chasingPlayer == null)
                return;

            Player ballOwner = Ball.Instance.Owner;
            bool isTeamMateInPossession = ballOwner != null
                && ballOwner.IsTeamInControl == chasingPlayer.IsTeamInControl;
            if (isTeamMateInPossession)
            {
                chasingPlayer.Invoke_OnIsNoLongerTheClosestPlayerToBall();
                _nextClosestPlayerSwitchTime = 0f;
                return;
            }

            // get the current closest player to ball
            TeamPlayer currClosestPlayerToPoint = GetClosestPlayerToPoint(Ball.Instance.NormalizedPosition);

            if (currClosestPlayerToPoint == null || currClosestPlayerToPoint.Player == null)
                return;

            if(chasingPlayer != currClosestPlayerToPoint.Player)
            {
                TeamPlayer currentChaser = GetTeamPlayer(chasingPlayer);
                bool shouldSwitch = ShouldSwitchClosestPlayer(currentChaser,
                    currClosestPlayerToPoint,
                    Ball.Instance.NormalizedPosition);

                if (!shouldSwitch)
                    return;

                if (Time.time < _nextClosestPlayerSwitchTime)
                    return;

                // message the previous chaser to stop chasing
                chasingPlayer.Invoke_OnIsNoLongerTheClosestPlayerToBall();

                // make the current closet player chase the ball
                ControllingPlayer = currClosestPlayerToPoint.Player;
                currClosestPlayerToPoint.Player.Invoke_OnBecameTheClosestPlayerToBall();
                _nextClosestPlayerSwitchTime = Time.time + ClosestPlayerSwitchCooldownSeconds;

            }
        }

        bool IsTeamMember(Player player)
        {
            if (player == null || Players == null)
                return false;

            for (int i = 0; i < Players.Count; i++)
            {
                TeamPlayer teamPlayer = Players[i];
                if (teamPlayer != null && teamPlayer.Player == player)
                    return true;
            }

            return false;
        }

        void ApplySingleUserControlSelection(Player selected)
        {
            if (Players == null)
                return;

            bool hasSelected = selected != null;

            foreach (TeamPlayer teamPlayer in Players)
            {
                if (teamPlayer == null || teamPlayer.Player == null)
                    continue;

                bool shouldBeUserControlled = _isUserControlled
                    && IsManualControlEnabled
                    && hasSelected
                    && teamPlayer.Player == selected;
                teamPlayer.Player.IsUserControlled = shouldBeUserControlled;
            }
        }

        public void SetManualControlEnabled(bool enabled)
        {
            if (IsManualControlEnabled == enabled)
                return;

            IsManualControlEnabled = enabled;
            ApplySingleUserControlSelection(_controllingPlayer);
        }
    }

    [Serializable]
    public class TeamPlayer
    {
        public Player Player;

        public Transform AttackingHomePosition;

        public Transform DefendingHomePosition;

        public Transform CurrentHomePosition;

        public Transform KickOffHomePosition;

        public TeamPlayer(Player player, 
            Team team, 
            Transform attackingHomePosition, 
            Transform defendingHomePosition, 
            Transform currentPlayerHomePosition, 
            Transform kickOffHomePosition,
            float distancePassMax,
            float distancePassMin,
            float distanceShotValidMax,
            float distanceTendGoal,
            float distanceThreatMax,
            float distanceThreatMin,
            float distanceThreatTrack,
            float distanceWonderMax,
            float velocityPassArrive,
            float velocityShotArrive,
            float power,
            float speed)
        {
            //init player details
            Player = player;

            Player.Init(distancePassMax,
                distancePassMin,
                distanceShotValidMax,
                distanceTendGoal,
                distanceThreatMax,
                distanceThreatMin,
                distanceThreatTrack,
                distanceWonderMax,
                velocityPassArrive,
                velocityShotArrive,
                power, 
                speed);

            AttackingHomePosition = attackingHomePosition;
            DefendingHomePosition = defendingHomePosition;
            CurrentHomePosition = currentPlayerHomePosition;
            KickOffHomePosition = kickOffHomePosition;

            //set the initial home position of the player
            Player.HomeRegion = CurrentHomePosition;
        }
    }
}
