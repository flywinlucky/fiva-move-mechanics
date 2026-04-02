using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.KickBall.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Objects;
using RobustFSM.Base;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.InFieldPlayerStates.ControlBall.SubStates
{
    public class AutomaticControl : BState
    {
        int maxNumOfTries;
        float maxPassTime;
        Range rangePassTime = new Range(0.85f, 1.65f);
        MatchDifficultyProfile _difficultyProfile;
        float _nextDecisionTime;
        float _badTouchSlowUntil;
        float _longShotHesitationUntil;
        float _possessionElapsed;

        const float MinPossessionBeforeNormalPass = 1.15f;
        const float ForcedReleasePossessionSeconds = 4.5f;

        MatchDifficultyProfile GetDifficultyProfile()
        {
            if (MatchManager.Instance != null)
                return MatchManager.Instance.RuntimeDifficultyProfile;

            return new MatchDifficultyProfile
            {
                AIReactionDelayMin = 0.1f,
                AIReactionDelayMax = 0.22f,
                AIErrorChanceBase = 0.06f,
                AIPressureErrorBoost = 0.12f,
                AIDecisionHesitationChance = 0.08f,
                AIUnderPressureDribbleSlowdown = 0.9f,
                AIBadTouchChance = 0.08f
            };
        }

        bool IsDecisionTick()
        {
            if (Time.time < _nextDecisionTime)
                return false;

            float minDelay = Mathf.Max(0f, _difficultyProfile.AIReactionDelayMin);
            float maxDelay = Mathf.Max(minDelay, _difficultyProfile.AIReactionDelayMax);
            _nextDecisionTime = Time.time + Random.Range(minDelay, maxDelay);
            return true;
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

        bool IsShootingAtUserGoal()
        {
            if (Owner.OppGoal == null || Owner.OppositionMembers == null)
                return false;

            for (int i = 0; i < Owner.OppositionMembers.Count; i++)
            {
                Player opponent = Owner.OppositionMembers[i];
                if (opponent != null && opponent.IsUserControlled)
                    return true;
            }

            return false;
        }

        bool IsInMercyZone()
        {
            if (MatchManager.Instance == null || Owner.OppGoal == null)
                return false;

            return Vector3.Distance(Owner.Position, Owner.OppGoal.Position) <= MatchManager.Instance.MercyZoneRadius;
        }

        bool HasOpenLongShotLane()
        {
            if (Owner.OppGoal == null || Owner.OppositionMembers == null)
                return false;

            Vector3 from = Owner.Position;
            Vector3 to = Owner.OppGoal.ShotTargetReferencePoint;
            Vector3 segment = to - from;
            float segmentLength = segment.magnitude;
            if (segmentLength <= 0.001f)
                return false;

            Vector3 direction = segment / segmentLength;
            float blockRadius = 1.35f;

            for (int i = 0; i < Owner.OppositionMembers.Count; i++)
            {
                Player defender = Owner.OppositionMembers[i];
                if (defender == null || defender.PlayerType == PlayerTypes.Goalkeeper)
                    continue;

                Vector3 toDefender = defender.Position - from;
                float projection = Vector3.Dot(toDefender, direction);
                if (projection <= 1f || projection >= segmentLength)
                    continue;

                Vector3 nearest = from + (direction * projection);
                float distanceFromLane = Vector3.Distance(defender.Position, nearest);
                if (distanceFromLane <= blockRadius)
                    return false;
            }

            return true;
        }

        bool TryMercyPassOrReposition()
        {
            if (Owner.OppGoal == null)
                return false;

            Vector3 awayFromGoal = Owner.Position - Owner.OppGoal.Position;
            awayFromGoal.y = 0f;
            if (awayFromGoal.sqrMagnitude <= 0.0001f)
                awayFromGoal = -Owner.transform.forward;
            awayFromGoal.Normalize();

            Vector3 lateral = Vector3.Cross(Vector3.up, awayFromGoal).normalized;
            if (lateral.sqrMagnitude <= 0.0001f)
                lateral = Vector3.right;

            bool hasPass = Owner.CanPassInDirection(awayFromGoal, false)
                || Owner.CanPassInDirection((awayFromGoal + lateral).normalized, false)
                || Owner.CanPassInDirection((awayFromGoal - lateral).normalized, true);

            if (hasPass)
            {
                Owner.MarkAutomaticKickCommand(KickType.Pass);
                SuperMachine.ChangeState<KickBallMainState>();
                return true;
            }

            Vector3 dribbleOutTarget = Owner.Position + awayFromGoal * 3.5f + lateral * (((Owner.GetInstanceID() & 1) == 0) ? 1.4f : -1.4f);
            Owner.RPGMovement.SetMoveTarget(dribbleOutTarget);
            Owner.RPGMovement.SetRotateFacePosition(dribbleOutTarget);

            return false;
        }

        bool IsUnderRearUserPressure(float maxDistance = 2.4f)
        {
            if (Owner.OppositionMembers == null)
                return false;

            float sqrMaxDistance = Mathf.Max(0.5f, maxDistance) * Mathf.Max(0.5f, maxDistance);
            Vector3 ownerForward = Vector3.Scale(Owner.transform.forward, new Vector3(1f, 0f, 1f));
            if (ownerForward.sqrMagnitude <= 0.0001f)
                ownerForward = Vector3.forward;
            ownerForward.Normalize();

            for (int i = 0; i < Owner.OppositionMembers.Count; i++)
            {
                Player opponent = Owner.OppositionMembers[i];
                if (opponent == null || !opponent.IsUserControlled)
                    continue;

                Vector3 toOpponent = opponent.Position - Owner.Position;
                toOpponent.y = 0f;
                if (toOpponent.sqrMagnitude > sqrMaxDistance)
                    continue;

                float behindDot = Vector3.Dot(ownerForward, toOpponent.normalized);
                if (behindDot <= -0.1f)
                    return true;
            }

            return false;
        }

        Player GetBestForwardTeammate(float minGoalAdvantage = 2.5f)
        {
            if (Owner.TeamMembers == null || Owner.OppGoal == null)
                return null;

            float myDistanceToGoal = Vector3.Distance(Owner.Position, Owner.OppGoal.Position);
            float requiredAdvantage = Mathf.Max(0.75f, minGoalAdvantage);

            Player best = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < Owner.TeamMembers.Count; i++)
            {
                Player teammate = Owner.TeamMembers[i];
                if (teammate == null || teammate == Owner)
                    continue;

                if (teammate.PlayerType == PlayerTypes.Goalkeeper)
                    continue;

                float teammateDistance = Vector3.Distance(teammate.Position, Owner.OppGoal.Position);
                float goalGain = myDistanceToGoal - teammateDistance;
                if (goalGain < requiredAdvantage)
                    continue;

                float spacing = Vector3.Distance(teammate.Position, Owner.Position);
                float score = (goalGain * 1.25f) + (Mathf.Clamp(spacing, 0f, 18f) * 0.06f);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = teammate;
                }
            }

            return best;
        }

        bool TryTeamPlayPass(float pressure01, float distanceToGoal, bool forcePass = false)
        {
            bool rearPressure = IsUnderRearUserPressure();
            bool threatened = Owner.IsThreatened();
            Player bestForwardTeammate = GetBestForwardTeammate(distanceToGoal >= 20f ? 2.2f : 1.4f);
            bool hasBetterTeammateOption = bestForwardTeammate != null;
            bool highStealPressure = rearPressure || (threatened && pressure01 >= 0.55f);

            if (!forcePass && !highStealPressure && _possessionElapsed < MinPossessionBeforeNormalPass)
                return false;

            float passChance = 0.10f + (pressure01 * 0.26f);

            // Requested behavior: ~50% random pass when AI feels chased from behind.
            if (highStealPressure)
                passChance = Mathf.Max(passChance, 0.50f);

            if (threatened)
                passChance = Mathf.Max(passChance, 0.28f + (pressure01 * 0.25f));

            // Prevent ego dribbles from very far away when a better forward pass exists.
            if (distanceToGoal >= 16f && hasBetterTeammateOption)
                passChance = Mathf.Max(passChance, 0.35f + (pressure01 * 0.20f));
            else if (distanceToGoal >= 24f)
                passChance = Mathf.Max(passChance, 0.28f);

            if (_possessionElapsed >= 3.2f && hasBetterTeammateOption)
                passChance = Mathf.Max(passChance, 0.62f);

            if (forcePass)
                passChance = 1f;

            passChance = Mathf.Clamp01(passChance);
            if (passChance <= 0f || Random.value > passChance)
                return false;

            bool canPass = false;

            if (bestForwardTeammate != null)
            {
                Vector3 preferredDirection = bestForwardTeammate.Position - Owner.Position;
                preferredDirection.y = 0f;
                if (preferredDirection.sqrMagnitude > 0.0001f)
                    canPass = Owner.CanPassInDirection(preferredDirection.normalized, true);
            }

            if (!canPass)
                canPass = Owner.CanPass(true);

            if (!canPass)
                canPass = Owner.CanPass(false);

            if (!canPass && rearPressure)
            {
                Vector3 side = Vector3.Cross(Vector3.up, Owner.transform.forward).normalized;
                if (side.sqrMagnitude <= 0.0001f)
                    side = Vector3.right;

                canPass = Owner.CanPassInDirection(side, true) || Owner.CanPassInDirection(-side, true);
            }

            if (!canPass)
                return false;

            Owner.MarkAutomaticKickCommand(KickType.Pass);
            SuperMachine.ChangeState<KickBallMainState>();
            return true;
        }

        public override void Enter()
        {
            base.Enter();

            // AI with ball: show only the can-pass icon as possession indicator.
            Owner.SetCanPassPreviewVisible(true);

            //set the range
            maxNumOfTries = Random.Range(1, 5);
            maxPassTime = Random.Range(rangePassTime.Min, rangePassTime.Max);
            _difficultyProfile = GetDifficultyProfile();
            _nextDecisionTime = Time.time + Random.Range(_difficultyProfile.AIReactionDelayMin, _difficultyProfile.AIReactionDelayMax);
            _badTouchSlowUntil = 0f;
            _longShotHesitationUntil = 0f;
            _possessionElapsed = 0f;

            //set the steering
            Owner.RPGMovement.SetMoveTarget(Owner.OppGoal.transform.position);
            Owner.RPGMovement.SetRotateFacePosition(Owner.OppGoal.transform.position);
            Owner.RPGMovement.SetSteeringOn();
            Owner.RPGMovement.SetTrackingOn();
        }

        public override void Execute()
        {
            base.Execute();

            if (Owner.IsUserControlled)
            {
                Owner.ClearPendingKickCommand();
                Machine.ChangeState<ManualControl>();
                return;
            }

            _difficultyProfile = GetDifficultyProfile();
            bool decisionTick = IsDecisionTick();
            float pressure01 = ComputePressure01();
            _possessionElapsed += Time.deltaTime;

            if (decisionTick)
            {
                float badTouchChance = Mathf.Clamp01(_difficultyProfile.AIBadTouchChance + pressure01 * _difficultyProfile.AIPressureErrorBoost * 0.35f);
                if (Random.value <= badTouchChance)
                    _badTouchSlowUntil = Time.time + Random.Range(0.2f, 0.45f);
            }

            bool isMoving = Owner.OppGoal != null
                && (Owner.OppGoal.transform.position - Owner.Position).sqrMagnitude > 0.25f;
            bool wantsSprint = Owner.EvaluateAISprintIntent(isMoving, Owner.IsThreatened());
            float dribbleMultiplier = 0.95f;
            dribbleMultiplier *= Mathf.Lerp(1f, _difficultyProfile.AIUnderPressureDribbleSlowdown, pressure01);
            if (Time.time < _badTouchSlowUntil)
                dribbleMultiplier *= 0.84f;

            Owner.ApplySprintToMovement(wantsSprint, isMoving, dribbleMultiplier);

            //decrement time
            if(maxPassTime > 0)
                maxPassTime -= Time.deltaTime;
        }

        public override void ManualExecute()
        {
            base.ManualExecute();

            _difficultyProfile = GetDifficultyProfile();
            float pressure01 = ComputePressure01();
            float distanceToGoal = Owner.OppGoal != null
                ? Vector3.Distance(Owner.OppGoal.Position, Owner.Position)
                : 0f;

            bool mustReleaseBallNow = _possessionElapsed >= ForcedReleasePossessionSeconds
                && distanceToGoal >= 14f
                && GetBestForwardTeammate(1.2f) != null
                && (pressure01 >= 0.45f || Owner.IsThreatened());

            //set the steering
            Owner.RPGMovement.SetMoveTarget(Owner.OppGoal.transform.position);
            Owner.RPGMovement.SetRotateFacePosition(Owner.OppGoal.transform.position);

            if (TryTeamPlayPass(pressure01, distanceToGoal, mustReleaseBallNow))
                return;

            if (Owner.CanScore())
            {
                bool shootingAtUserGoal = IsShootingAtUserGoal();
                if (!(shootingAtUserGoal && IsInMercyZone()))
                {
                    //go to kick-ball state
                    Owner.MarkAutomaticKickCommand(KickType.Shot);
                    SuperMachine.ChangeState<KickBallMainState>();
                }
            }
            else
            {
                // occasional long-range shots to make AI attack less linear
                bool isWithinLongShotRange = distanceToGoal >= 18f && distanceToGoal <= 25f;
                if (isWithinLongShotRange)
                {
                    bool hasOpenLane = HasOpenLongShotLane();
                    float longShotChance = MatchManager.Instance != null ? MatchManager.Instance.LongShotProbability : 0.75f;
                    bool shouldTryLongShot = hasOpenLane && Random.value <= longShotChance;

                    if (shouldTryLongShot && Owner.CanScore(false, false))
                    {
                        float hesitation = MatchManager.Instance != null ? MatchManager.Instance.AiHesitationTime : 0.2f;
                        if (_longShotHesitationUntil <= 0f)
                            _longShotHesitationUntil = Time.time + hesitation + Random.Range(0.03f, 0.1f);

                        if (Time.time < _longShotHesitationUntil)
                            return;

                        _longShotHesitationUntil = 0f;
                        Owner.MarkAutomaticKickCommand(KickType.Shot);
                        SuperMachine.ChangeState<KickBallMainState>();
                        return;
                    }
                }
            }

            if (SuperMachine.IsCurrentState<KickBallMainState>())
                return;

            if (IsShootingAtUserGoal() && IsInMercyZone())
            {
                if (TryMercyPassOrReposition())
                    return;

                maxPassTime = Mathf.Min(maxPassTime, 0.12f);
            }

            if (maxPassTime <= 0 || Owner.IsThreatened())  //try passing if threatened or depleted wait time
            {
                float hesitationChance = Mathf.Clamp01(_difficultyProfile.AIDecisionHesitationChance + pressure01 * _difficultyProfile.AIPressureErrorBoost * 0.4f);
                if (Random.value <= hesitationChance)
                {
                    maxPassTime = Random.Range(0.12f, 0.3f);
                    return;
                }

                if (TryTeamPlayPass(pressure01, distanceToGoal, true))
                    return;

                // decrement max num of tries
                if (maxNumOfTries > 0)
                    --maxNumOfTries;
            }
        }

        public override void Exit()
        {
            base.Exit();

            Owner.SetCanPassPreviewVisible(false);

            Owner.ResetSprintState(0.95f);

            //stop steering
            Owner.RPGMovement.SetSteeringOff();
            Owner.RPGMovement.SetTrackingOff();
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
