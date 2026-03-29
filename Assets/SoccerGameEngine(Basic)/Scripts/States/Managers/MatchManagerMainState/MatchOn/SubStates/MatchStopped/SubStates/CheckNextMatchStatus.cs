using System.Collections;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Managers.MatchManagerMainState.MatchOn.SubStates;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Managers.MatchManagerMainState.MatchOn.SubStates.MatchStopped.SubStates;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using RobustFSM.Base;
using RobustFSM.Interfaces;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Managers.MatchManagerMainState.MatchStopped.SubStates
{
    public class CheckNextMatchStatus : BState
    {
        const float SuddenDeathIntroDuration = 3f;
        Coroutine _suddenDeathDelayRoutine;

        public override void Enter()
        {
            base.Enter();

            // run the logic depending on match status
            if (Owner.MatchStatus == MatchStatuses.GoalScored)
            {
                if (Owner.IsSuddenDeath)
                    Machine.ChangeState<TriggerMatchOver>();
                else
                    Machine.ChangeState<BroadcastGoalScored>();
            }
            else if (Owner.MatchStatus == MatchStatuses.HalfExhausted)
            {
                if (!Owner.IsScoreDraw)
                {
                    Machine.ChangeState<TriggerMatchOver>();
                    return;
                }

                Owner.IsSuddenDeath = true;

                ActionUtility.Invoke_Action(Owner.OnEnterSuddenDeath);

#if UNITY_EDITOR
                Debug.Log("Regulation ended in a draw. Entering LAST GOAL TO WIN mode.");
#endif

                // Pause briefly so UI can explain the decisive round.
                _suddenDeathDelayRoutine = Owner.StartCoroutine(DelayedSuddenDeathKickOff());
            }
        }

        public override void Exit()
        {
            base.Exit();

            if (_suddenDeathDelayRoutine != null)
            {
                Owner.StopCoroutine(_suddenDeathDelayRoutine);
                _suddenDeathDelayRoutine = null;
            }
        }

        IEnumerator DelayedSuddenDeathKickOff()
        {
            yield return new WaitForSeconds(SuddenDeathIntroDuration);

            _suddenDeathDelayRoutine = null;

            // This state lives in MatchStopped sub-machine.
            // We must switch the parent machine back to kickoff flow.
            ((IState)Machine).Machine.ChangeState<WaitForKickOffToComplete>();
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
