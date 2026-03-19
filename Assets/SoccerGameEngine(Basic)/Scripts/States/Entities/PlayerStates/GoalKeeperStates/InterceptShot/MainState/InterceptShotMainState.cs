using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.ControlBall.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.GoToHome.MainState;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.InterceptShot.MainState
{
    public class InterceptShotMainState : BState
    {
        const float GoalKeeperCatchRetryDelay = 0.15f;

        float timeOfBallToInterceptPoint;
        Vector3 _steerTarget;

        void UpdateGoalKeeperControlIcons(bool isNearOrHasBall)
        {
            Owner.SetCanPassPreviewVisible(isNearOrHasBall);

            if (Owner.IconUserControlled != null)
                Owner.IconUserControlled.SetActive(Owner.IsUserControlled && isNearOrHasBall);
        }

        public float  BallInitialVelocity { get; set; }
        public Vector3 BallInitialPosition { get; set; }
        public Vector3 ShotTarget { get; set; }

        public override void Enter()
        {
            base.Enter();

            UpdateGoalKeeperControlIcons(false);

            //find the point on the ball path to target that is orthogonal to player position
           _steerTarget = Owner.GetPointOrthogonalToLine(BallInitialPosition, 
               ShotTarget, 
               Owner.Position);
           _steerTarget = Owner.ClampGoalKeeperTargetToHomeRadius(_steerTarget);

            // calculate time of ball to intercept point
            timeOfBallToInterceptPoint = Owner.TimeToTarget(BallInitialPosition,
                ShotTarget,
                BallInitialVelocity,
                Ball.Instance.Friction);

            // add some noise to it
            timeOfBallToInterceptPoint += 0.5f;

            if (Vector3.Distance(_steerTarget, ShotTarget) >= 2f)
                Owner.RPGMovement.SetSteeringOn();

            // set the steering 
            Owner.RPGMovement.SetMoveTarget(_steerTarget);
            Owner.RPGMovement.SetRotateFacePosition(BallInitialPosition);
            Owner.RPGMovement.SetTrackingOn();

            LogGoalKeeperDebug("Enter InterceptShot -> target: " + _steerTarget + ", time: " + timeOfBallToInterceptPoint);
        }

        public override void Execute()
        {
            base.Execute();

            bool isNearOrHasBall = Ball.Instance.Owner == Owner || Owner.IsBallWithinControlableDistance();
            UpdateGoalKeeperControlIcons(isNearOrHasBall);

            // keep steering to target
            _steerTarget = Owner.ClampGoalKeeperTargetToHomeRadius(_steerTarget);
            Owner.RPGMovement.SetMoveTarget(_steerTarget);

            // decrement ball time
            timeOfBallToInterceptPoint -= Time.deltaTime;

            if(Vector3.Distance(_steerTarget, Owner.Position) <= 1f)
            {
                if (Owner.RPGMovement.Steer == true)
                    Owner.RPGMovement.SetSteeringOff();
            }

            // if time is exhausted then go to tend goal
            // if ball within control distance te deflect its path
            if (timeOfBallToInterceptPoint <= 0f)
            {
                LogGoalKeeperDebug("Intercept timeout -> TendGoal");
                SuperMachine.ChangeState<TendGoalMainState>();
            }
            else if (Owner.IsBallWithinControlableDistance()
                && Time.time >= Owner.GoalKeeperPickupBlockedUntil)
            {
                TryCatchBallAndControl();
            }
        }

        bool TryCatchBallAndControl()
        {
            if (Ball.Instance == null || Ball.Instance.Owner != null)
                return false;

            float ballSpeed = Ball.Instance.Rigidbody != null
                ? Ball.Instance.Rigidbody.velocity.magnitude
                : 0f;

            float catchChance = Owner.EvaluateGoalKeeperCatchChance(ballSpeed);
            bool isCaught = Owner.TryCatchBallAsGoalKeeper(ballSpeed);

            if (isCaught)
            {
                LogGoalKeeperDebug("Ball reached keeper control distance -> Caught (chance: "
                    + catchChance.ToString("0.00") + ", speed: " + ballSpeed.ToString("0.00")
                    + ") -> ControlBall");
                Machine.ChangeState<ControlBallMainState>();
                return true;
            }

            Owner.GoalKeeperPickupBlockedUntil = Mathf.Max(Owner.GoalKeeperPickupBlockedUntil,
                Time.time + GoalKeeperCatchRetryDelay);

            LogGoalKeeperDebug("Ball reached keeper control distance -> Missed catch (chance: "
                + catchChance.ToString("0.00") + ", speed: " + ballSpeed.ToString("0.00") + ")");

            return false;
        }

        public override void Exit()
        {
            base.Exit();

            UpdateGoalKeeperControlIcons(false);

            // reset steering
            Owner.RPGMovement.SetSteeringOff();
            Owner.RPGMovement.SetTrackingOff();

            LogGoalKeeperDebug("Exit InterceptShot");
        }

        void LogGoalKeeperDebug(string message)
        {
            if (MatchManager.Instance == null || !MatchManager.Instance.EnableGoalkeeperDebug)
                return;

            Debug.Log("[GK DEBUG] " + Owner.name + " :: " + message);
        }

        public Player Owner
        {
            get
            {
                return ((GoalKeeperFSM)SuperMachine).Owner;
            }
        }
    }
}
