using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.GoToHome.SubStates;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.GoToHome.MainState
{
    public class GoToHomeMainState : BHState
    {
        const float LooseBallAutoCollectDistance = 5f;

        bool IsLooseBallNearKeeper()
        {
            if (Ball.Instance == null)
                return false;

            if (Ball.Instance.Owner != null)
                return false;

            return Vector3.Distance(Owner.Position, Ball.Instance.NormalizedPosition) <= LooseBallAutoCollectDistance;
        }

        public override void AddStates()
        {
            base.AddStates();

            //add the states
            AddState<SteerToHome>();
            AddState<WaitAtHome>();

            //set the initial state
            SetInitialState<SteerToHome>();
        }

        public override void Enter()
        {
            base.Enter();
            LogGoalKeeperDebug("Enter GoToHome");
        }

        public override void Execute()
        {
            base.Execute();

            if (IsLooseBallNearKeeper())
            {
                LogGoalKeeperDebug("GoToHome -> TendGoal (loose ball near keeper)");
                SuperMachine.ChangeState<TendGoalMainState>();
            }
        }

        public override void ManualExecute()
        {
            base.ManualExecute();

            if (IsLooseBallNearKeeper())
            {
                LogGoalKeeperDebug("GoToHome manual -> TendGoal (loose ball near keeper)");
                SuperMachine.ChangeState<TendGoalMainState>();
                return;
            }

            // run logic depending on whether team is in control or not
            if (Owner.IsTeamInControl == false)
            {
                LogGoalKeeperDebug("GoToHome -> TendGoal (team lost control)");
                SuperMachine.ChangeState<TendGoalMainState>();
            }
        }

        public override void Exit()
        {
            base.Exit();
            LogGoalKeeperDebug("Exit GoToHome");
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
