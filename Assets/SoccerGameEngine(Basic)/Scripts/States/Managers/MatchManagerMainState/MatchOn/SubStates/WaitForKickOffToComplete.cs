using System;
using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.Attack.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.Defend.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.KickOff.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
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

        public override void Enter()
        {
            base.Enter();

            // set hasn't invoked kick off event
            hasInvokedKickOffEvent = false;
            kickOffRecoveryTime = KickOffRecoveryTimeout;

            // hard reset to kickoff flow so every goal restarts from center positions
            ResetToKickOffRoundStart();

            //listen to team OnTakeKickOff events
            Owner.TeamAway.OnTakeKickOff += Instance_OnTeamTakeKickOff;
            Owner.TeamHome.OnTakeKickOff += Instance_OnTeamTakeKickOff;
        }

        public override void Execute()
        {
            base.Execute();

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
            Owner.TeamAway.OnTakeKickOff -= Instance_OnTeamTakeKickOff;
            Owner.TeamHome.OnTakeKickOff -= Instance_OnTeamTakeKickOff;
        }

        private void Instance_OnTeamTakeKickOff()
        {
            hasInvokedKickOffEvent = true;
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

            PrepareTeamForKickOff(Owner.TeamAway);
            PrepareTeamForKickOff(Owner.TeamHome);
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
