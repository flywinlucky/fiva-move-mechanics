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
        const float UserShotDistanceNear = 10f;
        const float UserShotDistanceFar = 34f;

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

        float ComputeShotPressure01()
        {
            if (Owner.OppositionMembers == null)
                return 0f;

            int nearbyOpponents = 0;
            float pressureRadius = Mathf.Max(1.4f, Owner.DistanceThreatMax * 1.15f);
            float sqrRadius = pressureRadius * pressureRadius;

            for (int i = 0; i < Owner.OppositionMembers.Count; i++)
            {
                Player opponent = Owner.OppositionMembers[i];
                if (opponent == null)
                    continue;

                if ((opponent.Position - Owner.Position).sqrMagnitude <= sqrRadius)
                    nearbyOpponents++;
            }

            return Mathf.Clamp01(nearbyOpponents / 3f);
        }

        Vector3 ApplyHardMissOffset(Vector3 target, float distance01, float challenge01)
        {
            bool highMiss = Random.value <= Mathf.Lerp(0.34f, 0.68f, distance01);
            if (highMiss)
            {
                target += Vector3.up * Random.Range(1.2f, 2.9f + challenge01 * 0.5f);
                return target;
            }

            float sideSign = Random.value < 0.5f ? -1f : 1f;
            float sideError = Random.Range(1.1f, 2.6f + challenge01 * 0.4f);
            target += Owner.OppGoal.transform.right * sideSign * sideError;
            target += Vector3.up * Random.Range(-0.2f, 0.9f);
            return target;
        }

        Vector3 ResolveUserShotTarget(Vector3 defaultTarget)
        {
            if (!Owner.IsUserControlled)
                return defaultTarget;

            if (MatchManager.Instance == null || Owner.OppGoal == null)
                return defaultTarget;

            MatchDifficultyProfile profile = MatchManager.Instance.RuntimeDifficultyProfile;
            float distanceToGoal = Vector3.Distance(Owner.Position, Owner.OppGoal.Position);
            float distance01 = Mathf.InverseLerp(UserShotDistanceNear, UserShotDistanceFar, distanceToGoal);
            float pressure01 = ComputeShotPressure01();
            float assist01 = MatchManager.Instance.DdaAssistStrength;
            float challenge01 = 1f - assist01;
            float performance01 = MatchManager.Instance.PlayerPerformanceScore;

            float errorRadius = profile.UserShotErrorBase + (profile.UserShotErrorLongRange * distance01);
            errorRadius += pressure01 * 0.45f;
            errorRadius += Mathf.Clamp01(performance01 - 0.55f) * 0.55f;
            errorRadius -= assist01 * 0.25f;
            errorRadius = Mathf.Clamp(errorRadius, 0.02f, 4.8f);

            Vector2 jitter = Random.insideUnitCircle * errorRadius;
            Vector3 target = defaultTarget;
            target += Owner.OppGoal.transform.right * jitter.x;
            target += Vector3.up * (jitter.y * 0.62f);

            float missChance = Mathf.Lerp(profile.UserShotMissChanceBase, profile.UserShotMissChanceLongRange, distance01);
            missChance += pressure01 * 0.12f;
            missChance += Mathf.Clamp01(performance01 - 0.62f) * 0.08f;
            missChance -= assist01 * 0.12f;
            missChance = Mathf.Clamp01(missChance);

            if (Random.value <= missChance)
                target = ApplyHardMissOffset(target, distance01, challenge01);

            return target;
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

            Vector3 baseTarget = (Vector3)Owner.KickTarget;
            Vector3 resolvedTarget = Owner.IsUserControlled
                ? ResolveUserShotTarget(baseTarget)
                : ResolveAiShotTarget(baseTarget);

            float resolvedBallTime = Owner.IsUserControlled
                ? Owner.BallTime
                : ResolveAiBallTime(Owner.BallTime);

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
