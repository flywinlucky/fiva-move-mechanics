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

        MatchDifficultyProfile GetDifficultyProfile()
        {
            if (MatchManager.Instance != null)
                return MatchManager.Instance.CurrentDifficultyProfile;

            return new MatchDifficultyProfile
            {
                PassMaxMultiplier = 1f,
                PassMinMultiplier = 1f,
                AICarrierLeadTime = 0.35f,
                AICarrierSideStepDistance = 1.15f,
                AIBehindDotThreshold = -0.3f,
                AIBehindStickBreakDistance = 1.25f,
                AIChaseSlowdownWhenBehind = 0.93f
            };
        }

        Vector3 GetCarrierPressingTarget(Player carrier, out bool isDirectlyBehindCarrier, out float carrierDistance)
        {
            Vector3 carrierForward = Vector3.Scale(carrier.transform.forward, new Vector3(1f, 0f, 1f));
            if (carrierForward.sqrMagnitude <= 0.0001f)
                carrierForward = Vector3.forward;
            carrierForward.Normalize();

            Vector3 fromCarrierToChaser = Owner.Position - carrier.Position;
            fromCarrierToChaser.y = 0f;
            carrierDistance = fromCarrierToChaser.magnitude;

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

            return predictedCarrierPosition;
        }

        public override void Enter()
        {
            base.Enter();

            _difficultyProfile = GetDifficultyProfile();

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

            //check if ball is within control distance
            if (isOpponentCarrier && Owner.IsBallWithinControlableDistance())
            {
                bool shouldBlockRearTackle = isDirectlyBehindCarrier
                    && carrierDistance <= _difficultyProfile.AIBehindStickBreakDistance;

                if (!shouldBlockRearTackle)
                {
                    //tackle player
                    SuperMachine.ChangeState<TackleMainState>();
                    return;
                }
            }
            else if (!hasBallCarrier && Owner.IsBallWithinControlableDistance())
            {
                // control ball
                SuperMachine.ChangeState<ControlBallMainState>();
                return;
            }

            float chaseSpeedMultiplier = 1f;
            if (isDirectlyBehindCarrier && carrierDistance <= _difficultyProfile.AIBehindStickBreakDistance)
                chaseSpeedMultiplier = _difficultyProfile.AIChaseSlowdownWhenBehind;

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
