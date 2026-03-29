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

            LogGoalKeeperDebug("Enter TendGoal");
        }

        public override void Execute()
        {
            base.Execute();

            //get the entity positions
            Vector3 ballPosition = Ball.Instance.NormalizedPosition;

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

            // if loose ball is near the keeper, actively step to it and pick it up when possible
            bool shouldAutoCollectLooseBall = isBallLoose && distanceToBall <= LooseBallAutoCollectDistance;
            if (shouldAutoCollectLooseBall)
            {
                Owner.RPGMovement.SetRotateFacePosition(ballPosition);
                Vector3 collectTarget = Owner.ClampGoalKeeperTargetToHomeRadius(ballPosition);
                Owner.RPGMovement.SetMoveTarget(collectTarget);
                Owner.RPGMovement.Steer = Vector3.Distance(Owner.Position, collectTarget) >= 0.2f;

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
                        LogGoalKeeperDebug("Auto collect waiting for pickup block: " + Mathf.Max(0f, remaining).ToString("0.00") + "s");
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
                    LogGoalKeeperDebug("Pickup temporarily blocked after pass release: " + Mathf.Max(0f, remaining).ToString("0.00") + "s");
                    _lastPickupBlockedLogTime = Time.time;
                }
            }

            //set the look target
            Owner.RPGMovement.SetRotateFacePosition(ballPosition);

            //if I have exhausted my time then update the tend point
            if (_timeSinceLastUpdate <= 0f)
            {
                //do not continue if the ball didnt move
                if (_prevBallPosition != ballPosition)
                {
                    //cache the ball position
                    _prevBallPosition = ballPosition;

                    //run the logic for protecting the goal, find the position
                    Vector3 ballRelativePosToGoal = Owner.TeamGoal.transform.InverseTransformPoint(ballPosition);
                    ballRelativePosToGoal.z = Owner.TendGoalDistance;
                    ballRelativePosToGoal.x /= 3f;
                    ballRelativePosToGoal.x = Mathf.Clamp(ballRelativePosToGoal.x, -2.14f, 2.14f);
                    MatchDifficultyProfile profile = GetRuntimeProfile();
                    Vector3 target = Owner.TeamGoal.transform.TransformPoint(ballRelativePosToGoal);
                    float positioningBlend = Mathf.Clamp01(profile.GKPositioningResponsiveness * Time.deltaTime * 1.8f);
                    _steeringTarget = Vector3.Lerp(_steeringTarget, target, positioningBlend);
                    _steeringTarget = Owner.ClampGoalKeeperTargetToHomeRadius(_steeringTarget);

                    //add some noise to the target
                    float limit = Mathf.Max(0.05f, 1f - Owner.GoalKeeping);
                    _steeringTarget.x += Random.Range(-limit, limit);
                    _steeringTarget.z += Random.Range(-limit, limit);
                }

                //reset the time 
                MatchDifficultyProfile updateProfile = GetRuntimeProfile();
                float baseUpdateDelay = 2f * (1f - Owner.GoalKeeping);
                float delayedUpdate = Mathf.Lerp(baseUpdateDelay, baseUpdateDelay + 0.2f, 1f - Mathf.Clamp01(updateProfile.GKPositioningResponsiveness / 2f));
                _timeSinceLastUpdate = Mathf.Max(0.08f, delayedUpdate);
                if (_timeSinceLastUpdate == 0f)
                    _timeSinceLastUpdate = 2f * 0.1f;
            }

            //decrement the time
            _timeSinceLastUpdate -= Time.deltaTime;

            //set the ability to steer here
            Owner.RPGMovement.Steer = Vector3.Distance(Owner.Position, _steeringTarget) >= 1f;
            Owner.RPGMovement.SetMoveTarget(Owner.ClampGoalKeeperTargetToHomeRadius(_steeringTarget));
        }

        public override void ManualExecute()
        {
            base.ManualExecute();

            bool isLooseBallNearKeeper = Ball.Instance.Owner == null
                && Vector3.Distance(Owner.Position, Ball.Instance.NormalizedPosition) <= LooseBallAutoCollectDistance;

            // run logic depending on whether team is in control or not
            if (Owner.IsTeamInControl == true && !isLooseBallNearKeeper)
            {
                LogGoalKeeperDebug("Team in control while TendGoal -> GoToHome");
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

            LogGoalKeeperDebug("OnShotTaken event -> willHitGoal: " + willBallHitAGoal);
            
            // get the goal from the goal trigger
            if (willBallHitAGoal)
            {
                // get the goal
                Goal goal = hitInfo.transform.GetComponent<GoalTrigger>().Goal;

                // check if shot is on target
                bool isShotOnTarget = goal == Owner.TeamGoal;
                LogGoalKeeperDebug("Shot goal test -> isShotOnTarget: " + isShotOnTarget);

                if (isShotOnTarget == true)
                {
                    if (!ShouldAttemptSave())
                    {
                        _hasPendingIntercept = false;
                        LogGoalKeeperDebug("Shot on target -> keeper late reaction (simulated miss)");
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

                    LogGoalKeeperDebug("Shot on target -> queued intercept in " + _pendingInterceptDelay.ToString("0.00") + "s");
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

            LogGoalKeeperDebug("Transition -> InterceptShot (delayed)");
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

            float pressureBoost = MatchManager.Instance != null ? MatchManager.Instance.DdaAssistStrength : 0f;
            float mistakeChance = Mathf.Clamp01(profile.GKMistakeChance + (positioningPenalty * 0.25f) + (pressureBoost * 0.12f));
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
            bool isCaught = Owner.TryCatchBallAsGoalKeeper(ballSpeed);

            if (isCaught)
            {
                MatchDifficultyProfile profile = GetRuntimeProfile();
                float reboundChance = Mathf.Clamp01(profile.GKReboundChance + (1f - Owner.GoalKeeping) * 0.15f);
                if (Random.value <= reboundChance)
                {
                    TriggerRebound(ballSpeed, "TendGoal catch converted to rebound");
                    return false;
                }

                LogGoalKeeperDebug(context + " -> Caught ball (chance: " + catchChance.ToString("0.00")
                    + ", speed: " + ballSpeed.ToString("0.00") + ") -> ControlBall");
                Machine.ChangeState<ControlBallMainState>();
                return true;
            }

            Owner.GoalKeeperPickupBlockedUntil = Mathf.Max(Owner.GoalKeeperPickupBlockedUntil,
                Time.time + GoalKeeperCatchRetryDelay);

            LogGoalKeeperDebug(context + " -> Missed catch (chance: " + catchChance.ToString("0.00")
                + ", speed: " + ballSpeed.ToString("0.00") + ")");

            return false;
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
            LogGoalKeeperDebug(context + " -> rebound target: " + reboundTarget + ", power: " + reboundPower.ToString("0.00"));
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

        void LogGoalKeeperDebug(string message)
        {
            if (MatchManager.Instance == null || !MatchManager.Instance.EnableGoalkeeperDebug)
                return;

            Debug.Log("[GK DEBUG] " + Owner.name + " :: " + message);
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
