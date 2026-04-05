using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.GoToHome.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.TakeKickOff.MainState
{
    /// <summary>
    /// Player takes the kick-off and broadcasts that he has done so
    /// </summary>
    public class TakeKickOffMainState : BState
    {
        const float PreferredKickOffPassDistance = 20f;
        const float PreferredKickOffPassTolerance = 3.5f;

        public override void Enter()
        {
            base.Enter();

            //get a player to pass to
            Player receiver = SelectKickOffReceiverAtPreferredDistance();
            if (receiver == null)
            {
                float kickoffPassRadius = Mathf.Max(20f, Owner.DistancePassMax * 1.5f);
                receiver = Owner.GetRandomTeamMemberInRadius(kickoffPassRadius);
            }

            if (receiver == null)
            {
                //broadcast that I have taken kick-off
                ActionUtility.Invoke_Action(Owner.OnTakeKickOff);

                //go to home state
                Machine.ChangeState<GoToHomeMainState>();
                return;
            }

            //find the power to target
            float power = Owner.FindPower(Ball.Instance.NormalizedPosition,
                receiver.Position,
                Owner.BallPassArriveVelocity,
                Ball.Instance.Friction);

            //clamp the power
            power = Mathf.Clamp(power, 0.5f, Owner.ActualPower);

            float time = Owner.TimeToTarget(Ball.Instance.Position,
                receiver.Position,
                power,
                Ball.Instance.Friction);

            if (time <= 0f || float.IsNaN(time) || float.IsInfinity(time))
                time = 0.55f;

            //make a normal pass to the player
            Owner.MakePass(Ball.Instance.NormalizedPosition,
                receiver.Position,
                receiver,
                power, 
                time);

            ////broadcast that I have taken kick-off
            ActionUtility.Invoke_Action(Owner.OnTakeKickOff);

            //go to home state
            Machine.ChangeState<GoToHomeMainState>();
        }

        Player SelectKickOffReceiverAtPreferredDistance()
        {
            if (Owner.TeamMembers == null || Owner.TeamMembers.Count == 0)
                return null;

            float minDistance = Mathf.Max(4f, PreferredKickOffPassDistance - PreferredKickOffPassTolerance);
            float maxDistance = PreferredKickOffPassDistance + PreferredKickOffPassTolerance;

            Player selectedInBand = null;
            int inBandCount = 0;

            Player closestToPreferred = null;
            float closestDistanceDelta = float.MaxValue;

            foreach (Player mate in Owner.TeamMembers)
            {
                if (mate == null || mate == Owner)
                    continue;

                float distance = Vector3.Distance(Owner.Position, mate.Position);
                float delta = Mathf.Abs(distance - PreferredKickOffPassDistance);

                if (distance >= minDistance && distance <= maxDistance)
                {
                    inBandCount++;

                    // Reservoir sampling gives random pick among valid mid-distance players.
                    if (Random.Range(0, inBandCount) == 0)
                        selectedInBand = mate;
                }

                if (delta < closestDistanceDelta)
                {
                    closestDistanceDelta = delta;
                    closestToPreferred = mate;
                }
            }

            return selectedInBand != null ? selectedInBand : closestToPreferred;
        }

        public override void Exit()
        {
            base.Exit();
        }

        public Player Owner
        {
            get
            {
                return ((InFieldPlayerFSM)SuperMachine).Owner;
            }
        }
    }
}
