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
        const float InterceptSteerStopDistance = 0.35f;

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
        public bool ForceRebound { get; set; }
        public float SaveQuality { get; set; }

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

            if(Vector3.Distance(_steerTarget, Owner.Position) <= InterceptSteerStopDistance)
            {
                if (Owner.RPGMovement.Steer == true)
                    Owner.RPGMovement.SetSteeringOff();
            }

            // if time is exhausted then go to tend goal
            // if ball within control distance te deflect its path
            if (timeOfBallToInterceptPoint <= 0f)
            {
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
            float saveQualityChance = SaveQuality > 0f
                ? Mathf.Clamp(Mathf.Lerp(SaveQuality, 1f, 0.22f), 0.1f, 0.98f)
                : 0f;
            float catchAssist = ComputeInterceptCatchAssist(ballSpeed);
            float adjustedCatchChance = Mathf.Clamp(Mathf.Max(catchChance + catchAssist, saveQualityChance), 0.1f, 0.98f);
            bool isCaught = Random.value <= adjustedCatchChance;

            if (isCaught)
            {
                if (ForceRebound)
                {
                    TriggerRebound(ballSpeed);
                    Machine.ChangeState<TendGoalMainState>();
                    return true;
                }

                Machine.ChangeState<ControlBallMainState>();
                return true;
            }

            Owner.GoalKeeperPickupBlockedUntil = Mathf.Max(Owner.GoalKeeperPickupBlockedUntil,
                Time.time + GoalKeeperCatchRetryDelay);

            return false;
        }

        float ComputeInterceptCatchAssist(float ballSpeed)
        {
            float speedAssist = Mathf.Clamp01(ballSpeed / 20f) * 0.05f;
            float qualityAssist = Mathf.Clamp01(SaveQuality) * 0.08f;
            float skillAssist = Mathf.Clamp01(Owner.GoalKeeping) * 0.04f;

            return speedAssist + qualityAssist + skillAssist;
        }

        void TriggerRebound(float ballSpeed)
        {
            Ball.Instance.Owner = null;
            Ball.Instance.Rigidbody.isKinematic = false;

            Vector3 awayFromGoal = (Owner.Position - Owner.TeamGoal.Position).normalized;
            if (awayFromGoal.sqrMagnitude <= 0.0001f)
                awayFromGoal = Owner.transform.forward;

            Vector3 side = Vector3.Cross(Vector3.up, awayFromGoal).normalized;
            float sign = Random.value <= 0.5f ? -1f : 1f;
            bool userLongRangeSupport = IsUserKeeperLongRangeContext();
            float lateralAmount = userLongRangeSupport
                ? Random.Range(0.55f, 1.05f)
                : Random.Range(0.2f, 0.65f);
            float forwardAmount = userLongRangeSupport ? 0.45f : 1f;
            Vector3 direction = (awayFromGoal * forwardAmount + side * sign * lateralAmount).normalized;

            Vector3 target = Owner.Position + direction * Random.Range(4f, 10f);
            target.y = 0f;
            float power = Mathf.Max(3f, (ballSpeed * 0.5f) + Random.Range(2f, 4f));

            Ball.Instance.Kick(target, power);
            Owner.GoalKeeperPickupBlockedUntil = Mathf.Max(Owner.GoalKeeperPickupBlockedUntil, Time.time + GoalKeeperCatchRetryDelay);
        }

        bool IsUserKeeperLongRangeContext()
        {
            if (Owner.TeamMembers == null)
                return false;

            bool defendingUserSide = false;
            for (int i = 0; i < Owner.TeamMembers.Count; i++)
            {
                Player mate = Owner.TeamMembers[i];
                if (mate != null && mate.IsUserControlled)
                {
                    defendingUserSide = true;
                    break;
                }
            }

            if (!defendingUserSide)
                return false;

            return Vector3.Distance(BallInitialPosition, ShotTarget) >= 18f;
        }

        public override void Exit()
        {
            base.Exit();

            UpdateGoalKeeperControlIcons(false);

            // reset steering
            Owner.RPGMovement.SetSteeringOff();
            Owner.RPGMovement.SetTrackingOff();

            ForceRebound = false;
            SaveQuality = 0f;
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
