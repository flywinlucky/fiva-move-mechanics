using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.ControlBall.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.InterceptShot.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Triggers;
using RobustFSM.Base;
using RobustFSM.Interfaces;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.GoToHome.MainState
{
    /// <summary>
    /// The keeper tends/protects the goal from the opposition
    /// </summary>
    public class TendGoalMainState : BState
    {
        const float LooseBallAutoCollectDistance = 10f;

        int _goalLayerMask;
        float _timeSinceLastUpdate;
        float _lastPickupBlockedLogTime;
        Vector3 _steeringTarget;
        Vector3 _prevBallPosition;

        void UpdateGoalKeeperControlIcons(bool isNearOrHasBall)
        {
            Owner.SetCanPassPreviewVisible(isNearOrHasBall);

            if (Owner.IconUserControlled != null)
                Owner.IconUserControlled.SetActive(Owner.IsUserControlled && isNearOrHasBall);
        }

        public override void Enter()
        {
            base.Enter();

            _goalLayerMask = LayerMask.GetMask("GoalTrigger");

            //set some data
            _prevBallPosition = 1000 * Vector3.one;
            _timeSinceLastUpdate = 0f;
            _lastPickupBlockedLogTime = -10f;

            //set the rpg movement
            Owner.RPGMovement.SetSteeringOn();
            Owner.RPGMovement.SetTrackingOn();
            Owner.RPGMovement.Speed = Owner.TendGoalSpeed;

            UpdateGoalKeeperControlIcons(false);

            //register to some events
            Owner.OnShotTaken += Instance_OnShotTaken;

            LogGoalKeeperDebug("Enter TendGoal");
        }

        public override void Execute()
        {
            base.Execute();

            //get the entity positions
            Vector3 ballPosition = Ball.Instance.NormalizedPosition;

            bool isNearOrHasBall = Ball.Instance.Owner == Owner || Owner.IsBallWithinControlableDistance();
            UpdateGoalKeeperControlIcons(isNearOrHasBall);

            // catch any loose ball that enters keeper control radius
            bool isBallLoose = Ball.Instance.Owner == null;
            bool isBallInPickupRange = Owner.IsBallWithinControlableDistance();
            bool canPickupNow = Time.time >= Owner.GoalKeeperPickupBlockedUntil;
            float distanceToBall = Vector3.Distance(Owner.Position, ballPosition);

            // if loose ball is near the keeper, actively step to it and pick it up when possible
            bool shouldAutoCollectLooseBall = isBallLoose && distanceToBall <= LooseBallAutoCollectDistance;
            if (shouldAutoCollectLooseBall)
            {
                Owner.RPGMovement.SetRotateFacePosition(ballPosition);
                Vector3 collectTarget = Owner.ClampGoalKeeperTargetToHomeRadius(ballPosition);
                Owner.RPGMovement.SetMoveTarget(collectTarget);
                Owner.RPGMovement.Steer = Vector3.Distance(Owner.Position, collectTarget) >= 0.2f;

                if (isBallInPickupRange && canPickupNow)
                {
                    LogGoalKeeperDebug("Loose ball within " + LooseBallAutoCollectDistance + "m -> auto collect -> ControlBall");
                    Machine.ChangeState<ControlBallMainState>();
                    return;
                }

                if (isBallInPickupRange && !canPickupNow)
                {
                    if (Time.time - _lastPickupBlockedLogTime >= 0.2f)
                    {
                        float remaining = Owner.GoalKeeperPickupBlockedUntil - Time.time;
                        LogGoalKeeperDebug("Auto collect waiting for pickup block: " + Mathf.Max(0f, remaining).ToString("0.00") + "s");
                        _lastPickupBlockedLogTime = Time.time;
                    }
                }

                return;
            }

            if (isBallLoose && isBallInPickupRange && canPickupNow)
            {
                LogGoalKeeperDebug("Loose ball within control distance -> ControlBall");
                Machine.ChangeState<ControlBallMainState>();
                return;
            }

            if (isBallLoose && isBallInPickupRange && !canPickupNow)
            {
                if (Time.time - _lastPickupBlockedLogTime >= 0.2f)
                {
                    float remaining = Owner.GoalKeeperPickupBlockedUntil - Time.time;
                    LogGoalKeeperDebug("Pickup temporarily blocked after pass release: " + Mathf.Max(0f, remaining).ToString("0.00") + "s");
                    _lastPickupBlockedLogTime = Time.time;
                }
            }

            //set the look target
            Owner.RPGMovement.SetRotateFacePosition(ballPosition);

            //if I have exhausted my time then update the tend point
            if (_timeSinceLastUpdate <= 0f)
            {
                //do not continue if the ball didnt move
                if (_prevBallPosition != ballPosition)
                {
                    //cache the ball position
                    _prevBallPosition = ballPosition;

                    //run the logic for protecting the goal, find the position
                    Vector3 ballRelativePosToGoal = Owner.TeamGoal.transform.InverseTransformPoint(ballPosition);
                    ballRelativePosToGoal.z = Owner.TendGoalDistance;
                    ballRelativePosToGoal.x /= 3f;
                    ballRelativePosToGoal.x = Mathf.Clamp(ballRelativePosToGoal.x, -2.14f, 2.14f);
                    _steeringTarget = Owner.TeamGoal.transform.TransformPoint(ballRelativePosToGoal);
                    _steeringTarget = Owner.ClampGoalKeeperTargetToHomeRadius(_steeringTarget);

                    //add some noise to the target
                    float limit = 1f - Owner.GoalKeeping;
                    _steeringTarget.x += Random.Range(-limit, limit);
                    _steeringTarget.z += Random.Range(-limit, limit);
                }

                //reset the time 
                _timeSinceLastUpdate = 2f * (1f - Owner.GoalKeeping);
                if (_timeSinceLastUpdate == 0f)
                    _timeSinceLastUpdate = 2f * 0.1f;
            }

            //decrement the time
            _timeSinceLastUpdate -= Time.deltaTime;

            //set the ability to steer here
            Owner.RPGMovement.Steer = Vector3.Distance(Owner.Position, _steeringTarget) >= 1f;
            Owner.RPGMovement.SetMoveTarget(Owner.ClampGoalKeeperTargetToHomeRadius(_steeringTarget));
        }

        public override void ManualExecute()
        {
            base.ManualExecute();

            bool isLooseBallNearKeeper = Ball.Instance.Owner == null
                && Vector3.Distance(Owner.Position, Ball.Instance.NormalizedPosition) <= LooseBallAutoCollectDistance;

            // run logic depending on whether team is in control or not
            if (Owner.IsTeamInControl == true && !isLooseBallNearKeeper)
            {
                LogGoalKeeperDebug("Team in control while TendGoal -> GoToHome");
                SuperMachine.ChangeState<GoToHomeMainState>();
            }
        }

        public override void Exit()
        {
            base.Exit();

            UpdateGoalKeeperControlIcons(false);

            Owner.RPGMovement.SetTrackingOff();

            //deregister to some events
            Owner.OnShotTaken -= Instance_OnShotTaken;
        }

        private void Instance_OnShotTaken(float flightTime, float velocity, Vector3 initial, Vector3 target)
        {
            // get the direction to target
            Vector3 direction = target - initial;

            // make a raycast and test if it hits target
            RaycastHit hitInfo;
            bool willBallHitAGoal = Physics.SphereCast(Ball.Instance.NormalizedPosition + Vector3.up,
                        Ball.Instance.SphereCollider.radius,
                        direction,
                        out hitInfo,
                        300,
                        _goalLayerMask);

            LogGoalKeeperDebug("OnShotTaken event -> willHitGoal: " + willBallHitAGoal);
            
            // get the goal from the goal trigger
            if (willBallHitAGoal)
            {
                // get the goal
                Goal goal = hitInfo.transform.GetComponent<GoalTrigger>().Goal;

                // check if shot is on target
                bool isShotOnTarget = goal == Owner.TeamGoal;
                LogGoalKeeperDebug("Shot goal test -> isShotOnTarget: " + isShotOnTarget);

                if (isShotOnTarget == true)
                {
                    // init the intercept shot state
                    InterceptShotMainState interceptShotState = Machine.GetState<InterceptShotMainState>();
                    interceptShotState.BallInitialPosition = initial;
                    interceptShotState.BallInitialVelocity = velocity;
                    interceptShotState.ShotTarget = target;

                    // trigger state change
                    LogGoalKeeperDebug("Transition -> InterceptShot");
                    Machine.ChangeState<InterceptShotMainState>();
                }
            }
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
