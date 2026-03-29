using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
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

        Vector3 ResolveAiShotTarget(Vector3 defaultTarget)
        {
            if (Owner.IsUserControlled)
                return defaultTarget;

            if (MatchManager.Instance == null || Owner.OppGoal == null)
                return defaultTarget;

            float distanceToGoal = Vector3.Distance(Owner.Position, Owner.OppGoal.Position);
            bool inLongShotWindow = distanceToGoal >= 18f && distanceToGoal <= 25f;
            if (!inLongShotWindow)
                return defaultTarget;

            Vector3 target = defaultTarget;

            // Distribution: center (safe), near-miss (post/crossbar), slower corner-like side bias.
            float roll = Random.value;
            bool forcePostHit = MatchManager.Instance.RegisterAIDangerousShotAndCheckPostTrigger();

            if (forcePostHit)
            {
                float horizontalSign = Random.value < 0.5f ? -1f : 1f;
                target += Vector3.right * horizontalSign * Random.Range(0.75f, 1.15f);
                target += Vector3.up * Random.Range(0.55f, 1.25f);
                MatchManager.Instance.NotifyAIPostHit(target);
                return target;
            }

            if (roll < 0.50f)
            {
                target += Vector3.up * Random.Range(-0.20f, 0.45f);
                return target;
            }

            if (roll < 0.75f)
            {
                float horizontalSign = Random.value < 0.5f ? -1f : 1f;
                target += Vector3.right * horizontalSign * Random.Range(0.70f, 1.05f);
                target += Vector3.up * Random.Range(0.45f, 1.05f);
                MatchManager.Instance.NotifyAIPostHit(target);
                return target;
            }

            target += Vector3.right * (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.35f, 0.70f);
            target += Vector3.up * Random.Range(0.05f, 0.55f);
            return target;
        }

        float ResolveAiBallTime(float defaultBallTime)
        {
            if (Owner.IsUserControlled)
                return defaultBallTime;

            if (MatchManager.Instance == null || Owner.OppGoal == null)
                return defaultBallTime;

            float distanceToGoal = Vector3.Distance(Owner.Position, Owner.OppGoal.Position);
            bool inLongShotWindow = distanceToGoal >= 18f && distanceToGoal <= 25f;
            if (!inLongShotWindow)
                return defaultBallTime;

            // Slightly longer travel time for cinematic long shots.
            return defaultBallTime * Random.Range(1.08f, 1.20f);
        }

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

            Vector3 resolvedTarget = ResolveAiShotTarget((Vector3)Owner.KickTarget);
            float resolvedBallTime = ResolveAiBallTime(Owner.BallTime);

            //make a shot
            Owner.MakeShot(Ball.Instance.NormalizedPosition,
                resolvedTarget,
                Owner.KickPower,
                resolvedBallTime);

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
