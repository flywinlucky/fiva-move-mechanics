using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
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

        MatchDifficultyProfile GetDifficultyProfile()
        {
            if (MatchManager.Instance != null)
                return MatchManager.Instance.RuntimeDifficultyProfile;

            return new MatchDifficultyProfile
            {
                AIErrorChanceBase = 0.06f,
                AIPressureErrorBoost = 0.12f
            };
        }

        float ComputePressure01()
        {
            if (Owner.OppositionMembers == null)
                return 0f;

            int nearbyCount = 0;
            float pressureRadius = Mathf.Max(0.9f, Owner.DistanceThreatMax * 1.35f);
            float sqrRadius = pressureRadius * pressureRadius;

            for (int i = 0; i < Owner.OppositionMembers.Count; i++)
            {
                Player opponent = Owner.OppositionMembers[i];
                if (opponent == null)
                    continue;

                if ((opponent.Position - Owner.Position).sqrMagnitude <= sqrRadius)
                    nearbyCount++;
            }

            return Mathf.Clamp01(nearbyCount / 3f);
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

            Vector3 passTarget = (Vector3)Owner.KickTarget;
            float passPower = Owner.KickPower;

            if (!Owner.IsUserControlled)
            {
                MatchDifficultyProfile profile = GetDifficultyProfile();
                float pressure01 = ComputePressure01();
                float errorChance = Mathf.Clamp01(profile.AIErrorChanceBase + profile.AIPressureErrorBoost * pressure01);

                if (Random.value <= errorChance)
                {
                    Vector3 toTarget = passTarget - Owner.Position;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude <= 0.0001f)
                        toTarget = Owner.transform.forward;

                    float errorAngle = Random.Range(7f, 18f);
                    if (Random.value <= 0.5f)
                        errorAngle = -errorAngle;

                    Vector3 erroredDirection = Quaternion.Euler(0f, errorAngle, 0f) * toTarget.normalized;
                    float distance = toTarget.magnitude;
                    passTarget = Owner.Position + erroredDirection * distance;
                    passTarget.y = 0f;

                    passPower *= Random.Range(0.86f, 0.97f);
                }
            }

            //make a normal pass to the player
            Owner.MakePass(Ball.Instance.NormalizedPosition,
                passTarget,
                Owner.PassReceiver, 
                passPower,
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
