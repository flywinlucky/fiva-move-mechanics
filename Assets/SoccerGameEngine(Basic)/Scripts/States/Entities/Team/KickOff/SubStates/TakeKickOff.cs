using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.TakeKickOff.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.Attack.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
using RobustFSM.Base;
using System;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.KickOff.SubStates
{
    public class TakeKickOff : BState
    {
        const float KickOffInstructionDelay = 0.2f;
        const float KickOffCompletionTimeout = 0.9f;
        const float PreferredKickOffPassDistance = 10f;
        const float PreferredKickOffPassTolerance = 3.5f;

        bool executed;
        bool _kickOffInstructionSent;
        float waitTime = KickOffInstructionDelay;
        float _completionTimeout;

        Action InstructPlayerToTakeKickOff;

        public TeamPlayer ControllingPlayer { get; set; }

        public override void Enter()
        {
            base.Enter();

            // set to unexecuted
            executed = false;
            _kickOffInstructionSent = false;
            waitTime = KickOffInstructionDelay + GetPostGoalKickOffDelay();
            _completionTimeout = KickOffCompletionTimeout;
            InstructPlayerToTakeKickOff = null;

            if (ControllingPlayer == null || ControllingPlayer.Player == null)
            {
                // Fallback: don't let kickoff flow stall if no valid controlling player is set.
                executed = true;
                ActionUtility.Invoke_Action(Owner.OnTakeKickOff);
                SuperMachine.ChangeState<AttackMainState>();
                return;
            }

            // uncomment to follow actual procedure in taking kick-off
            //// register player to listening to take-kickoff action
            ControllingPlayer.Player.OnTakeKickOff += Instance_OnPlayerTakeKickOff;
            InstructPlayerToTakeKickOff += ControllingPlayer.Player.Invoke_OnInstructedToTakeKickOff;

        }

        float GetPostGoalKickOffDelay()
        {
            MatchManager manager = MatchManager.Instance;
            if (manager == null)
                return 0f;

            if (manager.MatchStatus != Utilities.Enums.MatchStatuses.GoalScored)
                return 0f;

            return Mathf.Max(0f, manager.PostGoalRoundStartDelaySeconds);
        }

        public override void Execute()
        {
            base.Execute();

            // if not executed then run logic
            if (!executed)
            {
                if (!_kickOffInstructionSent)
                {
                    // decrement time
                    waitTime -= Time.deltaTime;

                    if (waitTime <= 0f)
                    {
                        _kickOffInstructionSent = true;
                        ForcePlayerKickOffInstruction();
                    }
                }
                else
                {
                    _completionTimeout -= Time.deltaTime;
                    if (_completionTimeout <= 0f)
                    {
                        Debug.LogWarning("Kickoff player did not respond in time. Forcing automatic kickoff pass.");
                        ForceKickOffPassFallback();
                    }
                }
            }
        }

        void ForcePlayerKickOffInstruction()
        {
            if (ControllingPlayer == null || ControllingPlayer.Player == null)
                return;

            Player player = ControllingPlayer.Player;
            if (player.InFieldPlayerFSM != null)
            {
                player.InFieldPlayerFSM.ChangeState<TakeKickOffMainState>();
                return;
            }

            ActionUtility.Invoke_Action(InstructPlayerToTakeKickOff);
        }

        void ForceKickOffPassFallback()
        {
            if (ControllingPlayer == null || ControllingPlayer.Player == null || Ball.Instance == null)
            {
                Instance_OnPlayerTakeKickOff();
                return;
            }

            Player kicker = ControllingPlayer.Player;
            Player receiver = SelectKickOffReceiverAtPreferredDistance(kicker);
            if (receiver == null)
                receiver = kicker.GetRandomTeamMemberInRadius(Mathf.Max(20f, kicker.DistancePassMax * 1.5f));

            if (receiver != null)
            {
                float power = kicker.FindPower(Ball.Instance.NormalizedPosition,
                    receiver.Position,
                    kicker.BallPassArriveVelocity,
                    Ball.Instance.Friction);
                power = Mathf.Clamp(power, 0.5f, kicker.ActualPower);

                float time = kicker.TimeToTarget(Ball.Instance.Position,
                    receiver.Position,
                    power,
                    Ball.Instance.Friction);
                if (time <= 0f || float.IsNaN(time) || float.IsInfinity(time))
                    time = 0.55f;

                kicker.MakePass(Ball.Instance.NormalizedPosition,
                    receiver.Position,
                    receiver,
                    power,
                    time);
            }

            Instance_OnPlayerTakeKickOff();
        }

        Player SelectKickOffReceiverAtPreferredDistance(Player kicker)
        {
            if (kicker == null || kicker.TeamMembers == null || kicker.TeamMembers.Count == 0)
                return null;

            float minDistance = Mathf.Max(4f, PreferredKickOffPassDistance - PreferredKickOffPassTolerance);
            float maxDistance = PreferredKickOffPassDistance + PreferredKickOffPassTolerance;

            Player best = null;
            float bestDistanceDelta = float.MaxValue;
            int inBandCount = 0;

            foreach (Player mate in kicker.TeamMembers)
            {
                if (mate == null || mate == kicker)
                    continue;

                float distance = Vector3.Distance(kicker.Position, mate.Position);
                float distanceDelta = Mathf.Abs(distance - PreferredKickOffPassDistance);

                if (distance >= minDistance && distance <= maxDistance)
                {
                    inBandCount++;

                    // Reservoir sampling for random selection inside preferred distance band.
                    if (UnityEngine.Random.Range(0, inBandCount) == 0)
                        best = mate;
                }

                if (best == null && distanceDelta < bestDistanceDelta)
                {
                    bestDistanceDelta = distanceDelta;
                    best = mate;
                }
            }

            return best;
        }

        public override void Exit()
        {
            base.Exit();

            // deregister player from listening to take-kickoff action
            if (ControllingPlayer != null && ControllingPlayer.Player != null)
            {
                ControllingPlayer.Player.OnTakeKickOff -= Instance_OnPlayerTakeKickOff;
                InstructPlayerToTakeKickOff -= ControllingPlayer.Player.Invoke_OnInstructedToTakeKickOff;

                // reset the home region of the player
                ControllingPlayer.Player.HomeRegion = ControllingPlayer.CurrentHomePosition;
            }

            InstructPlayerToTakeKickOff = null;
        }

        public void Instance_OnPlayerTakeKickOff()
        {
            // trigger state change to attack
            SuperMachine.ChangeState<AttackMainState>();

            //simply raise that I have taken the kick-off
            ActionUtility.Invoke_Action(Owner.OnTakeKickOff);
        }

        public Scripts.Entities.Team Owner
        {
            get
            {
                return ((TeamFSM)SuperMachine).Owner;
            }
        }
    }
}
