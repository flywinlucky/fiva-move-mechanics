using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ControlBall.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.GoToHome.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.TacklePlayer.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ChaseBall.SubStates
{
    public class AutomaticChase : BState
    {
        /// <summary>
        /// The steering target
        /// </summary>
        public Vector3 SteeringTarget { get; set; }

        MatchDifficultyProfile _difficultyProfile;
        float _nextDecisionTime;
        float _rearChaseTime;
        float _turnDelayUntil;
        Vector3 _lastCarrierForward;

        const float RearChaseLimiterSeconds = 1.35f;
        const float PersonalSpaceBuffer = 0.95f;

        MatchDifficultyProfile GetDifficultyProfile()
        {
            if (MatchManager.Instance != null)
                return MatchManager.Instance.RuntimeDifficultyProfile;

            return new MatchDifficultyProfile
            {
                PassMaxMultiplier = 1f,
                PassMinMultiplier = 1f,
                AICarrierLeadTime = 0.35f,
                AICarrierSideStepDistance = 1.15f,
                AIBehindDotThreshold = -0.3f,
                AIBehindStickBreakDistance = 1.25f,
                AIChaseSlowdownWhenBehind = 0.93f,
                AIReactionDelayMin = 0.1f,
                AIReactionDelayMax = 0.22f,
                AIErrorChanceBase = 0.06f,
                AIPressureErrorBoost = 0.12f,
                AIDecisionHesitationChance = 0.08f,
                AIUnderPressureDribbleSlowdown = 0.9f,
                AIDefensiveGapChance = 0.1f,
                AIPlayerAdvantageRadius = 1.8f,
                AIPlayerInterceptionAssist = 0.08f,
                AIBadTouchChance = 0.08f
            };
        }

        bool IsDecisionTick()
        {
            if (Time.time < _nextDecisionTime)
                return false;

            float minDelay = Mathf.Max(0f, _difficultyProfile.AIReactionDelayMin);
            float maxDelay = Mathf.Max(minDelay, _difficultyProfile.AIReactionDelayMax);
            _nextDecisionTime = Time.time + Random.Range(minDelay, maxDelay);
            return true;
        }

        float ComputePressure01(Player player)
        {
            if (player == null || player.OppositionMembers == null)
                return 0f;

            int nearbyCount = 0;
            float pressureRadius = Mathf.Max(0.9f, player.DistanceThreatMax * 1.35f);
            for (int i = 0; i < player.OppositionMembers.Count; i++)
            {
                Player opponent = player.OppositionMembers[i];
                if (opponent == null)
                    continue;

                if ((opponent.Position - player.Position).sqrMagnitude <= pressureRadius * pressureRadius)
                    nearbyCount++;
            }

            return Mathf.Clamp01(nearbyCount / 3f);
        }

        bool IsUserOpponentNearBall(float radius)
        {
            if (Ball.Instance == null || Owner.OppositionMembers == null)
                return false;

            float sqrRadius = Mathf.Max(0.25f, radius) * Mathf.Max(0.25f, radius);
            Vector3 ballPosition = Ball.Instance.NormalizedPosition;

            for (int i = 0; i < Owner.OppositionMembers.Count; i++)
            {
                Player opponent = Owner.OppositionMembers[i];
                if (opponent == null || !opponent.IsUserControlled)
                    continue;

                if ((opponent.Position - ballPosition).sqrMagnitude <= sqrRadius)
                    return true;
            }

            return false;
        }

        Vector3 GetCarrierPressingTarget(Player carrier, out bool isDirectlyBehindCarrier, out float carrierDistance)
        {
            Vector3 carrierForward = Vector3.Scale(carrier.transform.forward, new Vector3(1f, 0f, 1f));
            if (carrierForward.sqrMagnitude <= 0.0001f)
                carrierForward = Vector3.forward;
            carrierForward.Normalize();

            if (_lastCarrierForward.sqrMagnitude <= 0.0001f)
                _lastCarrierForward = carrierForward;

            float turnDot = Vector3.Dot(_lastCarrierForward, carrierForward);
            if (turnDot <= 0.75f)
                _turnDelayUntil = Mathf.Max(_turnDelayUntil, Time.time + Random.Range(0.15f, 0.3f));

            _lastCarrierForward = carrierForward;

            Vector3 fromCarrierToChaser = Owner.Position - carrier.Position;
            fromCarrierToChaser.y = 0f;
            carrierDistance = fromCarrierToChaser.magnitude;

            float engageDistance = Mathf.Max(1.1f, Owner.BallTacklableDistance * 0.75f);
            if (carrierDistance <= engageDistance)
            {
                isDirectlyBehindCarrier = false;
                return carrier.Position;
            }

            isDirectlyBehindCarrier = false;
            if (carrierDistance > 0.0001f)
            {
                float dot = Vector3.Dot(carrierForward, fromCarrierToChaser / carrierDistance);
                isDirectlyBehindCarrier = dot <= _difficultyProfile.AIBehindDotThreshold;
            }

            float carrierSpeed = carrier.RPGMovement != null
                ? Mathf.Max(0.1f, carrier.RPGMovement.CurrentSpeed)
                : Mathf.Max(0.1f, carrier.ActualSpeed);

            Vector3 predictedCarrierPosition = carrier.Position + carrierForward * carrierSpeed * _difficultyProfile.AICarrierLeadTime;

            // If chasing directly from behind, step out to a side lane to avoid sticky tailing.
            if (isDirectlyBehindCarrier && carrierDistance <= _difficultyProfile.AIBehindStickBreakDistance)
            {
                Vector3 sideDirection = Vector3.Cross(Vector3.up, carrierForward).normalized;
                if (sideDirection.sqrMagnitude <= 0.0001f)
                    sideDirection = Vector3.right;

                float sideSign = Mathf.Sign(Vector3.Dot(fromCarrierToChaser, sideDirection));
                if (Mathf.Approximately(sideSign, 0f))
                    sideSign = (Owner.GetInstanceID() & 1) == 0 ? 1f : -1f;

                predictedCarrierPosition += sideDirection * sideSign * _difficultyProfile.AICarrierSideStepDistance;
            }

            // Personal space buffer keeps AI from permanently sitting on the carrier's back.
            float desiredSpacing = Mathf.Max(PersonalSpaceBuffer, Owner.Radius + carrier.Radius + 0.25f);
            if (carrierDistance < desiredSpacing)
            {
                Vector3 spacingTarget = carrier.Position - (carrierForward * desiredSpacing);
                predictedCarrierPosition = Vector3.Lerp(predictedCarrierPosition, spacingTarget, 0.75f);
            }

            return predictedCarrierPosition;
        }

        public override void Enter()
        {
            base.Enter();

            _difficultyProfile = GetDifficultyProfile();
            _nextDecisionTime = Time.time + Random.Range(_difficultyProfile.AIReactionDelayMin, _difficultyProfile.AIReactionDelayMax);
            _rearChaseTime = 0f;
            _turnDelayUntil = 0f;
            _lastCarrierForward = Vector3.zero;

            Owner.SetCanPassPreviewVisible(false);

            //get the steering target
            SteeringTarget = Ball.Instance.NormalizedPosition;

            //set the steering to on
            Owner.RPGMovement.SetMoveTarget(SteeringTarget);
            Owner.RPGMovement.SetRotateFacePosition(SteeringTarget);
            Owner.RPGMovement.SetSteeringOn();
            Owner.RPGMovement.SetTrackingOn();
        }

        public override void Execute()
        {
            base.Execute();

            if (Owner.IsUserControlled)
            {
                Machine.ChangeState<ManualChase>();
                return;
            }

            _difficultyProfile = GetDifficultyProfile();
            bool decisionTick = IsDecisionTick();

            Player ballOwner = Ball.Instance.Owner;
            bool hasBallCarrier = ballOwner != null && ballOwner != Owner;
            bool isOpponentCarrier = hasBallCarrier && ballOwner.IsTeamInControl != Owner.IsTeamInControl;
            bool isGoalkeeperCarrier = hasBallCarrier && ballOwner.PlayerType == PlayerTypes.Goalkeeper;

            if (hasBallCarrier && !isOpponentCarrier)
            {
                // teammate has possession; stop chasing and reposition
                Owner.SetCanPassPreviewVisible(false);
                SuperMachine.ChangeState<GoToHomeMainState>();
                return;
            }

            if (isOpponentCarrier && isGoalkeeperCarrier)
            {
                // do not press a keeper that has the ball in hand
                Owner.SetCanPassPreviewVisible(false);
                SuperMachine.ChangeState<GoToHomeMainState>();
                return;
            }

            bool shouldShowAiBallIndicator = Ball.Instance.Owner == Owner || Owner.IsBallWithinControlableDistance();
            Owner.SetCanPassPreviewVisible(shouldShowAiBallIndicator);

            bool isDirectlyBehindCarrier = false;
            float carrierDistance = 0f;

            //get the steering target
            if (isOpponentCarrier)
                SteeringTarget = GetCarrierPressingTarget(ballOwner, out isDirectlyBehindCarrier, out carrierDistance);
            else
                SteeringTarget = Ball.Instance.NormalizedPosition;

            if (isDirectlyBehindCarrier)
                _rearChaseTime += Time.deltaTime;
            else
                _rearChaseTime = Mathf.Max(0f, _rearChaseTime - (Time.deltaTime * 1.35f));

            //check if ball is within control distance
            float pressure01 = isOpponentCarrier ? ComputePressure01(ballOwner) : 0f;
            float aiErrorChance = Mathf.Clamp01(_difficultyProfile.AIErrorChanceBase + (_difficultyProfile.AIPressureErrorBoost * pressure01));

            if (isOpponentCarrier && Owner.IsBallWithinTacklableDistance())
            {
                bool isUserCarrier = ballOwner != null && ballOwner.IsUserControlled;
                float userCarrierEngageDistance = Mathf.Max(0.65f,
                    Owner.BallTacklableDistance * Mathf.Clamp(_difficultyProfile.AITackleEngageDistanceScale, 0.4f, 1.1f));
                bool canEngageUserCarrier = !isUserCarrier || carrierDistance <= userCarrierEngageDistance;

                if (canEngageUserCarrier)
                {
                    bool shouldBlockRearTackle = isDirectlyBehindCarrier
                        && carrierDistance <= Mathf.Max(0.45f, _difficultyProfile.AIBehindStickBreakDistance * 0.45f)
                        && _rearChaseTime < 0.18f
                        && !isUserCarrier;

                    bool shouldHesitateTackle = !decisionTick || Random.value <= (aiErrorChance * 0.45f);

                    if (isUserCarrier)
                        shouldHesitateTackle = false;

                    if (isDirectlyBehindCarrier && _rearChaseTime >= 0.35f)
                        shouldHesitateTackle |= Random.value <= 0.12f;

                    if (!isUserCarrier && ballOwner != null && ballOwner.IsUserControlled)
                        shouldHesitateTackle |= Random.value <= _difficultyProfile.AIPlayerInterceptionAssist;

                    if (!shouldBlockRearTackle && !shouldHesitateTackle)
                    {
                        //tackle player
                        SuperMachine.ChangeState<TackleMainState>();
                        return;
                    }
                }
            }
            else if (!hasBallCarrier && Owner.IsBallWithinControlableDistance())
            {
                bool userNearBall = IsUserOpponentNearBall(_difficultyProfile.AIPlayerAdvantageRadius);
                float interceptionAssistChance = userNearBall ? _difficultyProfile.AIPlayerInterceptionAssist : 0f;

                if (decisionTick && Random.value > interceptionAssistChance)
                {
                    // control ball
                    SuperMachine.ChangeState<ControlBallMainState>();
                    return;
                }
            }

            float chaseSpeedMultiplier = 1f;
            if (isDirectlyBehindCarrier && carrierDistance <= _difficultyProfile.AIBehindStickBreakDistance)
                chaseSpeedMultiplier = _difficultyProfile.AIChaseSlowdownWhenBehind;

            if (_rearChaseTime >= RearChaseLimiterSeconds)
                chaseSpeedMultiplier *= 0.75f;

            if (Time.time < _turnDelayUntil)
            {
                chaseSpeedMultiplier *= 0.82f;
                if (hasBallCarrier)
                {
                    // During sudden turn reactions, avoid instant mirror movement.
                    SteeringTarget = Vector3.Lerp(SteeringTarget, ballOwner.Position, 0.3f);
                }
            }

            if (isOpponentCarrier && ballOwner != null && ballOwner.IsUserControlled)
                chaseSpeedMultiplier *= (1f - (_difficultyProfile.AIPlayerInterceptionAssist * 0.55f));

            if (!decisionTick)
                chaseSpeedMultiplier *= 0.94f;

            bool isMoving = (SteeringTarget - Owner.Position).sqrMagnitude > 0.25f;
            bool wantsSprint = Owner.EvaluateAISprintIntent(isMoving, isOpponentCarrier);
            Owner.ApplySprintToMovement(wantsSprint, isMoving, chaseSpeedMultiplier);

            //set the steering to on
            Owner.RPGMovement.SetMoveTarget(SteeringTarget);
            Owner.RPGMovement.SetRotateFacePosition(SteeringTarget);
        }

        public override void Exit()
        {
            base.Exit();

            Owner.SetCanPassPreviewVisible(false);

            //restore default chase speed
            Owner.ResetSprintState();

            //set the steering to on
            Owner.RPGMovement.SetSteeringOff();
            Owner.RPGMovement.SetTrackingOff();
        }

        public Player Owner
        {
            get
            {
                return ((InFieldPlayerFSM)SuperMachine).Owner;
            }
        }
    }
}
