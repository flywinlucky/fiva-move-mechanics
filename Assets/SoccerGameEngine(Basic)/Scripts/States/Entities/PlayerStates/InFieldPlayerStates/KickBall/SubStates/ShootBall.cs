using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ControlBall.MainState;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.KickBall.SubStates
{
    public class ShootBall : BState
    {
        float _timeUntilBallRelease;
        bool _shotExecuted;

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

            Ball.Instance.Owner = Owner;
            Ball.Instance.Rigidbody.isKinematic = true;

            _timeUntilBallRelease = Owner.TriggerShotAnimationAndGetReleaseDelay(0.35f);
            _shotExecuted = false;
            Owner.BlockKickInputAfterKick();

            if (_timeUntilBallRelease <= 0.001f)
                _timeUntilBallRelease = 0.01f;
        }

        public override void Execute()
        {
            base.Execute();

            if (_shotExecuted)
                return;

            Owner.PlaceBallInfronOfMe();

            _timeUntilBallRelease -= Time.deltaTime;
            if (_timeUntilBallRelease > 0f)
                return;

            Ball.Instance.Owner = null;
            Ball.Instance.Rigidbody.isKinematic = false;

            //make a shot
            Owner.MakeShot(Ball.Instance.NormalizedPosition,
                (Vector3)Owner.KickTarget,
                Owner.KickPower,
                Owner.BallTime);

            _shotExecuted = true;

            //got to recover state
            Machine.ChangeState<RecoverFromKick>();
        }

        public override void Exit()
        {
            base.Exit();

            Ball.Instance.Owner = null;
            Ball.Instance.Rigidbody.isKinematic = false;
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
