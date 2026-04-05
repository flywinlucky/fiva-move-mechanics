using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.GoToHome.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.Wait.MainState
{
    public class WaitMainState : BState
    {
        const float LooseBallAutoCollectDistance = 10f;

        bool IsLooseBallNearKeeper()
        {
            if (Ball.Instance == null)
                return false;

            if (Ball.Instance.Owner != null)
                return false;

            return Vector3.Distance(Owner.Position, Ball.Instance.NormalizedPosition) <= LooseBallAutoCollectDistance;
        }

        bool IsMatchStoppedForReset()
        {
            if (MatchManager.Instance == null)
                return false;

            MatchStatuses status = MatchManager.Instance.MatchStatus;
            return status == MatchStatuses.GoalScored;
        }

        public override void Enter()
        {
            base.Enter();

            // stop steering
            Owner.RPGMovement.SetSteeringOff();
            if (IsMatchStoppedForReset())
                Owner.RPGMovement.SetTrackingOff();
            else
                Owner.RPGMovement.SetTrackingOn();

            //listen to variaus events
            Owner.OnInstructedToGoToHome += Instance_OnInstructedToGoToHome;

            LogGoalKeeperDebug("Enter Wait");
        }

        public override void Execute()
        {
            base.Execute();

            if (IsMatchStoppedForReset())
            {
                if (Owner.RPGMovement.Track)
                    Owner.RPGMovement.SetTrackingOff();

                return;
            }

            // keep the keeper facing the ball while waiting.
            Owner.RPGMovement.SetRotateFacePosition(Ball.Instance.NormalizedPosition);

            if (IsLooseBallNearKeeper())
            {
                LogGoalKeeperDebug("Wait -> TendGoal (loose ball near keeper)");
                Machine.ChangeState<TendGoalMainState>();
            }
        }

        public override void Exit()
        {
            base.Exit();

            Owner.RPGMovement.SetTrackingOff();

            //stop listening to variaus events
            Owner.OnInstructedToGoToHome -= Instance_OnInstructedToGoToHome;

            LogGoalKeeperDebug("Exit Wait");
        }

        public void Instance_OnInstructedToGoToHome()
        {
            LogGoalKeeperDebug("Wait -> TendGoal (OnInstructedToGoToHome)");
            Machine.ChangeState<TendGoalMainState>();
        }

        void LogGoalKeeperDebug(string message)
        {
            if (MatchManager.Instance == null || !MatchManager.Instance.EnableGoalkeeperDebug)
                return;

            //Debug.Log("[GK DEBUG] " + Owner.name + " :: " + message);
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
