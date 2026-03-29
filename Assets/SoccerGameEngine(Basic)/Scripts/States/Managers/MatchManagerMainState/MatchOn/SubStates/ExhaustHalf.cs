using System;
using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Managers.MatchManagerMainState.Init.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Managers.MatchManagerMainState.MatchOver.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Managers.MatchManagerMainState.MatchStopped.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using RobustFSM.Base;
using RobustFSM.Interfaces;
using UnityEngine;
using static Assets.SoccerGameEngine_Basic_.Scripts.Managers.MatchManager;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Managers.MatchManagerMainState.MatchOn.SubStates
{
    public class ExhaustHalf : BState
    {
        Coroutine _executingCoroutine;
        bool _hasTriggeredMatchStop;

        public override void Enter()
        {
            base.Enter();

            _hasTriggeredMatchStop = false;

            //raise the match play start event
            ActionUtility.Invoke_Action(Owner.OnMatchPlayStart);

            // register the teams to goal score events
            Owner.TeamAway.Goal.OnCollideWithBall += Owner.TeamAway.OnOppScoredAGoal;
            Owner.TeamHome.Goal.OnCollideWithBall += Owner.TeamHome.OnOppScoredAGoal;

            Owner.TeamAway.Goal.OnCollideWithBall += Owner.TeamHome.OnTeamScoredAGoal;
            Owner.TeamHome.Goal.OnCollideWithBall += Owner.TeamAway.OnTeamScoredAGoal;

            //listen to the OnTick event of the Time Manager
            Owner.TeamAway.Goal.OnCollideWithBall += Instance_OnGoalScored;
            Owner.TeamHome.Goal.OnCollideWithBall += Instance_OnGoalScored;
            TimeManager.Instance.OnTick += Instance_TimeManagerOnTick;

            //start the counter
            _executingCoroutine = Owner.StartCoroutine(TimeManager.Instance.TickTime());
        }

        public override void Exit()
        {
            base.Exit();

            //raise the match play stop event
            ActionUtility.Invoke_Action(Owner.OnMatchPlayStop);

            // deregister the teams to goal score events
            Owner.TeamAway.Goal.OnCollideWithBall -= Owner.TeamAway.OnOppScoredAGoal;
            Owner.TeamHome.Goal.OnCollideWithBall -= Owner.TeamHome.OnOppScoredAGoal;

            // deregister the teams to goal score events
            Owner.TeamAway.Goal.OnCollideWithBall -= Owner.TeamHome.OnTeamScoredAGoal;
            Owner.TeamHome.Goal.OnCollideWithBall -= Owner.TeamAway.OnTeamScoredAGoal;

            //stop listening to the OnTick event of the Time Manager
            Owner.TeamAway.Goal.OnCollideWithBall -= Instance_OnGoalScored;
            Owner.TeamHome.Goal.OnCollideWithBall -= Instance_OnGoalScored;
            TimeManager.Instance.OnTick -= Instance_TimeManagerOnTick;

            //stop the counter
            Owner.StopCoroutine(_executingCoroutine);
        }

        private void Instance_OnGoalScored()
        {
            if (_hasTriggeredMatchStop)
                return;

            _hasTriggeredMatchStop = true;

            // Immediately freeze both teams so keepers don't chase/clear a scored ball from goal.
            Owner.MatchStatus = MatchStatuses.GoalScored;
            FreezePlayersForGoalReset();

            //prepare the text
            string info = string.Format("TeamA {0}-{1} TeamH", Owner.TeamAway.Goals, Owner.TeamHome.Goals);

            //invoke the goal-scored event
            GoalScored temp = Owner.OnGoalScored;
            if (temp != null) temp.Invoke(info);

            // trigger state change
            Machine.ChangeState<MatchStoppedMainState>();
        }

        void FreezePlayersForGoalReset()
        {
            if (Ball.Instance != null)
            {
                Ball.Instance.Owner = null;
                if (Ball.Instance.Rigidbody != null)
                    Ball.Instance.Rigidbody.isKinematic = false;
            }

            if (Owner.TeamAway != null)
            {
                Owner.TeamAway.ControllingPlayer = null;
                ActionUtility.Invoke_Action(Owner.TeamAway.OnInstructPlayersToWait);
            }

            if (Owner.TeamHome != null)
            {
                Owner.TeamHome.ControllingPlayer = null;
                ActionUtility.Invoke_Action(Owner.TeamHome.OnInstructPlayersToWait);
            }
        }

        private void Instance_TimeManagerOnTick(int minutes, int seconds)
        {
            if (_hasTriggeredMatchStop)
                return;

            int elapsedSeconds = (minutes * 60) + seconds;
            int remainingSeconds = Mathf.Max(0, Owner.RegulationDurationSeconds - elapsedSeconds);
            int remainingMinutes = remainingSeconds / 60;
            int remainingOnlySeconds = remainingSeconds % 60;

            //raise the on tick event of the match manager
            Tick temp = Owner.OnTick;
            if (temp != null) temp.Invoke(Owner.CurrentHalf, remainingMinutes, remainingOnlySeconds);

            //stop regulation at 00:00; sudden death has no time limit.
            if (!Owner.IsSuddenDeath && elapsedSeconds >= Owner.RegulationDurationSeconds)
            {
                _hasTriggeredMatchStop = true;

                // set the match status
                Owner.MatchStatus = MatchStatuses.HalfExhausted;

                // trigger state change
                Machine.ChangeState<MatchStoppedMainState>();
            }
        }

        /// <summary>
        /// Access the super state machine
        /// </summary>
        public IFSM SuperFSM
        {
            get
            {
                return (MatchManagerFSM)SuperMachine;
            }
        }

        /// <summary>
        /// Access the owner of the state machine
        /// </summary>
        public MatchManager Owner
        {
            get
            {
                return ((MatchManagerFSM)SuperMachine).Owner;
            }
        }
    }
}
