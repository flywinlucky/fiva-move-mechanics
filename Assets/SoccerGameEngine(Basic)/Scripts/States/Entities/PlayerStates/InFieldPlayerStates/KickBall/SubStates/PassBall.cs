using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ControlBall.MainState;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.KickBall.SubStates
{
    public class PassBall : BState
    {
        float _timeUntilBallRelease;
        bool _passExecuted;

        public override void Enter()
        {
            base.Enter();

            bool requiresManualCommand = Owner.IsUserControlled;
            bool hasMatchingKickCommand = requiresManualCommand
                ? Owner.PendingKickSource == Player.KickCommandSource.Manual
                : Owner.PendingKickSource == Player.KickCommandSource.Automatic;
            if (!hasMatchingKickCommand)
            {
                Owner.ClearPendingKickCommand();
                SuperMachine.ChangeState<ControlBallMainState>();
                return;
            }

            if (Owner.KickTarget == null)
            {
                Machine.ChangeState<RecoverFromKick>();
                return;
            }

            if (Owner.PassReceiver == null)
            {
                Owner.PassReceiver = Owner.GetRandomTeamMemberInRadius(Mathf.Max(20f, Owner.DistancePassMax * 1.5f));
            }

            // set the prev pass receiver
            Owner.PrevPassReceiver = Owner.PassReceiver;

            // Hold ball while pass animation starts and release at configured normalized time.
            Ball.Instance.Owner = Owner;
            Ball.Instance.Rigidbody.isKinematic = true;

            _timeUntilBallRelease = Owner.TriggerPassAnimationAndGetReleaseDelay(0.5f);
            _passExecuted = false;
            Owner.BlockKickInputAfterKick();

            if (_timeUntilBallRelease <= 0.001f)
                _timeUntilBallRelease = 0.01f;
        }

        public override void Execute()
        {
            base.Execute();

            if (_passExecuted)
                return;

            // Keep the ball attached to the foot until kick release point.
            Owner.PlaceBallInfronOfMe();

            _timeUntilBallRelease -= Time.deltaTime;
            if (_timeUntilBallRelease > 0f)
                return;

            Ball.Instance.Owner = null;
            Ball.Instance.Rigidbody.isKinematic = false;

            //make a normal pass to the player
            Owner.MakePass(Ball.Instance.NormalizedPosition,
                (Vector3)Owner.KickTarget,
                Owner.PassReceiver, 
                Owner.KickPower,
                Owner.BallTime);

            _passExecuted = true;

            //go to recover state
            Machine.ChangeState<RecoverFromKick>();
        }

        public override void Exit()
        {
            base.Exit();

            // reset the ball owner
            Ball.Instance.Owner = null;
            Owner.ClearPendingKickCommand();
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
