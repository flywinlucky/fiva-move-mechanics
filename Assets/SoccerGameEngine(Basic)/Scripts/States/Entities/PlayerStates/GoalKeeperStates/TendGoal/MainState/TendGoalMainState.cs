using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.ControlBall.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.InterceptShot.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Triggers;
using RobustFSM.Base;
using RobustFSM.Interfaces;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.GoToHome.MainState
{
    /// <summary>
    /// The keeper tends/protects the goal from the opposition
    /// </summary>
    public class TendGoalMainState : BState
    {
        const float LooseBallAutoCollectDistance = 10f;
        const float LooseBallCollectDistanceNearThreat = 13.5f;
        const float GoalThreatNearDistance = 16f;
        const float GoalThreatFarDistance = 46f;
        const float BallSpeedUrgencyReference = 14f;
        const float GoalKeeperBaseAdvanceRatio = 0.85f;
        const float GoalKeeperNearThreatDepthBias = 0.18f;
        const float GoalKeeperBallMoveRepositionThresholdSafe = 0.9f;
        const float GoalKeeperBallMoveRepositionThresholdDanger = 0.35f;
        const float GoalKeeperTargetMoveThresholdSafe = 1.05f;
        const float GoalKeeperTargetMoveThresholdDanger = 0.45f;
        const float GoalKeeperAnchorGridSafe = 0.22f;
        const float GoalKeeperAnchorGridDanger = 0.1f;
        const float GoalKeeperHoldRadiusSafe = 0.95f;
        const float GoalKeeperHoldRadiusDanger = 0.42f;
        const float GoalKeeperCatchRetryDelay = 0.15f;
        const float SaveQualityMin = 0.05f;
        const float SaveQualityMax = 0.95f;

        int _goalLayerMask;
        float _timeSinceLastUpdate;
        float _lastPickupBlockedLogTime;
        Vector3 _steeringTarget;
        Vector3 _prevBallPosition;
        bool _hasPendingIntercept;
        float _pendingInterceptDelay;
        float _pendingBallVelocity;
        Vector3 _pendingBallInitialPosition;
        Vector3 _pendingShotTarget;
        bool _pendingForceRebound;
        float _pendingSaveQuality;

        void UpdateGoalKeeperControlIcons(bool isNearOrHasBall)
        {
            Owner.SetCanPassPreviewVisible(isNearOrHasBall);

            if (Owner.IconUserControlled != null)
                Owner.IconUserControlled.SetActive(Owner.IsUserControlled && isNearOrHasBall);
        }

        public override void Enter()
        {
            base.Enter();

            _goalLayerMask = LayerMask.GetMask("GoalTrigger");

            //set some data
            _prevBallPosition = 1000 * Vector3.one;
            _steeringTarget = Owner.ClampGoalKeeperTargetToHomeRadius(Owner.Position);
            _timeSinceLastUpdate = 0f;
            _lastPickupBlockedLogTime = -10f;
            _hasPendingIntercept = false;
            _pendingInterceptDelay = 0f;
            _pendingForceRebound = false;
            _pendingSaveQuality = 0f;

            //set the rpg movement
            Owner.RPGMovement.SetSteeringOn();
            Owner.RPGMovement.SetTrackingOn();
            Owner.RPGMovement.Speed = Owner.TendGoalSpeed;

            UpdateGoalKeeperControlIcons(false);

            //register to some events
            Owner.OnShotTaken += Instance_OnShotTaken;
        }

        public override void Execute()
        {
            base.Execute();

            //get the entity positions
            Vector3 ballPosition = Ball.Instance.NormalizedPosition;
            float distanceToGoal = Vector3.Distance(ballPosition, Owner.TeamGoal.Position);
            float goalThreat01 = ComputeGoalThreat01(ballPosition, distanceToGoal);
            float looseCollectDistance = Mathf.Lerp(LooseBallAutoCollectDistance,
                LooseBallCollectDistanceNearThreat,
                goalThreat01);

            bool isNearOrHasBall = Ball.Instance.Owner == Owner || Owner.IsBallWithinControlableDistance();
            UpdateGoalKeeperControlIcons(isNearOrHasBall);

            ProcessPendingShotIntercept();

            if (SuperMachine.IsCurrentState<TendGoalMainState>() == false)
                return;

            // catch any loose ball that enters keeper control radius
            bool isBallLoose = Ball.Instance.Owner == null;
            bool isBallInPickupRange = Owner.IsBallWithinControlableDistance();
            bool canPickupNow = Time.time >= Owner.GoalKeeperPickupBlockedUntil;
            float distanceToBall = Vector3.Distance(Owner.Position, ballPosition);

            UpdateDynamicTendGoalSpeed(goalThreat01, isBallLoose, distanceToBall);

            // if loose ball is near the keeper, actively step to it and pick it up when possible
            bool shouldAutoCollectLooseBall = isBallLoose && distanceToBall <= looseCollectDistance;
            if (shouldAutoCollectLooseBall)
            {
                Owner.RPGMovement.SetRotateFacePosition(ballPosition);
                Vector3 collectTarget = Owner.ClampGoalKeeperTargetToHomeRadius(ballPosition);
                Owner.RPGMovement.SetMoveTarget(collectTarget);
                Owner.RPGMovement.Steer = Vector3.Distance(Owner.Position, collectTarget) >= Mathf.Lerp(0.12f, 0.28f, 1f - goalThreat01);

                if (isBallInPickupRange && canPickupNow)
                {
                    if (TryCatchBallAndControl("Loose ball within " + LooseBallAutoCollectDistance + "m"))
                        return;
                }

                if (isBallInPickupRange && !canPickupNow)
                {
                    if (Time.time - _lastPickupBlockedLogTime >= 0.2f)
                    {
                        float remaining = Owner.GoalKeeperPickupBlockedUntil - Time.time;
                        _lastPickupBlockedLogTime = Time.time;
                    }
                }

                return;
            }

            if (isBallLoose && isBallInPickupRange && canPickupNow)
            {
                if (TryCatchBallAndControl("Loose ball within control distance"))
                    return;
            }

            if (isBallLoose && isBallInPickupRange && !canPickupNow)
            {
                if (Time.time - _lastPickupBlockedLogTime >= 0.2f)
                {
                    float remaining = Owner.GoalKeeperPickupBlockedUntil - Time.time;
                    _lastPickupBlockedLogTime = Time.time;
                }
            }

            //set the look target
            Owner.RPGMovement.SetRotateFacePosition(ballPosition);

            //if I have exhausted my time then update the tend point
            if (_timeSinceLastUpdate <= 0f)
            {
                float minBallMoveThreshold = Mathf.Lerp(
                    GoalKeeperBallMoveRepositionThresholdSafe,
                    GoalKeeperBallMoveRepositionThresholdDanger,
                    goalThreat01);
                float ballMoveThresholdSqr = minBallMoveThreshold * minBallMoveThreshold;

                bool shouldReposition = (_prevBallPosition - ballPosition).sqrMagnitude >= ballMoveThresholdSqr;
                if (_prevBallPosition.x > 900f)
                    shouldReposition = true;

                if (shouldReposition)
                {
                    //cache the ball position
                    _prevBallPosition = ballPosition;

                    //run the logic for protecting the goal, find the position
                    Vector3 ballRelativePosToGoal = Owner.TeamGoal.transform.InverseTransformPoint(ballPosition);

                    float movementRadius = Mathf.Max(1f, Owner.GoalKeeperMovementRadius);
                    float maxAdvanceDepth = Mathf.Max(Owner.TendGoalDistance + 0.6f,
                        movementRadius * GoalKeeperBaseAdvanceRatio);
                    float advanceBySafety01 = 1f - goalThreat01;
                    float dynamicDepth = Mathf.Lerp(Owner.TendGoalDistance,
                        maxAdvanceDepth,
                        advanceBySafety01);

                    // In danger phases we bias the keeper slightly closer to goal for stable shot defense.
                    dynamicDepth = Mathf.Max(Owner.TendGoalDistance - GoalKeeperNearThreatDepthBias,
                        dynamicDepth);

                    float lateralScale = Mathf.Lerp(2.35f, 3.6f, advanceBySafety01);
                    float lateralClamp = Mathf.Lerp(1.85f, 2.55f, advanceBySafety01);

                    ballRelativePosToGoal.z = dynamicDepth;
                    ballRelativePosToGoal.x /= lateralScale;
                    ballRelativePosToGoal.x = Mathf.Clamp(ballRelativePosToGoal.x, -lateralClamp, lateralClamp);

                    MatchDifficultyProfile profile = GetRuntimeProfile();
                    Vector3 target = Owner.TeamGoal.transform.TransformPoint(ballRelativePosToGoal);
                    float anchorGrid = Mathf.Lerp(GoalKeeperAnchorGridSafe, GoalKeeperAnchorGridDanger, goalThreat01);
                    target = SnapToGridXZ(target, anchorGrid);
                    float speedUrgency = GetBallSpeedUrgency01(ballPosition);
                    float positioningBlend = Mathf.Clamp01(
                        profile.GKPositioningResponsiveness
                        * Time.deltaTime
                        * Mathf.Lerp(1.2f, 3.2f, goalThreat01 + (speedUrgency * 0.65f)));

                    float minTargetMoveThreshold = Mathf.Lerp(
                        GoalKeeperTargetMoveThresholdSafe,
                        GoalKeeperTargetMoveThresholdDanger,
                        goalThreat01);
                    bool targetChangedEnough = (_steeringTarget - target).sqrMagnitude >= (minTargetMoveThreshold * minTargetMoveThreshold);
                    if (!targetChangedEnough)
                        target = _steeringTarget;

                    _steeringTarget = Vector3.Lerp(_steeringTarget, target, positioningBlend);
                    _steeringTarget = Owner.ClampGoalKeeperTargetToHomeRadius(_steeringTarget);
                }

                //reset the time 
                MatchDifficultyProfile updateProfile = GetRuntimeProfile();
                float responsiveness01 = Mathf.Clamp01(updateProfile.GKPositioningResponsiveness / 2f);
                float skill01 = Mathf.Clamp01(Owner.GoalKeeping);
                float urgency01 = Mathf.Clamp01(goalThreat01 + (GetBallSpeedUrgency01(ballPosition) * 0.6f));

                float baseUpdateDelay = Mathf.Lerp(0.5f, 0.12f, responsiveness01);
                baseUpdateDelay = Mathf.Lerp(baseUpdateDelay, baseUpdateDelay * 0.78f, skill01);
                baseUpdateDelay = Mathf.Lerp(baseUpdateDelay, 0.1f, urgency01);

                _timeSinceLastUpdate = Mathf.Clamp(baseUpdateDelay, 0.1f, 0.62f);
            }

            //decrement the time
            _timeSinceLastUpdate -= Time.deltaTime;

            //set the ability to steer here
            float steerThreshold = Mathf.Lerp(1.05f, 0.55f, goalThreat01);
            float holdRadius = Mathf.Lerp(GoalKeeperHoldRadiusSafe, GoalKeeperHoldRadiusDanger, goalThreat01);
            float distanceToSteeringTarget = Vector3.Distance(Owner.Position, _steeringTarget);

            bool shouldHoldDefensiveAnchor = !isBallLoose && distanceToSteeringTarget <= holdRadius;
            if (shouldHoldDefensiveAnchor)
            {
                // Hold position near goal to avoid micro step jitter while still facing play.
                Owner.RPGMovement.Steer = false;
                Owner.RPGMovement.SetMoveTarget(Owner.Position);
            }
            else
            {
                Owner.RPGMovement.Steer = distanceToSteeringTarget >= steerThreshold;
                Owner.RPGMovement.SetMoveTarget(Owner.ClampGoalKeeperTargetToHomeRadius(_steeringTarget));
            }
        }

        public override void ManualExecute()
        {
            base.ManualExecute();

            bool isLooseBallNearKeeper = Ball.Instance.Owner == null
                && Vector3.Distance(Owner.Position, Ball.Instance.NormalizedPosition) <= LooseBallAutoCollectDistance;

            // run logic depending on whether team is in control or not
            if (Owner.IsTeamInControl == true && !isLooseBallNearKeeper)
            {
                SuperMachine.ChangeState<GoToHomeMainState>();
            }
        }

        public override void Exit()
        {
            base.Exit();

            UpdateGoalKeeperControlIcons(false);

            Owner.RPGMovement.SetTrackingOff();

            _hasPendingIntercept = false;
            _pendingInterceptDelay = 0f;

            //deregister to some events
            Owner.OnShotTaken -= Instance_OnShotTaken;
        }

        private void Instance_OnShotTaken(float flightTime, float velocity, Vector3 initial, Vector3 target)
        {
            // get the direction to target
            Vector3 direction = target - initial;

            // make a raycast and test if it hits target
            RaycastHit hitInfo;
            bool willBallHitAGoal = Physics.SphereCast(Ball.Instance.NormalizedPosition + Vector3.up,
                        Ball.Instance.SphereCollider.radius,
                        direction,
                        out hitInfo,
                        300,
                        _goalLayerMask);

            
            // get the goal from the goal trigger
            if (willBallHitAGoal)
            {
                // get the goal
                Goal goal = hitInfo.transform.GetComponent<GoalTrigger>().Goal;

                // check if shot is on target
                bool isShotOnTarget = goal == Owner.TeamGoal;

                if (isShotOnTarget == true)
                {
                    if (!ShouldAttemptSave())
                    {
                        _hasPendingIntercept = false;
                        return;
                    }

                    EvaluateShotContext(initial, target, velocity,
                        out float saveQuality,
                        out bool forceMistake,
                        out bool forceRebound,
                        out Vector3 diveAdjustedTarget);

                    _hasPendingIntercept = true;
                    _pendingBallInitialPosition = initial;
                    _pendingBallVelocity = velocity;
                    _pendingShotTarget = diveAdjustedTarget;
                    _pendingInterceptDelay = GetRandomReactionDelay();
                    if (forceMistake)
                        _pendingInterceptDelay += Random.Range(0.05f, 0.16f);
                    _pendingForceRebound = forceRebound;
                    _pendingSaveQuality = saveQuality;

                 }
            }
        }

        void ProcessPendingShotIntercept()
        {
            if (!_hasPendingIntercept)
                return;

            _pendingInterceptDelay -= Time.deltaTime;
            if (_pendingInterceptDelay > 0f)
                return;

            _hasPendingIntercept = false;

            InterceptShotMainState interceptShotState = Machine.GetState<InterceptShotMainState>();
            interceptShotState.BallInitialPosition = _pendingBallInitialPosition;
            interceptShotState.BallInitialVelocity = _pendingBallVelocity;
            interceptShotState.ShotTarget = _pendingShotTarget;
            interceptShotState.ForceRebound = _pendingForceRebound;
            interceptShotState.SaveQuality = _pendingSaveQuality;

            Machine.ChangeState<InterceptShotMainState>();
        }

        bool ShouldAttemptSave()
        {
            MatchDifficultyProfile profile = GetRuntimeProfile();
            float saveAttemptChance = Mathf.Clamp01(0.35f + (Owner.GoalKeeping * 0.35f) - (profile.GKMistakeChance * 0.45f));
            return Random.value <= saveAttemptChance;
        }

        void EvaluateShotContext(
            Vector3 initial,
            Vector3 target,
            float shotVelocity,
            out float saveQuality,
            out bool forceMistake,
            out bool forceRebound,
            out Vector3 diveAdjustedTarget)
        {
            MatchDifficultyProfile profile = GetRuntimeProfile();

            Vector3 shotDirection = (target - initial);
            float shotDistance = shotDirection.magnitude;
            if (shotDirection.sqrMagnitude > 0.0001f)
                shotDirection.Normalize();
            else
                shotDirection = Owner.TeamGoal.transform.forward;

            float speedN = Mathf.Clamp01(Mathf.Max(0f, shotVelocity) / 40f);
            float distancePressure = 1f - Mathf.Clamp01(shotDistance / 24f);

            Vector3 goalLocalTarget = Owner.TeamGoal.transform.InverseTransformPoint(target);
            float lateralHardness = Mathf.Clamp01(Mathf.Abs(goalLocalTarget.x) / 2.5f);

            Vector3 keeperToTarget = target - Owner.Position;
            keeperToTarget.y = 0f;
            Vector3 keeperFacing = Vector3.Scale(Owner.transform.forward, new Vector3(1f, 0f, 1f));
            if (keeperFacing.sqrMagnitude <= 0.0001f)
                keeperFacing = Owner.TeamGoal.transform.forward;
            keeperFacing.Normalize();

            float anglePenalty = 1f - ((Vector3.Dot(keeperFacing, keeperToTarget.normalized) + 1f) * 0.5f);
            float positioningPenalty = Mathf.Clamp01(Vector3.Distance(Owner.Position, _steeringTarget) / 4f);

            float baseSaveChance =
                0.60f
                + (Owner.GoalKeeping * 0.22f)
                - (speedN * 0.20f)
                - (distancePressure * 0.18f)
                - (lateralHardness * 0.14f)
                - (anglePenalty * 0.12f)
                - (positioningPenalty * 0.12f);

            bool likelyUserShot = IsLikelyUserShot(initial);
            if (likelyUserShot)
                baseSaveChance -= profile.GKShotForgiveness;

            bool userKeeperLongRangeSupport = IsUserKeeperFacingAiLongShot(initial, shotDistance);
            if (userKeeperLongRangeSupport)
                baseSaveChance += 0.10f;

            float pressureBoost = MatchManager.Instance != null ? MatchManager.Instance.DdaAssistStrength : 0f;
            float mistakeChance = Mathf.Clamp01(profile.GKMistakeChance + (positioningPenalty * 0.25f) + (pressureBoost * 0.12f));
            if (userKeeperLongRangeSupport)
                mistakeChance *= 0.72f;
            forceMistake = Random.value <= mistakeChance;

            if (forceMistake)
                baseSaveChance -= Random.Range(0.08f, 0.18f);

            saveQuality = Mathf.Clamp(baseSaveChance, SaveQualityMin, SaveQualityMax);

            bool saveWillSucceed = Random.value <= saveQuality;
            if (!saveWillSucceed)
            {
                forceMistake = true;
                forceRebound = false;
                diveAdjustedTarget = target;
                return;
            }

            float reboundChance = Mathf.Clamp01(profile.GKReboundChance + (forceMistake ? 0.12f : 0f));
            if (userKeeperLongRangeSupport)
                reboundChance = Mathf.Clamp01(reboundChance + 0.12f);
            forceRebound = Random.value <= reboundChance;

            diveAdjustedTarget = ApplyDiveImprecision(target, goalLocalTarget.x, profile, forceMistake);
        }

        Vector3 ApplyDiveImprecision(Vector3 target, float targetLocalX, MatchDifficultyProfile profile, bool forceMistake)
        {
            Vector3 adjusted = target;

            int desiredDive = targetLocalX < -0.15f ? -1 : (targetLocalX > 0.15f ? 1 : 0);
            int selectedDive = desiredDive;
            if (Random.value <= profile.GKWrongDiveChance || forceMistake)
            {
                if (desiredDive == 0)
                    selectedDive = Random.value <= 0.5f ? -1 : 1;
                else
                    selectedDive = -desiredDive;
            }

            if (selectedDive != 0)
            {
                float diveOffset = forceMistake ? Random.Range(0.55f, 1.05f) : Random.Range(0.20f, 0.55f);
                adjusted += Owner.TeamGoal.transform.right * selectedDive * diveOffset;
            }

            return adjusted;
        }

        bool TryCatchBallAndControl(string context)
        {
            if (Ball.Instance == null || Ball.Instance.Owner != null)
                return false;

            float ballSpeed = Ball.Instance.Rigidbody != null
                ? Ball.Instance.Rigidbody.velocity.magnitude
                : 0f;

            float catchChance = Owner.EvaluateGoalKeeperCatchChance(ballSpeed);
            float catchAssist = ComputeCatchAssistBonus(ballSpeed);
            float adjustedCatchChance = Mathf.Clamp(catchChance + catchAssist, 0.1f, 0.97f);
            bool isCaught = Random.value <= adjustedCatchChance;

            if (isCaught)
            {
                MatchDifficultyProfile profile = GetRuntimeProfile();
                float reboundChance = Mathf.Clamp01(profile.GKReboundChance + (1f - Owner.GoalKeeping) * 0.15f);
                if (Random.value <= reboundChance)
                {
                    TriggerRebound(ballSpeed, "TendGoal catch converted to rebound");
                    return false;
                }

                Machine.ChangeState<ControlBallMainState>();
                return true;
            }

            Owner.GoalKeeperPickupBlockedUntil = Mathf.Max(Owner.GoalKeeperPickupBlockedUntil,
                Time.time + GoalKeeperCatchRetryDelay);

            return false;
        }

        void UpdateDynamicTendGoalSpeed(float goalThreat01, bool isBallLoose, float distanceToBall)
        {
            float skill01 = Mathf.Clamp01(Owner.GoalKeeping);
            float speedMultiplier = Mathf.Lerp(1.02f, 1.38f, goalThreat01);
            speedMultiplier = Mathf.Lerp(speedMultiplier, speedMultiplier * 1.08f, skill01);

            if (isBallLoose && distanceToBall <= LooseBallCollectDistanceNearThreat)
                speedMultiplier += 0.18f;

            Owner.RPGMovement.Speed = Owner.TendGoalSpeed * speedMultiplier;
        }

        float GetBallSpeedUrgency01(Vector3 ballPosition)
        {
            if (Ball.Instance == null || Ball.Instance.Rigidbody == null)
                return 0f;

            Vector3 velocity = Ball.Instance.Rigidbody.velocity;
            Vector3 toGoal = Owner.TeamGoal.Position - ballPosition;
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude <= 0.0001f)
                return 0f;

            toGoal.Normalize();
            Vector3 planarVelocity = Vector3.Scale(velocity, new Vector3(1f, 0f, 1f));
            float speed01 = Mathf.Clamp01(planarVelocity.magnitude / BallSpeedUrgencyReference);
            float towardGoal01 = Mathf.Clamp01((Vector3.Dot(planarVelocity.normalized, toGoal) + 1f) * 0.5f);

            return speed01 * towardGoal01;
        }

        float ComputeGoalThreat01(Vector3 ballPosition, float distanceToGoal)
        {
            float distanceThreat01 = 1f - Mathf.InverseLerp(GoalThreatNearDistance,
                GoalThreatFarDistance,
                distanceToGoal);

            float possessionThreat01 = 0.55f;
            Player ballOwner = Ball.Instance != null ? Ball.Instance.Owner : null;
            if (ballOwner == Owner)
            {
                possessionThreat01 = 0f;
            }
            else if (ballOwner != null)
            {
                possessionThreat01 = ballOwner.IsTeamInControl == Owner.IsTeamInControl ? 0.15f : 1f;
            }

            float speedUrgency01 = GetBallSpeedUrgency01(ballPosition);
            float threat = (distanceThreat01 * 0.62f) + (possessionThreat01 * 0.26f) + (speedUrgency01 * 0.22f);
            return Mathf.Clamp01(threat);
        }

        float ComputeCatchAssistBonus(float ballSpeed)
        {
            float distanceToGoal = Vector3.Distance(Owner.Position, Owner.TeamGoal.Position);
            float homeAssist = 1f - Mathf.Clamp01(distanceToGoal / Mathf.Max(2.5f, Owner.GoalKeeperMovementRadius + 1.5f));
            float speedAssist = Mathf.Clamp01(ballSpeed / Mathf.Max(0.1f, 18f));
            float skillAssist = Mathf.Clamp01(Owner.GoalKeeping);

            return (homeAssist * 0.1f) + (speedAssist * 0.05f) + (skillAssist * 0.03f);
        }

        Vector3 SnapToGridXZ(Vector3 value, float gridSize)
        {
            float grid = Mathf.Max(0.01f, gridSize);
            value.x = Mathf.Round(value.x / grid) * grid;
            value.z = Mathf.Round(value.z / grid) * grid;
            value.y = 0f;
            return value;
        }

        float GetRandomReactionDelay()
        {
            MatchDifficultyProfile profile = GetRuntimeProfile();
            float minDelay = profile.GKReactionDelayMin;
            float maxDelay = Mathf.Max(minDelay, profile.GKReactionDelayMax);
            float delay = Random.Range(minDelay, maxDelay);

            float skillPenalty = Mathf.Clamp01(1f - Owner.GoalKeeping);
            delay += Random.Range(0f, 0.12f) * skillPenalty;
            return delay;
        }

        void TriggerRebound(float ballSpeed, string context)
        {
            if (Ball.Instance == null)
                return;

            Ball.Instance.Owner = null;
            Ball.Instance.Rigidbody.isKinematic = false;

            Vector3 awayFromGoal = (Owner.Position - Owner.TeamGoal.Position).normalized;
            if (awayFromGoal.sqrMagnitude <= 0.0001f)
                awayFromGoal = Owner.transform.forward;

            Vector3 lateral = Vector3.Cross(Vector3.up, awayFromGoal).normalized;
            float lateralSign = Random.value <= 0.5f ? -1f : 1f;
            Vector3 reboundDirection = (awayFromGoal + lateral * lateralSign * Random.Range(0.25f, 0.7f)).normalized;
            Vector3 reboundTarget = Owner.Position + reboundDirection * Random.Range(4f, 10f);
            reboundTarget.y = 0f;

            float reboundPower = Mathf.Max(3f, (ballSpeed * 0.45f) + Random.Range(2f, 4.5f));
            Ball.Instance.Kick(reboundTarget, reboundPower);

            Owner.GoalKeeperPickupBlockedUntil = Mathf.Max(Owner.GoalKeeperPickupBlockedUntil, Time.time + 0.2f);
        }

        bool IsLikelyUserShot(Vector3 initial)
        {
            if (Owner.OppositionMembers == null)
                return false;

            float sqrRadius = 2.6f * 2.6f;
            for (int i = 0; i < Owner.OppositionMembers.Count; i++)
            {
                Player opp = Owner.OppositionMembers[i];
                if (opp == null || !opp.IsUserControlled)
                    continue;

                if ((opp.Position - initial).sqrMagnitude <= sqrRadius)
                    return true;
            }

            return false;
        }

        bool IsUserKeeperFacingAiLongShot(Vector3 initial, float shotDistance)
        {
            if (shotDistance < 18f)
                return false;

            if (Owner.TeamMembers == null)
                return false;

            bool defendingUserSide = false;
            for (int i = 0; i < Owner.TeamMembers.Count; i++)
            {
                Player mate = Owner.TeamMembers[i];
                if (mate != null && mate.IsUserControlled)
                {
                    defendingUserSide = true;
                    break;
                }
            }

            if (!defendingUserSide)
                return false;

            if (Owner.OppositionMembers != null)
            {
                float sqr = 3.2f * 3.2f;
                for (int i = 0; i < Owner.OppositionMembers.Count; i++)
                {
                    Player opp = Owner.OppositionMembers[i];
                    if (opp != null && (opp.Position - initial).sqrMagnitude <= sqr)
                        return true;
                }
            }

            return true;
        }

        MatchDifficultyProfile GetRuntimeProfile()
        {
            if (MatchManager.Instance != null)
                return MatchManager.Instance.RuntimeDifficultyProfile;

            return new MatchDifficultyProfile
            {
                GKReactionDelayMin = 0.12f,
                GKReactionDelayMax = 0.25f,
                GKMistakeChance = 0.1f,
                GKReboundChance = 0.3f,
                GKPositioningResponsiveness = 1.1f,
                GKWrongDiveChance = 0.1f,
                GKShotForgiveness = 0.08f
            };
        }

        public Player Owner
        {
            get
            {
                return ((GoalKeeperFSM)SuperMachine).Owner;
            }
        }
    }
}
