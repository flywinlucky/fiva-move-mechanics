using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using RobustFSM.Base;
using System.Linq;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.Team.KickOff.SubStates
{
    public class PrepareForKickOff : BState
    {
        public override void Enter()
        {
            base.Enter();

            // set player current psotion to kick-off position
            SetPlayerCurrentHomePositionToKickOffPosition();

            //place every player on the kick off position
            PlaceEveryPlayerAtKickOffPosition();

            //go to the next state
            if (Owner.HasKickOff)
            {
                //comment to follow kickoff procedure
                PlaceKickOffTakerAtTakeKickOffPosition();
                Machine.ChangeState<TakeKickOff>();
            }
            else
                Machine.ChangeState<WaitForKickOff>();
        }

        void PlaceEveryPlayerAtKickOffPosition()
        {
            Owner.Players.ForEach(tM =>
            {
                tM.Player.Position = tM.CurrentHomePosition.position;
                tM.Player.Rotation = tM.KickOffHomePosition.rotation;
            });
        }

        TeamPlayer GetKickOffTaker()
        {
            if (Owner.Players == null || Owner.Players.Count == 0)
                return null;

            Vector3 centerSpot = Pitch.Instance != null && Pitch.Instance.CenterSpot != null
                ? Pitch.Instance.CenterSpot.position
                : Vector3.zero;

            // Prefer an in-field player for kickoff. Picking a goalkeeper can deadlock kickoff flow.
            TeamPlayer inFieldPlayer = Owner.Players
                .Where(tM => tM != null
                    && tM.Player != null
                    && tM.Player.PlayerType == PlayerTypes.InFieldPlayer)
                .OrderBy(tM => Vector3.Distance(tM.Player.Position, centerSpot))
                .FirstOrDefault();

            if (inFieldPlayer != null)
                return inFieldPlayer;

            return Owner.Players
                .Where(tM => tM != null && tM.Player != null)
                .LastOrDefault();
        }

        void PlaceKickOffTakerAtTakeKickOffPosition()
        {
            TeamPlayer teamPlayer = GetKickOffTaker();
            if (teamPlayer == null || teamPlayer.Player == null)
                return;

            //get the take kick of state and set the controlling player
            Machine.GetState<TakeKickOff>().ControllingPlayer = teamPlayer;

            if (Pitch.Instance == null || Pitch.Instance.CenterSpot == null)
            {
                teamPlayer.Player.HomeRegion = teamPlayer.CurrentHomePosition;
                return;
            }

            //place player a kick off position
            teamPlayer.CurrentHomePosition.position = Pitch.Instance.CenterSpot.position + (Owner.Goal.transform.forward * (teamPlayer.Player.BallControlDistance + teamPlayer.Player.Radius));
            teamPlayer.Player.transform.position = teamPlayer.CurrentHomePosition.position;
            Owner.KickOffRefDirection.position = teamPlayer.Player.transform.position;
            teamPlayer.Player.HomeRegion = Owner.KickOffRefDirection;

            // rotate the player to face the ball
            teamPlayer.Player.transform.rotation = Owner.KickOffRefDirection.rotation;
        }

        void SetPlayerCurrentHomePositionToKickOffPosition()
        {
            Owner.Players.ForEach(tM => tM.CurrentHomePosition.transform.position = tM.KickOffHomePosition.transform.position);
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
