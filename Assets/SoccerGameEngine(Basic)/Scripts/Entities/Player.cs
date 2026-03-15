using Assets.SimpleSteering.Scripts.Movement;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using RobustFSM.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Entities
{
    //[RequireComponent(typeof(InFieldPlayerFSM))]
    [RequireComponent(typeof(RPGMovement))]
    [RequireComponent(typeof(SupportSpot))]
    public class Player : MonoBehaviour
    {
        [Header("Control Variables")]

        [SerializeField]
        bool _isUserControlled;

        [SerializeField]
        float _ballControlDistance = 0.5f;

        [SerializeField]
        float _maxWanderDistance = 10f;

        [SerializeField]
        float _distancePassMax = 15f;

        [SerializeField]
        float _distancePassMin = 5f;

        [SerializeField]
        float _distanceShotMaxValid = 20f;

        [SerializeField]
        float _distanceThreatMax = 0.5f;

        [SerializeField]
        float _distanceThreatMin = 1f;

        [SerializeField]
        float _ballPassArriveVelocity = 5f;

        [SerializeField]
        float _ballShotArriveVelocity = 10f;

        //[SerializeField]
        //[Range(1f, 3f)]
        float _shotPowerMultiplier = 1.5f;

        //[SerializeField]
        //[Range(1f, 3f)]
        float _goalKeeperPassPowerMultiplier = 1.25f;

        [Header("Sprint & Stamina")]

        [SerializeField]
        [Range(1f, 3f)]
        float _sprintSpeedMultiplier = 0.7f;

        [SerializeField]
        [Range(10f, 200f)]
        float _maxStamina = 100f;

        [SerializeField]
        [Range(1f, 100f)]
        float _staminaDrainPerSecond = 22f;

        [SerializeField]
        [Range(1f, 100f)]
        float _staminaRegenPerSecond = 14f;

        [SerializeField]
        [Range(0f, 5f)]
        float _staminaRegenDelay = 0.7f;

        [SerializeField]
        [Range(0f, 1f)]
        float _aiSprintChance = 0.2f;

        [SerializeField]
        [Range(0f, 1f)]
        float _aiSprintUrgentChance = 0.45f;

        [SerializeField]
        [Range(0.1f, 5f)]
        float _aiSprintMinDuration = 0.45f;

        [SerializeField]
        [Range(0.1f, 5f)]
        float _aiSprintMaxDuration = 1.2f;

        [SerializeField]
        [Range(0.1f, 5f)]
        float _aiSprintDecisionIntervalMin = 0.35f;

        [SerializeField]
        [Range(0.1f, 5f)]
        float _aiSprintDecisionIntervalMax = 1.1f;

        [SerializeField]
        float _threatTrackDistance = 1f;

        [SerializeField]
        float _tendGoalDistance = 1f;

        [SerializeField]
        float _goalKeeperMovementRadius = 15f;

        [SerializeField]
        float _goalKeeperMovementRadiusSlowdown = 2f;

        [SerializeField]
        Goal _oppGoal;

        [SerializeField]
        Goal _teamGoal;

        [Header("Player Attributes")]

        //[SerializeField]
        //[Range(0.1f, 1f)]
        float  _accuracy = 0.8f;

        [SerializeField]
        [Range(0.1f, 1f)]
        float _goalKeeping = 0.8f;

        //[SerializeField]
        //[Range(0.1f, 5f)]
        float _power = 1.5f;

        //[SerializeField]
        //[Range(0.1f, 5f)]
        float _speed = 4f;

        [SerializeField]
        [Range(0.1f, 5f)]
        float _tendGoalSpeed = 4f;

        [SerializeField]
        Transform _homeRegion;

        [SerializeField]
        List<Player> _oppositionMembers;

        [SerializeField]
        List<Player> _teamMembers;

        [SerializeField]
        List<SupportSpot> _pitchPoints;

        [SerializeField]
        float _ballContrallableDistance = 1f;

        [SerializeField]
        float _ballTacklableDistance = 3f;

        [SerializeField]
        PlayerTypes _playerType;

        [SerializeField]
        GameObject _iconUserControlled;
        [SerializeField]
        GameObject _iconCanPassPlayer;

        float _radius;

        float _rotationSpeed = 6f;

        const float _manualPassDirectionAngle = 60f;
        const float _casualPassFallbackRangeMultiplier = 1.5f;

        Player _prevPassReceiver;
        RPGMovement _rpgMovement;
        float _currentStamina;
        float _staminaRegenBlockedUntil;
        float _aiSprintUntilTime;
        float _aiNextSprintDecisionTime;
        bool _isSprinting;
        int _lastSprintApplyFrame = -1;

        public Action OnBecameTheClosestPlayerToBall;
        public Action OnInstructedToGoToHome;
        public Action OnInstructedToTakeKickOff;
        public Action OnInstructedToWait;
        public Action OnIsNoLongerClosestPlayerToBall;
        public Action OnTackled;
        public Action OnTakeKickOff;
        public Action OnTeamGainedPossession;
        public Action OnTeamLostControl;

        public delegate void BallLaunched(float flightPower, float velocity, Vector3 initial, Vector3 target);
        public delegate void ChaseBallDel(Player player);
        public delegate void ControlBallDel(Player player);
        public delegate void InstructedToReceiveBall(float time, Vector3 position);

        public BallLaunched OnShotTaken;
        public ChaseBallDel OnChaseBall;
        public ControlBallDel OnControlBall;
        public InstructedToReceiveBall OnInstructedToReceiveBall;

        [SerializeField]
        public bool IsTeamInControl;// { get; set; }

        public bool IsUserControlled { get => _isUserControlled; set => _isUserControlled = value; }

        public float ActualAccuracy { get; set; }

        public float ActualPower { get; set; }

        public float ActualSpeed { get; set; }

        public float BallTime { get; set; }

        public float KickPower { get; set; }

        public float KickTime { get; set; }

        public Vector3? KickTarget { get; set; }

        public IFSM GoalKeeperFSM { get; set; }

        public IFSM InFieldPlayerFSM { get; set; }

        public KickType KickType { get; set; }

        public Player PassReceiver;// { get; set; }

        public SupportSpot SupportSpot { get; set; }

        public Transform HomeRegion { get => _homeRegion; set => _homeRegion = value; }
        public List<Player> OppositionMembers { get => _oppositionMembers; set => _oppositionMembers = value; }
        public List<Player> TeamMembers { get => _teamMembers; set => _teamMembers = value; }

        private void Awake()
        {
            //get some components
            GoalKeeperFSM = GetComponent<GoalKeeperFSM>();
            InFieldPlayerFSM = GetComponent<InFieldPlayerFSM>();
            RPGMovement = GetComponent<RPGMovement>();
            SupportSpot = GetComponent<SupportSpot>();

            // cache some component data
            _radius = GetComponent<CapsuleCollider>().radius;

            //initialize some data
            _accuracy = Mathf.Clamp(Random.value, 0.6f, 0.9f);
            _goalKeeping = Mathf.Clamp(Random.value, 0.6f, 0.9f);
            _power = Mathf.Clamp(Random.value, 0.6f, 0.9f);
            _speed = Mathf.Clamp(Random.value, 0.8f, 0.9f);
            _currentStamina = Mathf.Max(1f, _maxStamina);
            _staminaRegenBlockedUntil = 0f;
            _isSprinting = false;

            // keep preview icon hidden until a valid manual pass target is selected
            SetCanPassPreviewVisible(false);
        }

        private void LateUpdate()
        {
            // If no movement state applied sprint logic this frame, allow passive stamina regen.
            if (_lastSprintApplyFrame == Time.frameCount)
                return;

            _isSprinting = false;
            RegenerateStamina(Time.deltaTime);
        }

        void DrainStamina(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _currentStamina = Mathf.Max(0f, _currentStamina - _staminaDrainPerSecond * deltaTime);
            _staminaRegenBlockedUntil = Time.time + _staminaRegenDelay;
        }

        void RegenerateStamina(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            if (Time.time < _staminaRegenBlockedUntil)
                return;

            _currentStamina = Mathf.Min(MaxStamina, _currentStamina + _staminaRegenPerSecond * deltaTime);
        }

        public bool EvaluateAISprintIntent(bool isMoving, bool isHighUrgency = false)
        {
            if (!isMoving || _currentStamina <= 0.01f)
            {
                _aiSprintUntilTime = 0f;
                return false;
            }

            if (Time.time < _aiSprintUntilTime)
                return true;

            if (Time.time < _aiNextSprintDecisionTime)
                return false;

            float chance = isHighUrgency
                ? Mathf.Max(_aiSprintChance, _aiSprintUrgentChance)
                : _aiSprintChance;

            // Preserve some stamina so AI does not hard-drain itself every possession.
            if (_currentStamina <= MaxStamina * 0.2f)
                chance *= 0.35f;

            if (Random.value <= chance)
            {
                float sprintDuration = Random.Range(
                    Mathf.Min(_aiSprintMinDuration, _aiSprintMaxDuration),
                    Mathf.Max(_aiSprintMinDuration, _aiSprintMaxDuration));

                _aiSprintUntilTime = Time.time + Mathf.Max(0.1f, sprintDuration);
            }

            float nextDecisionDelay = Random.Range(
                Mathf.Min(_aiSprintDecisionIntervalMin, _aiSprintDecisionIntervalMax),
                Mathf.Max(_aiSprintDecisionIntervalMin, _aiSprintDecisionIntervalMax));

            _aiNextSprintDecisionTime = Time.time + Mathf.Max(0.1f, nextDecisionDelay);

            return Time.time < _aiSprintUntilTime;
        }

        public bool ApplySprintToMovement(bool wantsSprint, bool isMoving, float baseSpeedMultiplier = 1f)
        {
            _lastSprintApplyFrame = Time.frameCount;

            bool canSprint = wantsSprint && isMoving && _currentStamina > 0.01f;

            if (canSprint)
                DrainStamina(Time.deltaTime);
            else
                RegenerateStamina(Time.deltaTime);

            _isSprinting = canSprint;

            float normalizedBaseMultiplier = Mathf.Max(0.1f, baseSpeedMultiplier);
            float speedMultiplier = canSprint ? SprintSpeedMultiplier : 1f;
            float finalSpeed = Mathf.Max(0.1f, ActualSpeed * normalizedBaseMultiplier * speedMultiplier);

            if (_rpgMovement != null)
                _rpgMovement.Speed = finalSpeed;

            return canSprint;
        }

        public void ResetSprintState(float baseSpeedMultiplier = 1f)
        {
            _isSprinting = false;
            _aiSprintUntilTime = 0f;

            float normalizedBaseMultiplier = Mathf.Max(0.1f, baseSpeedMultiplier);
            float finalSpeed = Mathf.Max(0.1f, ActualSpeed * normalizedBaseMultiplier);

            if (_rpgMovement != null)
                _rpgMovement.Speed = finalSpeed;
        }

        public bool CanBallReachPoint(Vector3 position, float power, out float time)
        {
            //calculate the time
            time = TimeToTarget(Ball.Instance.NormalizedPosition,
                       position,
                       power,
                       Ball.Instance.Friction);

            //return result
            return time > 0;
        }

        /// <summary>
        /// Checks whether a player can pass
        /// </summary>
        /// <returns></returns>
        /// ToDo::Implement logic to cache players to message so that they can intercept the pass
        public bool CanPass(bool considerPassSafety = true)
        {
            if (TeamMembers == null || TeamMembers.Count == 0)
                return false;

            //set the pass target
            bool passToPlayerClosestToMe = false;// Random.value <= 0.1f;

            //reset pass selection
            KickTarget = null;
            PassReceiver = null;

            //loop through each team player and find a pass for each
            foreach (Player player in TeamMembers)
            {
                if (player == null)
                    continue;

                // can't pass to myself
                bool isPlayerMe = player == this;
                if (isPlayerMe)
                    continue;

                // we don't want to pass to the last receiver
                bool isPlayePrevPassReceiver = player == _prevPassReceiver;
                if (isPlayePrevPassReceiver)
                    continue;

                // can't pass to the goalie
                bool isPlayerGoalKeeper = player.PlayerType == PlayerTypes.Goalkeeper;
                if (isPlayerGoalKeeper)
                    continue;

                // check if player can pass
                CanPass(player.Position, considerPassSafety, passToPlayerClosestToMe, player);
            }

            // Casual fallback: if strict safety found nothing, allow less strict passing.
            if (KickTarget == null && considerPassSafety)
            {
                foreach (Player player in TeamMembers)
                {
                    if (player == null || player == this)
                        continue;

                    if (player == _prevPassReceiver)
                        continue;

                    if (player.PlayerType == PlayerTypes.Goalkeeper)
                        continue;

                    CanPass(player.Position, false, passToPlayerClosestToMe, player);
                }
            }

            // Last fallback: include previous receiver to avoid dead-ends.
            if (KickTarget == null)
            {
                foreach (Player player in TeamMembers)
                {
                    if (player == null || player == this)
                        continue;

                    if (player.PlayerType == PlayerTypes.Goalkeeper)
                        continue;

                    CanPass(player.Position, false, passToPlayerClosestToMe, player);
                }
            }

            //return result
            //Player can pass if there is a pass target
            return KickTarget != null;
        }

        public bool CanPass(Vector3 position, bool considerPassSafety = true, bool considerPlayerClosestToMe = false, Player player = null)
        {
            //get the possible pass options
            List<Vector3> passOptions = GetPassPositionOptions(position);

            //loop through each option and search if it is possible to 
            //pass to it. Consider positions higher up the pitch
            foreach (Vector3 passOption in passOptions)
            {
                // check if position is within pass range
                bool isPositionWithinPassRange = IsPositionWithinPassRange(passOption);

                // we consider a target which is out of our min pass distance
                if (isPositionWithinPassRange == true)
                {
                    //find power to kick ball
                    float power = FindPower(Ball.Instance.NormalizedPosition,
                        passOption,
                        BallPassArriveVelocity,
                        Ball.Instance.Friction);

                    //clamp the power to the player's max power
                    power = Mathf.Clamp(power, 0f, this.ActualPower);

                    //find if ball can reach point
                    float ballTimeToTarget = 0f;
                    bool canBallReachTarget = CanBallReachPoint(passOption,
                            power,
                            out ballTimeToTarget);

                    //return false if the time is less than zero
                    //that means the ball can't reach it's target
                    if (canBallReachTarget == false)
                        continue;

                    if (ballTimeToTarget <= 0f)
                        continue;

                    // get time of player to point
                    float receiverSpeed = player != null ? player.ActualSpeed : ActualSpeed;
                    receiverSpeed = Mathf.Max(0.01f, receiverSpeed);
                    float timeOfReceiverToTarget = TimeToTarget(position,
                        passOption,
                        receiverSpeed);

                    // pass is not safe if receiver can't reach target before the ball
                    if (timeOfReceiverToTarget > ballTimeToTarget)
                        continue;

                    // check if pass is safe from all opponents
                    bool isPassSafeFromAllOpponents = false;
                    if (considerPassSafety)
                    {
                        // check pass safety
                        isPassSafeFromAllOpponents = IsPassSafeFromAllOpponents(Ball.Instance.NormalizedPosition,
                            position,
                            passOption,
                            power,
                            ballTimeToTarget);
                    }

                    //if pass is safe from all opponents then cache it
                    if (considerPassSafety == false ||
                        (considerPassSafety == true && isPassSafeFromAllOpponents == true))
                    {
                        if (considerPlayerClosestToMe)
                        {
                            //set the pass-target to be the initial position
                            //check if pass is closer to goal and save it
                            if (KickTarget == null
                                || IsPositionCloserThanPosition(Position,
                                                        passOption,
                                                        (Vector3)KickTarget))
                            {
                                BallTime = ballTimeToTarget;
                                KickPower = power;
                                KickTarget = passOption;
                                PassReceiver = player;
                            }
                        }
                        else
                        {
                            //set the pass-target to be the initial position
                            //check if pass is closer to goal and save it
                            if (KickTarget == null
                                || IsPositionCloserThanPosition(OppGoal.transform.position,
                                                        passOption,
                                                        (Vector3)KickTarget))
                            {
                                BallTime = ballTimeToTarget;
                                KickPower = power;
                                KickTarget = passOption;
                                PassReceiver = player;
                            }
                        }
                    }
                }
            }

            //return result
            //Player can pass if there is a pass target
            return KickTarget != null;
        }

        public bool CanScore(bool considerGoalDistance = true, bool considerShotSafety = true)
        {
            // shoot if distance to goal is valid
            if (considerGoalDistance)
            {
                float distanceToGoal = Vector3.Distance(OppGoal.Position, Position);
                if (distanceToGoal > _distanceShotMaxValid)
                    return false;
            }

            //number of tries to find a shot
            float numOfTries = Random.Range(1, 6);

            //loop through and find a valid shot
            for (int i = 0; i < numOfTries; i++)
            {
                //find a random target
                Vector3 randomGoalTarget = FindRandomShot();

                float power = FindPower(Ball.Instance.NormalizedPosition,
                    randomGoalTarget,
                    _ballShotArriveVelocity);

                //clamp the power
                power = Mathf.Clamp(power, 0f, ActualPower);

                //check if ball can reach the target
                float time = 0f;
                bool canBallReachPoint = CanBallReachPoint(randomGoalTarget,
                    power,
                    out time);

                // keep searching other shot options if this one is not reachable
                if (!canBallReachPoint || time <= 0f)
                    continue;

                //check if shot to target is possible
                bool isShotPossible = false;
                if (considerShotSafety)
                {
                    isShotPossible = IsPassSafeFromAllOpponents(Ball.Instance.NormalizedPosition,
                        randomGoalTarget,
                        randomGoalTarget,
                        power,
                        time);
                }

                //if shot is possible set the data
                if (isShotPossible == false && considerShotSafety == false
                    || isShotPossible && considerShotSafety)
                {
                    //set the data
                    KickPower = power;
                    KickTarget = randomGoalTarget;
                    KickTime = time;

                    //return result
                    return true;
                }
            }

            return false;
        }

        public bool CanPlayerReachTargetBeforePlayer(Vector3 target, Player player001, Player player002)
        {
            return IsPositionCloserThanPosition(target,
                player001.Position,
                player002.Position);
        }

        public bool CanPassInDirection(Vector3 direction, bool allowAnyDirectionFallback = true)
        {
            if (TeamMembers == null || TeamMembers.Count == 0)
                return false;

            //set the pass target
            bool passToPlayerClosestToMe = Random.value <= 0.75f;

            //reset pass selection
            KickTarget = null;
            PassReceiver = null;

            //loop through each team player and find a pass for each
            foreach (Player player in TeamMembers)
            {
                if (player == null)
                    continue;

                // find a pass to a player who isn't me
                // who isn't a goal keeper
                // who is in this direction
                if (player != this
                    && player.PlayerType == PlayerTypes.InFieldPlayer
                    && player != _prevPassReceiver
                    && IsPositionInDirection(direction, player.Position, _manualPassDirectionAngle))
                {
                    CanPass(player.Position, true, passToPlayerClosestToMe, player);
                }
            }

            // if there is no pass simply find a team member in this direction
            if (KickTarget == null)
            {
                //loop through each team player and find a pass for each
                foreach (Player player in TeamMembers)
                {
                    if (player == null)
                        continue;

                    // find a pass to a player who isn't me
                    // who isn't a goal keeper
                    // who is in this direction
                    if (player != this
                        && player.PlayerType == PlayerTypes.InFieldPlayer
                        && IsPositionInDirection(direction, player.Position, _manualPassDirectionAngle))
                    {
                        CanPass(player.Position, false, passToPlayerClosestToMe, player);
                    }
                }
            }

            // Final fallback: ignore pass direction so user pass input never feels stuck.
            if (KickTarget == null && allowAnyDirectionFallback)
                CanPass(false);

            //return result
            //Player can pass if there is a pass target
            return KickTarget != null;

        }

        public bool IsPassSafeFromAllOpponents(Vector3 initialPosition, Vector3 receiverPosition, Vector3 target, float initialBallVelocity, float time)
        {
            //look for a player threatening the pass
            foreach (Player player in OppositionMembers)
            {
                bool isPassSafeFromOpponent = IsPassSafeFromOpponent(initialPosition,
                    target,
                    player.Position,
                    receiverPosition,
                    initialBallVelocity,
                    time);

                //return false if the pass is not safe
                if (isPassSafeFromOpponent == false)
                    return false;
            }

            //return result
            return true;
        }

        public bool IsPassSafeFromOpponent(Vector3 initialPosition, Vector3 target, Vector3 oppPosition, Vector3 receiverPosition, float initialBallVelocity, float timeOfBall)
        {
            #region Consider some logic that might threaten the pass

            //we might not want to pass to a player who is highly threatened(marked)
            if (IsPositionAHighThreat(receiverPosition, oppPosition))
                return false;

            //return false if opposition is closer to target than reciever
            if (IsPositionCloserThanPosition(target, oppPosition, receiverPosition))
                return false;

            //If oppossition is not between the passing lane then he is behind the passer
            //receiver and he can't intercept the ball
            if (IsPositionBetweenTwoPoints(initialPosition, receiverPosition, oppPosition) == false)
                return true;

            #endregion

            #region find if opponent can intercept ball

            //check if pass to position can be intercepted
            Vector3 orthogonalPoint = GetPointOrthogonalToLine(initialPosition,
                target,
                oppPosition);

            //get time of ball to point
            float timeOfBallToOrthogonalPoint = 0f;
            CanBallReachPoint(orthogonalPoint, initialBallVelocity, out timeOfBallToOrthogonalPoint);

            //get time of opponent to target
            float timeOfOpponentToTarget = TimeToTarget(oppPosition,
            orthogonalPoint,
            ActualSpeed);

            //ball is safe if it can reach that point before the opponent
            bool canBallReachOrthogonalPointBeforeOpp = timeOfBallToOrthogonalPoint < timeOfOpponentToTarget;

            if (canBallReachOrthogonalPointBeforeOpp == true)
                return true;
            else
                return false;
            // return true;
            #endregion
        }

        /// <summary>
        /// Checks whether this instance is picked out or not
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public bool IsPickedOut(Player player)
        {
            return SupportSpot.IsPickedOut(player);
        }

        public bool IsPositionBetweenTwoPoints(Vector3 A, Vector3 B, Vector3 point)
        {
            //find some direction vectors
            Vector3 fromAToPoint = point - A;
            Vector3 fromBToPoint = point - B;
            Vector3 fromBToA = A - B;
            Vector3 fromAToB = -fromBToA;

            //check if point is inbetween and return result
            return Vector3.Dot(fromAToB.normalized, fromAToPoint.normalized) > 0
                && Vector3.Dot(fromBToA.normalized, fromBToPoint.normalized) > 0;
        }

        /// <summary>
        /// Checks whether the first position is closer to target than the second position
        /// </summary>
        /// <param name="target"></param>
        /// <param name="position001"></param>
        /// <param name="position002"></param>
        /// <returns></returns>
        public bool IsPositionCloserThanPosition(Vector3 target, Vector3 position001, Vector3 position002)
        {
            return Vector3.Distance(position001, target) < Vector3.Distance(position002, target);
        }

        public bool IsPositionInDirection(Vector3 forward, Vector3 position, float angle)
        {
            // find direction to target
            Vector3 directionToTarget = position - Position;

            // find angle between forward and direction to target
            float angleBetweenDirections = Vector3.Angle(forward.normalized, directionToTarget.normalized);

            // return result
            return angleBetweenDirections <= angle / 2;
        }

        public bool IsPositionThreatened(Vector3 position)
        {
            //search for threatening player
            foreach (Player player in OppositionMembers)
            {
                if (IsPositionWithinHighThreatDistance(position, player.Position))
                    return true;
            }

            //return false
            return false;

        }

        public bool IsPositionWithinMinPassDistance(Vector3 position)
        {
            return IsWithinDistance(Position,
                position,
                _distancePassMin);
        }

        public bool IsPositionWithinMinPassDistance(Vector3 center, Vector3 position)
        {
            return IsWithinDistance(center,
                position,
                _distancePassMin);
        }

        public bool IsPositionWithinWanderRadius(Vector3 position)
        {
            return IsWithinDistance(_homeRegion.position,
                position,
                _maxWanderDistance);
        }

        /// <summary>
        /// Finds the power
        /// </summary>
        /// <param name="from">initial position</param>
        /// <param name="to">target</param>
        /// <param name="arriveVelocity">required velocity on arrival to target</param>
        /// <param name="friction">force acting against motion</param>
        /// <returns></returns>
        public float FindPower(Vector3 from, Vector3 to, float arriveVelocity, float friction)
        {
            // v^2 = u^2 + 2as => u^2 = v^2 - 2as => u = root(v^2 - 2as)

            //calculate some values
            float vSquared = Mathf.Pow(arriveVelocity, 2f);
            float twoAS = 2 * friction * Vector3.Distance(from, to);
            float uSquared = vSquared - twoAS;

            //find result
            float result = Mathf.Sqrt(uSquared);

            //return result
            return result;
        }

        public float TimeToTarget(Vector3 initial, Vector3 target, float velocityInitial)
        {
            //use S = D/T => T = D/S
            return Vector3.Distance(initial, target) / velocityInitial;
        }

        /// <summary>
        /// Calculates the time it will take to reach the target
        /// </summary>
        /// <param name="inital">start position</param>
        /// <param name="target">final position</param>
        /// <param name="initialVelocity">initial velocity</param>
        /// <param name="acceleration">force acting aginst motion</param>
        /// <returns></returns>
        public float TimeToTarget(Vector3 initial, Vector3 target, float velocityInitial, float acceleration)
        {
            //using  v^2 = u^2 + 2as 
            float distance = Vector3.Distance(initial, target);
            float uSquared = Mathf.Pow(velocityInitial, 2f);
            float v_squared = uSquared + (2 * acceleration * distance);

            //if v_squared is less thaSn or equal to zero it means we can't reach the target
            if (v_squared <= 0)
                return -1.0f;

            //find the final velocity
            float v = Mathf.Sqrt(v_squared);

            //find time to travel 
            return TimeToTravel(velocityInitial, v, acceleration);
        }

        public float TimeToTravel(float initialVelocity, float finalVelocity, float acceleration)
        {
            // t = v-u
            //     ---
            //      a
            float time = (finalVelocity - initialVelocity) / acceleration;

            //return result
            return time;
        }

        /// <summary>
        /// Finds a random target on the goal
        /// </summary>
        /// <returns></returns>
        public Vector3 FindRandomShot()
        {
            if (_oppGoal == null)
                return Position + transform.forward * 2f;

            return _oppGoal.GetRandomShotTarget();
        }

        /// <summary>
        /// Calculates a point on line a-b that is at right angle to a point
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public Vector3 GetPointOrthogonalToLine(Vector3 from, Vector3 to, Vector3 point)
        {
            //this is the normal
            Vector3 fromTo = to - from;

            //this is the vector/direction
            Vector3 fromPoint = point - from;

            //find projection
            Vector3 projection = Vector3.Project(fromPoint, fromTo);

            //find point on normal
            return projection + from;
        }

        /// <summary>
        /// Gets the options to pass the ball
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public List<Vector3> GetPassPositionOptions(Vector3 position)
        {
            //create a list to hold the results
            List<Vector3> result = new List<Vector3>();

            //the first position is the current position
            result.Add(position);

            //set some data
            float incrementAngle = 45;
            float iterations = 360 / incrementAngle;
            float passOptionDistance = Mathf.Max(1f, _distancePassMin * 0.75f);

            Vector3 baseDirection = position - Position;
            baseDirection.y = 0f;
            if (baseDirection.sqrMagnitude <= 0.0001f)
                baseDirection = transform.forward;
            baseDirection.Normalize();

            //find some positions around the player
            for (int i = 0; i < iterations; i++)
            {
                //get the direction
                float angle = incrementAngle * i;

                //rotate the direction
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * baseDirection;

                //get point
                Vector3 point = position + direction * passOptionDistance;

                //add to list
                result.Add(point);
            }

            //return results
            return result;
        }

        /// <summary>
        /// Initializes this instance
        /// </summary>
        public void Init()
        {
            ActualPower *= _power;
            ActualSpeed *= _speed;

            //Init the RPGMovement
            RPGMovement.Init(ActualSpeed,
                ActualSpeed,
                _rotationSpeed * _speed,
                ActualSpeed);
        }

        /// <summary>
        /// Initializes this instance
        /// </summary>
        /// <param name="power"></param>
        /// <param name="speed"></param>
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
            _distanceShotMaxValid = distanceShotValidMax;
            _tendGoalDistance = distanceTendGoal;
            _threatTrackDistance = distanceThreatTrack;
            _maxWanderDistance = distanceWonderMax;
            _ballPassArriveVelocity = velocityPassArrive;
            _ballShotArriveVelocity = velocityShotArrive;
            _distanceThreatMax = distanceThreatMax;
            _distanceThreatMin = distanceThreatMin;
            ActualSpeed = speed;
            ActualPower = power;
        }

        /// <summary>
        /// Checks whether the player is at target
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool IsAtTarget(Vector3 position)
        {
            return IsWithinDistance(Position, position, 0.25f);
        }

        public bool IsBallWithinControlableDistance()
        {
            return IsWithinDistance(Position, Ball.Instance.NormalizedPosition, _ballContrallableDistance + Radius);
        }

        public bool IsBallWithinTacklableDistance()
        {
            return IsWithinDistance(Position, Ball.Instance.NormalizedPosition, _ballTacklableDistance);
        }

        public bool IsInfrontOfPlayer(Vector3 position)
        {
            // find the direction to target
            Vector3 direction = position - Position;

            float dot = Vector3.Dot(direction.normalized, transform.forward);

            return dot > 0;
            ////transfrom point to local
            //Vector3 localDirection = transform.InverseTransformDirection(direction);

            ////return result
            //return localDirection.z >= 1;
        }

        /// <summary>
        /// Checks whether a player is a threat
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public bool IsPlayerAThreat(Player player)
        {
            return IsWithinDistance(Position, player.Position, DistanceThreatMin);
        }

        public bool IsPositionAHighThreat(Vector3 position)
        {
            return IsPositionWithinHighThreatDistance(Position, position);
        }

        public bool IsPositionALowThreat(Vector3 position)
        {
            return IsPositionWithinLowThreatDistance(Position, position);
        }

        public bool IsPositionWithinHighThreatDistance(Vector3 center, Vector3 position)
        {
            return IsWithinDistanceRange(center, position, 0f, DistanceThreatMax);
        }

        public bool IsPositionWithinLowThreatDistance(Vector3 center, Vector3 position)
        {
            return IsWithinDistanceRange(center, position, DistanceThreatMin, DistanceThreatMax);
        }

        public bool IsPositionWithinPassRange(Vector3 position)
        {
            return IsWithinDistanceRange(Position,
                position,
                _distancePassMin,
                _distancePassMax);
        }

        public bool IsPositionWithinPassRange(Vector3 center, Vector3 position)
        {
            return IsWithinDistanceRange(center,
                position,
                _distancePassMin,
                _distancePassMax);
        }

        /// <summary>
        /// Check whether a position is a threat or not
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool IsPositionAThreat(Vector3 position)
        {
            // position is a threat if its within saftey distance
            return IsWithinDistance(Position, position, DistanceThreatMax);
        }

        public bool IsPositionAThreat(Vector3 center, Vector3 position)
        {
            return IsWithinDistance(center, position, DistanceThreatMax);
        }

        public bool IsPositionAHighThreat(Vector3 center, Vector3 position)
        {
            return IsWithinDistance(center, position, DistanceThreatMax);
        }

        public bool IsTeamMemberWithinMinPassDistance(Vector3 position)
        {
            if (_teamMembers == null)
                return false;

            float minTeamSeparation = Mathf.Max(0.75f, _distancePassMin * 0.75f);

            foreach (Player tM in _teamMembers)
            {
                if (tM != this && IsWithinDistance(position, tM.transform.position, minTeamSeparation))
                    return true;
            }

            return false;
        }

        public bool IsThreatened()
        {
            //search for threatening player
            foreach (Player player in OppositionMembers)
            {
                if (IsPlayerAThreat(player))
                    return true;
            }

            //return false
            return false;
        }

        public bool IsWithinDistance(Vector3 center, Vector3 position, float distance)
        {
            return Vector3.Distance(center, position) <= distance;
        }

        public bool IsWithinDistanceRange(Vector3 center, Vector3 position, float minDistance, float maxDistance)
        {
            return !IsWithinDistance(center, position, minDistance) && IsWithinDistance(center, position, maxDistance);
        }

        /// <summary>
        /// Finds the power needed to kick the ball and make it reach
        /// a particular target with a particular velocity
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="arrivalVelocity"></param>
        /// <returns></returns>
        public float FindPower(Vector3 from, Vector3 to, float arrivalVelocity)
        {
            //find the power to target
            float power = Ball.Instance.FindPower(from,
                to,
                arrivalVelocity);

            //return result
            return power;
        }

        public void Invoke_OnBallLaunched(float flightTime, float velocity, Vector3 initial, Vector3 target)
        {
            BallLaunched temp = OnShotTaken;
            if (temp != null)
                temp.Invoke(flightTime, velocity, initial, target);
        }

        public void Invoke_OnBecameTheClosestPlayerToBall()
        {
            ActionUtility.Invoke_Action(OnBecameTheClosestPlayerToBall);
        }

        public void Invoke_OnInstructedToTakeKickOff()
        {
            ActionUtility.Invoke_Action(OnInstructedToTakeKickOff);
        }

        public void Invoke_OnInstructedToWait()
        {
            ActionUtility.Invoke_Action(OnInstructedToWait);
        }

        public void Invoke_OnIsNoLongerTheClosestPlayerToBall()
        {
            ActionUtility.Invoke_Action(OnIsNoLongerClosestPlayerToBall);
        }

        public void Invoke_OnTeamGainedPossession()
        {
            // set that my team is in control
            IsTeamInControl = true;

            // raise event that team is now in control
            ActionUtility.Invoke_Action(OnTeamGainedPossession);
        }

        public void Invoke_OnTeamLostControl()
        {
            // set team no longer in control
            IsTeamInControl = false;

            // invoke team has lost control
            ActionUtility.Invoke_Action(OnTeamLostControl);
        }

        /// <summary>
        /// Player kicks the ball from his position to the target
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public void MakePass(Vector3 from, Vector3 to, Player receiver, float power, float time)
        {
            if (receiver == null)
                receiver = GetRandomTeamMemberInRadius(Mathf.Max(20f, _distancePassMax * _casualPassFallbackRangeMultiplier));

            float finalPower = power;
            if (_playerType == PlayerTypes.Goalkeeper)
                finalPower = Mathf.Max(0f, power * _goalKeeperPassPowerMultiplier);

            //kick the ball to target
            Ball.Instance.Kick(to, finalPower);

            float receiveTime = time;
            float recalculatedTime = Ball.Instance.TimeToCoverDistance(Ball.Instance.NormalizedPosition,
                to,
                Mathf.Max(0.1f, finalPower),
                true);

            if (!float.IsNaN(recalculatedTime)
                && !float.IsInfinity(recalculatedTime)
                && recalculatedTime > 0f)
            {
                receiveTime = recalculatedTime;
            }

            //message the receiver to receive the ball
            if (receiver != null)
            {
                InstructedToReceiveBall temp = receiver.OnInstructedToReceiveBall;
                if (temp != null)
                    temp.Invoke(receiveTime, to);
            }
        }

        public void MakeShot(Vector3 from, Vector3 to, float power, float time)
        {
            float shotPower = Mathf.Max(0f, power * _shotPowerMultiplier);

            //launch the ball
            Ball.Instance.Kick(to, shotPower);

            // raise the ball shot event
            Ball.BallLaunched temp = Ball.Instance.OnBallShot;
            if (temp != null)
                temp.Invoke(time, shotPower, from, to);
        }

        /// <summary>
        /// Puts the ball infront of this player
        /// </summary>
        public void PlaceBallInfronOfMe()
        {
            Ball.Instance.NormalizedPosition = Position + transform.forward * (Radius + _ballControlDistance);
            Ball.Instance.transform.rotation = transform.rotation;
        }

        public List<Player> GetTeamMembersInRadius(float radius)
        {
            if (_teamMembers == null)
                return new List<Player>();

            //get the players
            List<Player> result = _teamMembers.FindAll(tM => Vector3.Distance(this.Position, tM.Position) <= radius
            && this != tM);

            //retur result
            return result;
        }

        public Player GetRandomTeamMemberInRadius(float radius)
        {
            //get the list
            List<Player> players = GetTeamMembersInRadius(radius);

            //return random player
            if (players == null || players.Count == 0)
                return null;
            else
                return players[Random.Range(0, players.Count)];
        }

        public void SetCanPassPreviewVisible(bool visible)
        {
            if (_iconCanPassPlayer == null)
                return;

            if (_iconCanPassPlayer.activeSelf != visible)
                _iconCanPassPlayer.SetActive(visible);
        }

        public Vector3 GoalKeeperHomeCenter
        {
            get
            {
                Vector3 center = HomeRegion != null ? HomeRegion.position : Position;
                center.y = 0f;
                return center;
            }
        }

        public Vector3 ClampGoalKeeperTargetToHomeRadius(Vector3 target)
        {
            Vector3 center = GoalKeeperHomeCenter;
            Vector3 offset = target - center;
            offset.y = 0f;

            float radius = GoalKeeperMovementRadius;
            float sqrRadius = radius * radius;
            if (offset.sqrMagnitude > sqrRadius)
                target = center + offset.normalized * radius;

            target.y = 0f;
            return target;
        }

        public Vector3 ConstrainGoalKeeperMoveDirection(Vector3 desiredDirection)
        {
            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            Vector3 center = GoalKeeperHomeCenter;
            Vector3 toPlayer = Position - center;
            toPlayer.y = 0f;

            float distance = toPlayer.magnitude;
            float radius = GoalKeeperMovementRadius;
            float slowdownStart = Mathf.Max(0f, radius - GoalKeeperMovementRadiusSlowdown);

            if (distance < slowdownStart)
                return desiredDirection;

            if (distance <= 0.0001f)
                return desiredDirection;

            Vector3 outward = toPlayer / distance;
            float outwardVelocity = Vector3.Dot(desiredDirection, outward);
            if (outwardVelocity <= 0f)
                return desiredDirection;

            // Remove outward drift at boundary and fade speed near the edge to avoid abrupt stops.
            Vector3 constrained = desiredDirection - outward * outwardVelocity;
            float edgeT = Mathf.InverseLerp(slowdownStart, radius, distance);
            float speedScale = Mathf.Lerp(1f, 0.15f, edgeT);

            if (distance >= radius)
                speedScale = 0f;

            constrained *= speedScale;

            return constrained;
        }

        public Quaternion Rotation
        {
            get
            {
                return transform.rotation;
            }

            set
            {
                transform.rotation = value;
            }
        }

        public Vector3 Position
        {
            get
            {
                return new Vector3(transform.position.x, 0f, transform.position.z);
            }

            set
            {
                transform.position = new Vector3(value.x, 0f, value.z);
            }
        }

        public float BallControlDistance { get => _ballControlDistance; set => _ballControlDistance = value; }
        public Goal OppGoal { get => _oppGoal; set => _oppGoal = value; }
        public float BallPassArriveVelocity { get => _ballPassArriveVelocity; set => _ballPassArriveVelocity = value; }
        public List<SupportSpot> PlayerSupportSpots { get => _pitchPoints; set => _pitchPoints = value; }

        public float DistancePassMin
        {
            get => _distancePassMin;
            set => _distancePassMin = value;
        }

        public float DistanceThreatMin
        {
            get => _distanceThreatMin + _radius;
            set => _distanceThreatMin = value;
        }

        public float DistanceThreatMax
        {
            get => _distanceThreatMax + _radius;
            set => _distanceThreatMax = value;
        }

        public RPGMovement RPGMovement
        {
            get
            {
                // set the rpg movement
                if (_rpgMovement == null)
                {
                    gameObject.AddComponent<RPGMovement>();
                    _rpgMovement = GetComponent<RPGMovement>();
                }

                // return result
                return _rpgMovement;
            }

            set
            {
                _rpgMovement = value;
            }
        }

        public float Radius { get => _radius; set => _radius = value; }
        public Goal TeamGoal { get => _teamGoal; set => _teamGoal = value; }
        public PlayerTypes PlayerType { get => _playerType; set => _playerType = value; }
        public float ThreatTrackDistance { get => _threatTrackDistance; set => _threatTrackDistance = value; }
        public float TendGoalSpeed { get => _tendGoalSpeed; set => _tendGoalSpeed = value; }
        public float TendGoalDistance { get => _tendGoalDistance; set => _tendGoalDistance = value; }
        public float GoalKeeping { get => _goalKeeping; set => _goalKeeping = value; }
        public float DistanceShotMaxValid { get => _distanceShotMaxValid; set => _distanceShotMaxValid = value; }
        public float DistancePassMax { get => _distancePassMax; set => _distancePassMax = value; }
        public float GoalKeeperMovementRadius
        {
            get => Mathf.Max(1f, _goalKeeperMovementRadius);
            set => _goalKeeperMovementRadius = Mathf.Max(1f, value);
        }
        public float GoalKeeperMovementRadiusSlowdown
        {
            get => Mathf.Max(0.1f, _goalKeeperMovementRadiusSlowdown);
            set => _goalKeeperMovementRadiusSlowdown = Mathf.Max(0.1f, value);
        }
        public float GoalKeeperPickupBlockedUntil { get; set; }
        public float GoalKeeperPassPowerMultiplier
        {
            get => Mathf.Max(1f, _goalKeeperPassPowerMultiplier);
            set => _goalKeeperPassPowerMultiplier = Mathf.Max(1f, value);
        }
        public float SprintSpeedMultiplier
        {
            get => Mathf.Max(1f, _sprintSpeedMultiplier);
            set => _sprintSpeedMultiplier = Mathf.Max(1f, value);
        }
        public float MaxStamina
        {
            get => Mathf.Max(1f, _maxStamina);
            set
            {
                _maxStamina = Mathf.Max(1f, value);
                _currentStamina = Mathf.Clamp(_currentStamina, 0f, _maxStamina);
            }
        }
        public float CurrentStamina
        {
            get => Mathf.Clamp(_currentStamina, 0f, MaxStamina);
            set => _currentStamina = Mathf.Clamp(value, 0f, MaxStamina);
        }
        public float CurrentStaminaNormalized
        {
            get
            {
                float maxStamina = MaxStamina;
                if (maxStamina <= 0.0001f)
                    return 0f;

                return CurrentStamina / maxStamina;
            }
        }
        public bool IsSprinting => _isSprinting;
        public Player PrevPassReceiver { get => _prevPassReceiver; set => _prevPassReceiver = value; }
        public GameObject IconUserControlled { get => _iconUserControlled; set => _iconUserControlled = value; }
        public GameObject IconCanPassPlayer { get => _iconCanPassPlayer; set => _iconCanPassPlayer = value; }
    }
}
