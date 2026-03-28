using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.KickBall.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Objects;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ControlBall.SubStates
{
    public class AutomaticControl : BState
    {
        int maxNumOfTries;
        float maxPassTime;
        Range rangePassTime = new Range(0.5f, 1f);

        public override void Enter()
        {
            base.Enter();

            // AI with ball: show only the can-pass icon as possession indicator.
            Owner.SetCanPassPreviewVisible(true);

            //set the range
            maxNumOfTries = Random.Range(1, 5);
            maxPassTime = Random.Range(rangePassTime.Min, rangePassTime.Max);

            //set the steering
            Owner.RPGMovement.SetMoveTarget(Owner.OppGoal.transform.position);
            Owner.RPGMovement.SetRotateFacePosition(Owner.OppGoal.transform.position);
            Owner.RPGMovement.SetSteeringOn();
            Owner.RPGMovement.SetTrackingOn();
        }

        public override void Execute()
        {
            base.Execute();

            if (Owner.IsUserControlled)
            {
                Owner.ClearPendingKickCommand();
                Machine.ChangeState<ManualControl>();
                return;
            }

            bool isMoving = Owner.OppGoal != null
                && (Owner.OppGoal.transform.position - Owner.Position).sqrMagnitude > 0.25f;
            bool wantsSprint = Owner.EvaluateAISprintIntent(isMoving, Owner.IsThreatened());
            Owner.ApplySprintToMovement(wantsSprint, isMoving, 0.95f);

            //decrement time
            if(maxPassTime > 0)
                maxPassTime -= Time.deltaTime;
        }

        public override void ManualExecute()
        {
            base.ManualExecute();

            //set the steering
            Owner.RPGMovement.SetMoveTarget(Owner.OppGoal.transform.position);
            Owner.RPGMovement.SetRotateFacePosition(Owner.OppGoal.transform.position);

            if (Owner.CanScore())
            {
                //go to kick-ball state
                Owner.MarkAutomaticKickCommand(KickType.Shot);
                SuperMachine.ChangeState<KickBallMainState>();
            }
            else
            {
                // occasional long-range shots to make AI attack less linear
                float distanceToGoal = Vector3.Distance(Owner.OppGoal.Position, Owner.Position);
                bool isWithinLongShotRange = distanceToGoal <= Owner.DistanceShotMaxValid * 1.6f;
                if (isWithinLongShotRange)
                {
                    float longShotChance = distanceToGoal <= Owner.DistanceShotMaxValid ? 0.22f : 0.12f;
                    bool shouldTryLongShot = Random.value <= longShotChance;

                    if (shouldTryLongShot && Owner.CanScore(false, false))
                    {
                        Owner.MarkAutomaticKickCommand(KickType.Shot);
                        SuperMachine.ChangeState<KickBallMainState>();
                        return;
                    }
                }
            }

            if (SuperMachine.IsCurrentState<KickBallMainState>())
                return;

            if (maxPassTime <= 0 || Owner.IsThreatened())  //try passing if threatened or depleted wait time
            {
                // check if I still should consider pass safety
                bool considerPassSafety = true;// maxNumOfTries > 0;

                //start considering passing if wait -time is less than zero
                //find player to pass ball to if threatened or
                //has spend alot of time controlling the ball
                bool canPass = Owner.CanPass(considerPassSafety);
                if (!canPass)
                    canPass = Owner.CanPass(false);

                if (canPass)
                {
                    //go to kick-ball state
                    Owner.MarkAutomaticKickCommand(KickType.Pass);
                    SuperMachine.ChangeState<KickBallMainState>();
                }

                // decrement max num of tries
                if (maxNumOfTries > 0)
                    --maxNumOfTries;
            }
        }

        public override void Exit()
        {
            base.Exit();

            Owner.SetCanPassPreviewVisible(false);

            Owner.ResetSprintState(0.95f);

            //stop steering
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
