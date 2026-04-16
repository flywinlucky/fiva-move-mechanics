using System;
using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.Attack.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.Defend.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.KickOff.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Managers.MatchManagerMainState.MatchOn.SubStates
{
    /// <summary>
    /// Waits for the kick-off event to be raised by either of the teams
    /// If the kickoff event is raised then it triggers a state change to
    /// the exhaust first half state
    /// </summary>
    public class WaitForKickOffToComplete : BState
    {
        const float KickOffRecoveryTimeout = 8f;

        bool hasInvokedKickOffEvent;
        float kickOffRecoveryTime;
        bool _kickOffFlowStarted;
        bool _isListeningKickOffEvents;
        float _preKickOffDelayRemaining;

        public override void Enter()
        {
            base.Enter();

            // set hasn't invoked kick off event
            hasInvokedKickOffEvent = false;
            kickOffRecoveryTime = KickOffRecoveryTimeout;
            _kickOffFlowStarted = false;
            _isListeningKickOffEvents = false;
            _preKickOffDelayRemaining = 0f;

            // hard reset to kickoff flow so every goal restarts from center positions
            ResetToKickOffRoundStart();

            if (_preKickOffDelayRemaining <= 0f)
                StartKickOffFlow();
        }

        public override void Execute()
        {
            base.Execute();

            if (!_kickOffFlowStarted)
            {
                if (_preKickOffDelayRemaining > 0f)
                {
                    _preKickOffDelayRemaining -= Time.deltaTime;
                    if (_preKickOffDelayRemaining > 0f)
                        return;
                }

                StartKickOffFlow();
            }

            if (hasInvokedKickOffEvent == false)
            {
                kickOffRecoveryTime -= Time.deltaTime;
                if (kickOffRecoveryTime <= 0f)
                    ForceKickOffRecovery();
            }
        }

        public override void Exit()
        {
            base.Exit();

            //stop listening to team OnInit events
            StopListeningKickOffEvents();
        }

        private void Instance_OnTeamTakeKickOff()
        {
            if (!_kickOffFlowStarted)
                return;

            hasInvokedKickOffEvent = true;
            Owner.MatchStatus = MatchStatuses.HalfExhausted;
            Machine.ChangeState<ExhaustHalf>();
        }

        void ResetToKickOffRoundStart()
        {
            if (Ball.Instance != null)
            {
                Ball.Instance.Owner = null;
                Ball.Instance.Rigidbody.isKinematic = false;
                Ball.Instance.Trap();
                Ball.Instance.Position = Owner.TransformCentreSpot.position;
            }

            PrepareTeamForKickOffStaging(Owner.TeamAway);
            PrepareTeamForKickOffStaging(Owner.TeamHome);

            _preKickOffDelayRemaining = 0f;
            if (!IsOpeningKickOff())
                return;

            RoundTeamsResuldInWord panel = RoundTeamsResuldInWord.Instance;
            if (panel == null)
                panel = UnityEngine.Object.FindObjectOfType<RoundTeamsResuldInWord>();

            if (panel != null)
            {
                panel.PlayKickOffPanel();
                _preKickOffDelayRemaining = Mathf.Max(0f, panel.KickOffPanelTotalDuration);
            }
        }

        bool IsOpeningKickOff()
        {
            if (Owner == null)
                return false;

            if (Owner.CurrentHalf != 1)
                return false;

            if (Owner.MatchStatus == MatchStatuses.GoalScored)
                return false;

            if (TimeManager.Instance == null)
                return true;

            return TimeManager.Instance.Minutes <= 0 && TimeManager.Instance.Seconds <= 0;
        }

        void StartKickOffFlow()
        {
            if (_kickOffFlowStarted)
                return;

            _kickOffFlowStarted = true;

            //listen to team OnTakeKickOff events
            if (Owner.TeamAway != null)
                Owner.TeamAway.OnTakeKickOff += Instance_OnTeamTakeKickOff;

            if (Owner.TeamHome != null)
                Owner.TeamHome.OnTakeKickOff += Instance_OnTeamTakeKickOff;

            _isListeningKickOffEvents = true;

            PrepareTeamForKickOff(Owner.TeamAway);
            PrepareTeamForKickOff(Owner.TeamHome);
        }

        void PrepareTeamForKickOffStaging(Team team)
        {
            if (team == null)
                return;

            team.ControllingPlayer = null;
            ActionUtility.Invoke_Action(team.OnInstructPlayersToWait);
        }

        void StopListeningKickOffEvents()
        {
            if (!_isListeningKickOffEvents)
                return;

            if (Owner.TeamAway != null)
                Owner.TeamAway.OnTakeKickOff -= Instance_OnTeamTakeKickOff;

            if (Owner.TeamHome != null)
                Owner.TeamHome.OnTakeKickOff -= Instance_OnTeamTakeKickOff;

            _isListeningKickOffEvents = false;
        }

        void PrepareTeamForKickOff(Team team)
        {
            if (team == null)
                return;

            team.ControllingPlayer = null;

            // force players into a neutral state before kickoff placement
            ActionUtility.Invoke_Action(team.OnInstructPlayersToWait);

            if (team.FSM != null)
                team.FSM.ChangeState<KickOffMainState>();
        }

        void ForceKickOffRecovery()
        {
            hasInvokedKickOffEvent = true;

            if (Ball.Instance != null)
            {
                Ball.Instance.Owner = null;
                Ball.Instance.Rigidbody.isKinematic = false;
                Ball.Instance.Trap();
                Ball.Instance.Position = Owner.TransformCentreSpot.position;
            }

            Team kickOffTeam = null;
            if (Owner.TeamAway != null && Owner.TeamAway.HasKickOff)
                kickOffTeam = Owner.TeamAway;
            else if (Owner.TeamHome != null && Owner.TeamHome.HasKickOff)
                kickOffTeam = Owner.TeamHome;
            else
                kickOffTeam = Owner.TeamAway != null ? Owner.TeamAway : Owner.TeamHome;

            Team defendTeam = kickOffTeam == Owner.TeamAway ? Owner.TeamHome : Owner.TeamAway;

            if (kickOffTeam != null && kickOffTeam.FSM != null)
                kickOffTeam.FSM.ChangeState<AttackMainState>();

            if (defendTeam != null && defendTeam.FSM != null)
                defendTeam.FSM.ChangeState<DefendMainState>();

            Machine.ChangeState<ExhaustHalf>();

#if UNITY_EDITOR
            Debug.LogWarning("Kickoff recovery fallback triggered to prevent match deadlock.");
#endif
        }

        public MatchManager Owner
        {
            get
            {
                return ((MatchManagerFSM)SuperMachine).Owner;
            }
        }
    }
}
