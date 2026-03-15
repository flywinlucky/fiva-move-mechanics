using System.Collections.Generic;
using System.Linq;
using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.StateMachines.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.GoToHome.MainState;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities;
using RobustFSM.Base;
using UnityEngine;
using static Assets.SoccerGameEngine_Basic_.Scripts.Entities.Player;

namespace Assets.SoccerGameEngine_Basic_.Scripts.States.Entities.PlayerStates.GoalKeeperStates.ControlBall.MainState
{
    /// <summary>
    /// Keeper catches and secures the ball, returns home, then distributes upfield.
    /// </summary>
    public class ControlBallMainState : BState
    {
        const float HoldBallTime = 1.5f;
        const float GoalKeeperRecaptureBlockDuration = 0.45f;
        const float BallReleaseExtraDistance = 1.25f;
        const float ManualForwardPassConeAngle = 120f;
        const float ManualDirectionalFallbackConeAngle = 170f;
        const float ManualForwardPassMinDistance = 2f;
        const float ManualForwardPassMaxDistance = 70f;
        const float ManualEmergencyPassMaxDistance = 90f;
        const float ManualForwardPassLeadDistance = 2.5f;
        const float ManualForwardPassArrivalVelocityMultiplier = 2.2f;
        const float ManualPassSafetyDistance = 10f;
        const float AutoPassDirectionJitterAngle = 25f;
        const float PassPreviewScanInterval = 0.5f;

        float _distributionTimer;
        float _nextPreviewScanTime;
        bool _isSettledAtHome;
        bool _manualControlEnabled;
        bool _hasCachedManualPassTarget;
        float _cachedManualPassPower;
        float _cachedManualPassTime;
        Player _cachedManualPassReceiver;
        Player _previewPassReceiver;
        Vector3 _cachedManualPassTarget;
        Vector3 _distributionLookTarget;
        Transform _refObject;

        void SetPreviewPassReceiver(Player receiver)
        {
            if (_previewPassReceiver == receiver)
                return;

            if (_previewPassReceiver != null)
                _previewPassReceiver.SetCanPassPreviewVisible(false);

            _previewPassReceiver = receiver;

            if (_previewPassReceiver != null)
                _previewPassReceiver.SetCanPassPreviewVisible(true);
        }

        void ClearPreviewPassReceiver()
        {
            if (_previewPassReceiver != null)
                _previewPassReceiver.SetCanPassPreviewVisible(false);

            _previewPassReceiver = null;
        }

        public override void Enter()
        {
            base.Enter();

            _distributionTimer = HoldBallTime;
            _isSettledAtHome = false;
            _manualControlEnabled = Owner.IsUserControlled;
            _distributionLookTarget = Owner.OppGoal != null
                ? Owner.OppGoal.Position
                : Owner.HomeRegion.position + Owner.transform.forward * 10f;

            Ball.Instance.Owner = Owner;
            Ball.Instance.Rigidbody.isKinematic = true;
            Ball.Instance.Trap();

            // notify team possession so teammates spread into attack shape
            ControlBallDel temp = Owner.OnControlBall;
            if (temp != null)
                temp.Invoke(Owner);

            // Keep teammates away from the keeper while he secures the ball.
            if (Owner.TeamMembers != null)
            {
                foreach (Player teamMate in Owner.TeamMembers)
                {
                    if (teamMate == null || teamMate == Owner)
                        continue;

                    ActionUtility.Invoke_Action(teamMate.OnInstructedToGoToHome);
                }
            }

            if (_manualControlEnabled)
            {
                EnsureReferenceObject();

                if (Owner.IconUserControlled != null)
                    Owner.IconUserControlled.SetActive(true);

                Owner.SetCanPassPreviewVisible(true);

                Owner.RPGMovement.Speed = Owner.ActualSpeed;
                Owner.RPGMovement.SetMoveDirection(Vector3.zero);
                Owner.RPGMovement.SetSteeringOff();
                Owner.RPGMovement.SetTrackingOff();

                ClearPreviewPassReceiver();
                _nextPreviewScanTime = 0f;
                _hasCachedManualPassTarget = false;
                _cachedManualPassReceiver = null;
                _cachedManualPassTarget = Owner.Position;
                _cachedManualPassPower = 0f;
                _cachedManualPassTime = 0f;

                LogGoalKeeperDebug("Enter ControlBall -> manual enabled");
                return;
            }

            if (Owner.IconUserControlled != null)
                Owner.IconUserControlled.SetActive(false);

            Owner.SetCanPassPreviewVisible(true);

            Owner.RPGMovement.SetSteeringOn();
            Owner.RPGMovement.SetTrackingOn();
            Owner.RPGMovement.SetMoveTarget(Owner.HomeRegion.position);
            Owner.RPGMovement.SetRotateFacePosition(Owner.HomeRegion.position);

            LogGoalKeeperDebug("Enter ControlBall -> auto return-home hold");
        }

        public override void Execute()
        {
            base.Execute();

            if (_manualControlEnabled)
            {
                Owner.PlaceBallInfronOfMe();
                ExecuteManualControl();
                return;
            }

            if (!_isSettledAtHome)
            {
                // keep moving and facing home while holding the ball
                Owner.RPGMovement.SetMoveTarget(Owner.HomeRegion.position);
                Owner.RPGMovement.SetRotateFacePosition(Owner.HomeRegion.position);

                if (Owner.IsAtTarget(Owner.HomeRegion.position))
                {
                    _isSettledAtHome = true;

                    // stop movement/rotation updates to avoid trembling at target
                    Owner.RPGMovement.SetSteeringOff();
                    Owner.RPGMovement.SetTrackingOff();
                    Owner.RPGMovement.CurrentSpeed = 0f;
                    Owner.RPGMovement.Velocity = Vector3.zero;

                    // align once toward distribution side of the pitch
                    Vector3 lookDirection = _distributionLookTarget - Owner.Position;
                    lookDirection.y = 0f;
                    if (lookDirection.sqrMagnitude > 0.0001f)
                        Owner.Rotation = Quaternion.LookRotation(lookDirection.normalized);
                }
            }
            else
            {
                // keep a stable hold stance at home
                Owner.RPGMovement.CurrentSpeed = 0f;
                Owner.RPGMovement.Velocity = Vector3.zero;
            }

            Owner.PlaceBallInfronOfMe();

            if (!_isSettledAtHome)
                return;

            _distributionTimer -= Time.deltaTime;
            if (_distributionTimer <= 0f)
            {
                LogGoalKeeperDebug("Auto distribution timer complete -> distribute and return to TendGoal");
                DistributeBallUpField();
                SuperMachine.ChangeState<TendGoalMainState>();
            }
        }

        void ExecuteManualControl()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Vector3 input = new Vector3(horizontal, 0f, vertical);

            EnsureReferenceObject();

            Vector3 refForward = Vector3.Scale(_refObject.forward, new Vector3(1f, 0f, 1f)).normalized;
            Vector3 refRight = Vector3.Scale(_refObject.right, new Vector3(1f, 0f, 1f)).normalized;
            Vector3 movement = input.z * refForward + input.x * refRight;
            if (movement.sqrMagnitude > 1f)
                movement.Normalize();

            movement = Owner.ConstrainGoalKeeperMoveDirection(movement);

            bool isMoving = movement.sqrMagnitude > 0.0001f;
            bool wantsSprint = isMoving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            Owner.ApplySprintToMovement(wantsSprint, isMoving);

            Vector3 passDirection = movement.sqrMagnitude <= 0.0001f ? Owner.transform.forward : movement;
            passDirection.y = 0f;
            if (passDirection.sqrMagnitude <= 0.0001f)
                passDirection = GetUpfieldDirection();
            else
                passDirection.Normalize();

            bool passPressed = Input.GetButtonDown("Pass/Press") || Input.GetKeyDown(KeyCode.N);
            bool canPass = UpdateManualPassPreview(passDirection, false);

            if (passPressed)
            {
                canPass = UpdateManualPassPreview(passDirection, true);

                if (canPass)
                {
                    Vector3 target = _cachedManualPassTarget;
                    Player receiver = _cachedManualPassReceiver;
                    float power = _cachedManualPassPower;
                    float ballTime = _cachedManualPassTime;

                    PrepareBallReleaseForPass(target);
                    Ball.Instance.Owner = null;
                    Ball.Instance.Rigidbody.isKinematic = false;
                    Owner.MakePass(Ball.Instance.NormalizedPosition,
                        target,
                        receiver,
                        power,
                        ballTime);

                    if (receiver != null)
                        Owner.PrevPassReceiver = receiver;

                    LogGoalKeeperDebug("Manual pass executed -> receiver: "
                        + (receiver != null ? receiver.name : "<fallback>")
                        + ", target: " + target);

                    SuperMachine.ChangeState<TendGoalMainState>();
                    return;
                }

                LogGoalKeeperDebug("Manual pass requested but no valid teammate in front");
            }

            if (input == Vector3.zero)
            {
                Owner.RPGMovement.SetMoveDirection(Vector3.zero);

                if (Owner.RPGMovement.Steer)
                    Owner.RPGMovement.SetSteeringOff();

                if (Owner.RPGMovement.Track)
                    Owner.RPGMovement.SetTrackingOff();
            }
            else
            {
                Owner.RPGMovement.SetMoveDirection(movement);
                Owner.RPGMovement.SetRotateFaceDirection(movement);

                if (!Owner.RPGMovement.Steer)
                    Owner.RPGMovement.SetSteeringOn();

                if (!Owner.RPGMovement.Track)
                    Owner.RPGMovement.SetTrackingOn();
            }
        }

        bool UpdateManualPassPreview(Vector3 passDirection, bool forceScan)
        {
            if (!forceScan && Time.time < _nextPreviewScanTime)
                return _hasCachedManualPassTarget;

            _nextPreviewScanTime = Time.time + PassPreviewScanInterval;

            _hasCachedManualPassTarget = TryFindLongForwardManualPass(passDirection,
                out _cachedManualPassReceiver,
                out _cachedManualPassTarget,
                out _cachedManualPassPower,
                out _cachedManualPassTime);

            if (!_hasCachedManualPassTarget)
            {
                _hasCachedManualPassTarget = TryFindClosestTeamMateEmergencyPass(passDirection,
                    out _cachedManualPassReceiver,
                    out _cachedManualPassTarget,
                    out _cachedManualPassPower,
                    out _cachedManualPassTime);
            }

            SetPreviewPassReceiver(_hasCachedManualPassTarget ? _cachedManualPassReceiver : null);

            return _hasCachedManualPassTarget;
        }

        void DistributeBallUpField()
        {
            BuildDistributionPass(out Vector3 target, out Player receiver, out float power, out float ballTime);

            PrepareBallReleaseForPass(target);
            Ball.Instance.Owner = null;
            Ball.Instance.Rigidbody.isKinematic = false;

            // use the same pass mechanic as outfield players for predictable teammate distribution
            Owner.MakePass(Ball.Instance.NormalizedPosition,
                target,
                receiver,
                power,
                ballTime);

            if (receiver != null)
                Owner.PrevPassReceiver = receiver;

            LogGoalKeeperDebug("Auto pass distribution -> receiver: "
                + (receiver != null ? receiver.name : "<fallback>")
                + ", target: " + target);
        }

        void PrepareBallReleaseForPass(Vector3 target)
        {
            Owner.GoalKeeperPickupBlockedUntil = Time.time + GoalKeeperRecaptureBlockDuration;

            Vector3 releaseDirection = target - Owner.Position;
            releaseDirection.y = 0f;
            if (releaseDirection.sqrMagnitude <= 0.0001f)
                releaseDirection = Owner.transform.forward;

            releaseDirection.y = 0f;
            if (releaseDirection.sqrMagnitude <= 0.0001f)
                releaseDirection = Vector3.forward;

            releaseDirection.Normalize();

            Vector3 releasePosition = Owner.Position
                + releaseDirection * (Owner.Radius + Owner.BallControlDistance + BallReleaseExtraDistance);

            Ball.Instance.NormalizedPosition = releasePosition;
        }

        void BuildDistributionPass(out Vector3 target, out Player receiver, out float power, out float ballTime)
        {
            if (TryFindRandomAutoDistributionPass(out receiver, out target, out power, out ballTime))
                return;

            bool hasPassTarget = Owner.CanPass(true);
            if (!hasPassTarget)
                hasPassTarget = Owner.CanPass(false);

            if (hasPassTarget && Owner.KickTarget != null)
            {
                target = (Vector3)Owner.KickTarget;
                receiver = Owner.PassReceiver;
                if (receiver == null)
                    receiver = PickReceiver();

                power = Mathf.Clamp(Owner.KickPower, 0.25f, Owner.ActualPower);
                ballTime = Mathf.Max(0.1f, Owner.BallTime);
                return;
            }

            receiver = PickReceiver();
            target = receiver != null
                ? receiver.Position
                : Owner.HomeRegion.position + Owner.transform.forward * 18f;

            if (receiver != null)
            {
                Vector3 receiverForward = Vector3.Scale(receiver.transform.forward, new Vector3(1f, 0f, 1f));
                if (receiverForward.sqrMagnitude > 0.0001f)
                    receiverForward.Normalize();
                else
                    receiverForward = Vector3.forward;

                target += receiverForward * Random.Range(0.75f, 2.0f);
            }

            float candidatePower = Owner.FindPower(Ball.Instance.NormalizedPosition,
                target,
                Owner.BallPassArriveVelocity);

            if (float.IsNaN(candidatePower) || float.IsInfinity(candidatePower) || candidatePower <= 0f)
                candidatePower = Owner.ActualPower * 0.65f;

            power = Mathf.Clamp(candidatePower, 0.25f, Owner.ActualPower);

            float candidateBallTime = Ball.Instance.TimeToCoverDistance(Ball.Instance.NormalizedPosition,
                target,
                power,
                true);

            if (float.IsNaN(candidateBallTime) || float.IsInfinity(candidateBallTime) || candidateBallTime <= 0f)
                candidateBallTime = Vector3.Distance(Ball.Instance.NormalizedPosition, target) / Mathf.Max(0.1f, power);

            ballTime = Mathf.Max(0.1f, candidateBallTime);
        }

        bool TryFindRandomAutoDistributionPass(out Player receiver,
            out Vector3 target,
            out float power,
            out float ballTime)
        {
            receiver = null;
            target = Owner.Position + GetUpfieldDirection() * 14f;
            power = Mathf.Clamp(Owner.ActualPower * 0.8f, 0.25f, Owner.ActualPower);
            ballTime = 1f;

            if (Owner.TeamMembers == null || Owner.TeamMembers.Count == 0)
                return false;

            List<Player> seeds = Owner.TeamMembers
                .Where(p => p != null
                    && p != Owner
                    && p.PlayerType == Utilities.Enums.PlayerTypes.InFieldPlayer)
                .OrderBy(_ => Random.value)
                .ToList();

            if (seeds.Count == 0)
                return false;

            if (seeds.Count > 1 && Owner.PrevPassReceiver != null)
                seeds.Remove(Owner.PrevPassReceiver);

            if (seeds.Count == 0)
            {
                seeds = Owner.TeamMembers
                    .Where(p => p != null
                        && p != Owner
                        && p.PlayerType == Utilities.Enums.PlayerTypes.InFieldPlayer)
                    .OrderBy(_ => Random.value)
                    .ToList();
            }

            int attempts = Mathf.Min(6, seeds.Count);
            for (int i = 0; i < attempts; i++)
            {
                Vector3 passDirection = seeds[i].Position - Owner.Position;
                passDirection.y = 0f;
                if (passDirection.sqrMagnitude <= 0.0001f)
                    passDirection = GetUpfieldDirection();

                passDirection = Quaternion.AngleAxis(Random.Range(-AutoPassDirectionJitterAngle, AutoPassDirectionJitterAngle), Vector3.up)
                    * passDirection.normalized;

                bool found = TryFindLongForwardManualPass(passDirection,
                    out receiver,
                    out target,
                    out power,
                    out ballTime,
                    true);

                if (found)
                    return true;
            }

            // final randomized attempts from upfield direction
            Vector3 upfieldDirection = GetUpfieldDirection();
            for (int i = 0; i < 3; i++)
            {
                Vector3 passDirection = Quaternion.AngleAxis(Random.Range(-40f, 40f), Vector3.up) * upfieldDirection;
                bool found = TryFindLongForwardManualPass(passDirection,
                    out receiver,
                    out target,
                    out power,
                    out ballTime,
                    true);

                if (found)
                    return true;
            }

            return false;
        }

        Player PickReceiver()
        {
            if (Owner.TeamMembers == null || Owner.TeamMembers.Count == 0)
                return null;

            List<Player> candidates = Owner.TeamMembers
                .Where(p => p != null
                    && p != Owner
                    && p.PlayerType == Utilities.Enums.PlayerTypes.InFieldPlayer)
                .OrderBy(p => Vector3.Distance(Owner.Position, p.Position))
                .Take(4)
                .ToList();

            if (candidates.Count == 0)
                return null;

            return candidates[Random.Range(0, candidates.Count)];
        }

        bool TryFindLongForwardManualPass(Vector3 passDirection,
            out Player receiver,
            out Vector3 target,
            out float power,
            out float ballTime,
            bool avoidPrevReceiver = false)
        {
            receiver = null;
            target = Owner.Position + passDirection * 10f;
            power = 0f;
            ballTime = 0f;

            if (Owner.TeamMembers == null || Owner.TeamMembers.Count == 0)
                return false;

            float minDot = Mathf.Cos((ManualForwardPassConeAngle * 0.5f) * Mathf.Deg2Rad);
            float bestScore = float.NegativeInfinity;

            foreach (Player teamMate in Owner.TeamMembers)
            {
                if (teamMate == null || teamMate == Owner)
                    continue;

                if (teamMate.PlayerType != Utilities.Enums.PlayerTypes.InFieldPlayer)
                    continue;

                Vector3 toMate = teamMate.Position - Owner.Position;
                toMate.y = 0f;
                float distance = toMate.magnitude;

                if (distance < ManualForwardPassMinDistance || distance > ManualForwardPassMaxDistance)
                    continue;

                if (distance <= 0.0001f)
                    continue;

                Vector3 toMateDir = toMate / distance;
                float dot = Vector3.Dot(passDirection, toMateDir);
                if (dot < minDot)
                    continue;

                float safetyScore = Mathf.Clamp01(GetNearestOpponentDistance(teamMate.Position) / ManualPassSafetyDistance);
                float lateralScore = 1f - Mathf.Abs(Vector3.Dot(Vector3.Cross(Vector3.up, passDirection), toMateDir));
                float distanceScore = Mathf.Clamp01(distance / ManualForwardPassMaxDistance);

                // Keep user control high: alignment dominates, with small safety/distance tie-breakers.
                float score = dot * 3.0f + lateralScore * 1.6f + safetyScore * 0.7f + distanceScore * 0.25f;
                if (teamMate == Owner.PrevPassReceiver)
                    score -= avoidPrevReceiver ? 2.5f : 0.35f;

                if (score > bestScore)
                {
                    bestScore = score;
                    receiver = teamMate;
                }
            }

            if (receiver == null)
                return false;

            target = receiver.Position;

            Vector3 receiverForward = Vector3.Scale(receiver.transform.forward, new Vector3(1f, 0f, 1f));
            if (receiverForward.sqrMagnitude > 0.0001f)
            {
                receiverForward.Normalize();
                target += receiverForward * Random.Range(1.0f, ManualForwardPassLeadDistance);
            }

            float arriveVelocity = Owner.BallPassArriveVelocity * ManualForwardPassArrivalVelocityMultiplier;
            float candidatePower = Owner.FindPower(Ball.Instance.NormalizedPosition,
                target,
                arriveVelocity);

            if (float.IsNaN(candidatePower) || float.IsInfinity(candidatePower) || candidatePower <= 0f)
                candidatePower = Owner.ActualPower * 0.9f;

            power = Mathf.Clamp(candidatePower, 0.5f, Owner.ActualPower);

            bool canReach = Owner.CanBallReachPoint(target, power, out ballTime);
            if (!canReach || ballTime <= 0f)
            {
                // fallback to direct target on receiver if lead target is too optimistic
                target = receiver.Position;

                candidatePower = Owner.FindPower(Ball.Instance.NormalizedPosition,
                    target,
                    Owner.BallPassArriveVelocity);

                if (float.IsNaN(candidatePower) || float.IsInfinity(candidatePower) || candidatePower <= 0f)
                    candidatePower = Owner.ActualPower * 0.8f;

                power = Mathf.Clamp(candidatePower, 0.5f, Owner.ActualPower);

                canReach = Owner.CanBallReachPoint(target, power, out ballTime);
                if (!canReach || ballTime <= 0f)
                {
                    float fallbackTime = Ball.Instance.TimeToCoverDistance(Ball.Instance.NormalizedPosition,
                        target,
                        power,
                        true);

                    if (float.IsNaN(fallbackTime) || float.IsInfinity(fallbackTime) || fallbackTime <= 0f)
                        fallbackTime = Vector3.Distance(Ball.Instance.NormalizedPosition, target) / Mathf.Max(0.1f, power);

                    ballTime = Mathf.Max(0.1f, fallbackTime);
                }
            }

            ballTime = Mathf.Max(0.1f, ballTime);
            return true;
        }

        bool TryFindClosestTeamMateEmergencyPass(Vector3 passDirection,
            out Player receiver,
            out Vector3 target,
            out float power,
            out float ballTime)
        {
            receiver = null;
            target = Owner.Position + GetUpfieldDirection() * 12f;
            power = Mathf.Clamp(Owner.ActualPower * 0.85f, 0.5f, Owner.ActualPower);
            ballTime = 0.8f;

            if (Owner.TeamMembers == null || Owner.TeamMembers.Count == 0)
                return false;

            float minDot = Mathf.Cos((ManualDirectionalFallbackConeAngle * 0.5f) * Mathf.Deg2Rad);
            float closestDistance = float.MaxValue;
            foreach (Player teamMate in Owner.TeamMembers)
            {
                if (teamMate == null || teamMate == Owner)
                    continue;

                if (teamMate.PlayerType != Utilities.Enums.PlayerTypes.InFieldPlayer)
                    continue;

                Vector3 toMate = teamMate.Position - Owner.Position;
                toMate.y = 0f;
                float distance = toMate.magnitude;

                if (distance < ManualForwardPassMinDistance || distance > ManualEmergencyPassMaxDistance)
                    continue;

                if (distance <= 0.0001f)
                    continue;

                Vector3 toMateDir = toMate / distance;
                if (Vector3.Dot(passDirection, toMateDir) < minDot)
                    continue;

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    receiver = teamMate;
                }
            }

            if (receiver == null)
                return false;

            target = receiver.Position;

            float candidatePower = Owner.FindPower(Ball.Instance.NormalizedPosition,
                target,
                Owner.BallPassArriveVelocity * ManualForwardPassArrivalVelocityMultiplier);

            if (float.IsNaN(candidatePower) || float.IsInfinity(candidatePower) || candidatePower <= 0f)
                candidatePower = Owner.ActualPower * 0.85f;

            power = Mathf.Clamp(candidatePower, 0.5f, Owner.ActualPower);

            bool canReach = Owner.CanBallReachPoint(target, power, out ballTime);
            if (!canReach || ballTime <= 0f)
            {
                float fallbackTime = Ball.Instance.TimeToCoverDistance(Ball.Instance.NormalizedPosition,
                    target,
                    power,
                    true);

                if (float.IsNaN(fallbackTime) || float.IsInfinity(fallbackTime) || fallbackTime <= 0f)
                    fallbackTime = Vector3.Distance(Ball.Instance.NormalizedPosition, target) / Mathf.Max(0.1f, power);

                ballTime = Mathf.Max(0.1f, fallbackTime);
            }

            ballTime = Mathf.Max(0.1f, ballTime);
            return true;
        }

        float GetNearestOpponentDistance(Vector3 position)
        {
            if (Owner.OppositionMembers == null || Owner.OppositionMembers.Count == 0)
                return ManualPassSafetyDistance;

            float nearestDistance = float.MaxValue;
            foreach (Player opponent in Owner.OppositionMembers)
            {
                if (opponent == null)
                    continue;

                float distance = Vector3.Distance(position, opponent.Position);
                if (distance < nearestDistance)
                    nearestDistance = distance;
            }

            if (nearestDistance == float.MaxValue)
                return ManualPassSafetyDistance;

            return nearestDistance;
        }

        Vector3 GetUpfieldDirection()
        {
            Vector3 upfieldDirection;
            if (Owner.OppGoal != null)
                upfieldDirection = Owner.OppGoal.Position - Owner.Position;
            else if (_refObject != null)
                upfieldDirection = Vector3.Scale(_refObject.forward, new Vector3(1f, 0f, 1f));
            else
                upfieldDirection = Owner.transform.forward;

            upfieldDirection.y = 0f;
            if (upfieldDirection.sqrMagnitude <= 0.0001f)
                upfieldDirection = Owner.transform.forward;

            upfieldDirection.y = 0f;
            if (upfieldDirection.sqrMagnitude <= 0.0001f)
                upfieldDirection = Vector3.forward;

            return upfieldDirection.normalized;
        }

        void EnsureReferenceObject()
        {
            if (_refObject == null)
                _refObject = Camera.main != null ? Camera.main.transform : Owner.transform;
        }

        void LogGoalKeeperDebug(string message)
        {
            if (MatchManager.Instance == null || !MatchManager.Instance.EnableGoalkeeperDebug)
                return;

            Debug.Log("[GK DEBUG] " + Owner.name + " :: " + message);
        }

        public override void Exit()
        {
            base.Exit();

            ClearPreviewPassReceiver();
            _hasCachedManualPassTarget = false;

            Owner.SetCanPassPreviewVisible(false);

            if (Owner.IconUserControlled != null)
                Owner.IconUserControlled.SetActive(false);

            // safety reset if we leave state before distribution happened
            if (Ball.Instance.Owner == Owner)
            {
                Ball.Instance.Owner = null;
                Ball.Instance.Rigidbody.isKinematic = false;
            }

            Owner.RPGMovement.SetMoveDirection(Vector3.zero);
            Owner.RPGMovement.SetSteeringOff();
            Owner.RPGMovement.SetTrackingOff();
            Owner.ResetSprintState();

            LogGoalKeeperDebug("Exit ControlBall");
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
