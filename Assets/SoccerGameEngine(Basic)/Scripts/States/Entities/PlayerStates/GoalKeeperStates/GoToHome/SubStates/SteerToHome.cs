using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.GoToHome.SubStates
{
    public class SteerToHome : BState
    {
        /// <summary>
        /// The steering target
        /// </summary>
        public Vector3 SteeringTarget { get; set; }

        void UpdateMoveHomeAndFaceBall()
        {
            SteeringTarget = Owner.HomeRegion.position;

            Owner.RPGMovement.SetMoveTarget(SteeringTarget);

            Vector3 faceTarget = Ball.Instance != null
                ? Ball.Instance.NormalizedPosition
                : SteeringTarget;

            Owner.RPGMovement.SetRotateFacePosition(faceTarget);
        }

        public override void Enter()
        {
            base.Enter();

            // move home but keep head/body facing the ball while travelling.
            UpdateMoveHomeAndFaceBall();
            Owner.RPGMovement.SetSteeringOn();
            Owner.RPGMovement.SetTrackingOn();
        }


        public override void Execute()
        {
            base.Execute();

            UpdateMoveHomeAndFaceBall();

            //check if now at target and switch to wait for ball
            if (Owner.IsAtTarget(SteeringTarget))
                Machine.ChangeState<WaitAtHome>();
        }

        public override void ManualExecute()
        {
            base.ManualExecute();

            UpdateMoveHomeAndFaceBall();
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
