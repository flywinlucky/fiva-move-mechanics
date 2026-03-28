using System;
using Patterns.Singleton;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Entities
{
    public class Ball : Singleton<Ball>
    {
        [SerializeField]
        public Transform ball_model_position;

        [SerializeField]
        public GameObject ball_model;
        [SerializeField]
        [Min(0)]
        float _friction = 3f;

        [SerializeField]
        [Min(0)]
        float _gravity = 9.11f;
      [SerializeField]
        GameObject _iconBallControlled;
        [SerializeField]
        string _groundMaskName;

        [SerializeField]
        [Range(0f, 3f)]
        float _ownerRecaptureBlockDuration = 1f;

        [Header("Visual Roll")]
        [SerializeField]
        bool _autoFindBallModel = true;

        [SerializeField]
        [Range(0f, 3f)]
        float _visualRollSpeedMultiplier = 1f;

        [SerializeField]
        [Range(0f, 1f)]
        float _minRollSpeed = 0.05f;

        [SerializeField]
        [Range(0f, 2f)]
        float _airSpinMultiplier = 0.25f;

        [SerializeField]
        [Range(90f, 2880f)]
        float _maxVisualRollDegreesPerSecond = 1440f;

        [Header("Casual Flight")]
        [SerializeField]
        bool _useCasualFlight = true;

        [SerializeField]
        [Min(0f)]
        float _groundPassMaxDistance = 9f;

        [SerializeField]
        [Min(0f)]
        float _liftDistanceStart = 10f;

        [SerializeField]
        [Min(0.1f)]
        float _liftDistanceMax = 45f;

        [SerializeField]
        [Min(0f)]
        float _liftPowerStart = 8f;

        [SerializeField]
        [Min(0.1f)]
        float _liftPowerMax = 28f;

        [SerializeField]
        bool _useBallisticKickForLongDistance = true;

        [SerializeField]
        [Min(0f)]
        float _ballisticKickDistanceStart = 16f;

        [SerializeField]
        bool _autoPowerByDistance = true;

        [SerializeField]
        [Min(0f)]
        float _powerBoostDistanceStart = 14f;

        [SerializeField]
        [Min(0.1f)]
        float _powerBoostDistanceMax = 50f;

        [SerializeField]
        [Min(0f)]
        float _maxDistancePowerBoost = 10f;

        [SerializeField]
        AnimationCurve _distancePowerBoostCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 0.45f),
            new Keyframe(1f, 1f));

        [SerializeField]
        [Range(0f, 20f)]
        float _minLiftVelocity = 1.2f;

        [SerializeField]
        [Range(0f, 30f)]
        float _maxLiftVelocity = 8.5f;

        [SerializeField]
        AnimationCurve _distanceLiftCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 0.55f),
            new Keyframe(1f, 1f));

        [Header("Adaptive Flight Profiles")]
        [SerializeField]
        bool _useAdaptiveFlightProfiles = true;

        [SerializeField]
        [Range(0f, 2f)]
        float _drivenLiftMultiplier = 0.7f;

        [SerializeField]
        [Range(0f, 2f)]
        float _lobLiftMultiplier = 1.15f;

        [SerializeField]
        [Range(0f, 1f)]
        float _distanceWeightForLob = 0.65f;

        [SerializeField]
        AnimationCurve _drivenDistanceLiftCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 0.35f),
            new Keyframe(1f, 0.7f));

        [SerializeField]
        AnimationCurve _lobDistanceLiftCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 0.75f),
            new Keyframe(1f, 1f));

        [Header("Physics Safety")]
        [SerializeField]
        bool _autoConfigureRigidbody = true;

        [SerializeField]
        bool _forceContinuousCollision = true;

        [SerializeField]
        bool _forceInterpolate = true;

        [SerializeField]
        bool _forceUseGravity = true;

        [SerializeField]
        [Range(0f, 0.5f)]
        float _postKickFrictionDelay = 0.12f;

        bool _isGrounded;
        float _rayCastDistance;
        int _groundMask;
        RaycastHit _hit;
        Vector3 _frictionVector;
        Vector3 _rayCastStartPosition;
        float _ignoreFrictionUntilTime;
        Transform _ballModelTransform;
        Vector3 _previousVisualPosition;
        bool _hasPreviousVisualPosition;

        public float LastKickLaunchSpeed { get; private set; }

        public delegate void BallLaunched(float flightTime, float velocity, Vector3 initial, Vector3 target);

        public BallLaunched OnBallLaunched;
        public BallLaunched OnBallShot;

        public float Friction { get => -_friction; set => _friction = value; }

        Player _owner;
        public Player Owner
        {
            get => _owner;
            set
            {
                Player previousOwner = _owner;
                _owner = value;

                // Prevent ping-pong possession: previous owner cannot instantly retake after losing the ball.
                if (previousOwner != null && previousOwner != value)
                {
                    previousOwner.BallRecoveryBlockedUntil = Mathf.Max(
                        previousOwner.BallRecoveryBlockedUntil,
                        Time.time + Mathf.Max(0f, _ownerRecaptureBlockDuration));
                }

                //if (_iconBallControlled != null)
                    //_iconBallControlled.SetActive(_owner == null);
            }
        }

        public Rigidbody Rigidbody { get; set; }
        public SphereCollider SphereCollider { get; set; }

        public override void Awake()
        {
            base.Awake();

            //get the components
            Rigidbody = GetComponent<Rigidbody>();
            SphereCollider = GetComponent<SphereCollider>();

            EnsurePhysicsComponents();
            ResolveBallModel();

            //init some variables
            //if (_iconBallControlled != null)
                //_iconBallControlled.SetActive(true);
            _groundMask = LayerMask.GetMask(_groundMaskName);
            _rayCastDistance = SphereCollider.radius + 0.05f;
        }

        void EnsurePhysicsComponents()
        {
            if (Rigidbody == null)
                Rigidbody = GetComponent<Rigidbody>();

            if (SphereCollider == null)
                SphereCollider = GetComponent<SphereCollider>();

            if (Rigidbody == null || SphereCollider == null)
            {
                Debug.LogWarning("Ball is missing Rigidbody or SphereCollider. Physics may not behave correctly.", this);
                return;
            }

            if (!_autoConfigureRigidbody)
                return;

            if (_forceUseGravity)
                Rigidbody.useGravity = true;

            if (_forceContinuousCollision)
                Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (_forceInterpolate)
                Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            Rigidbody.maxAngularVelocity = Mathf.Max(7f, Rigidbody.maxAngularVelocity);
        }

        private void FixedUpdate()
        {
            ApplyFriction();
        }

        private void LateUpdate()
        {
            UpdateBallVisualRotation(Time.deltaTime);
        }

        void ResolveBallModel()
        {
            if (ball_model != null)
            {
                _ballModelTransform = ball_model.transform;
                return;
            }

            if (ball_model_position != null)
            {
                _ballModelTransform = ball_model_position;
                return;
            }

            if (_autoFindBallModel && transform.childCount > 0)
                _ballModelTransform = transform.GetChild(0);
        }

        void UpdateBallVisualRotation(float deltaTime)
        {
            if (_ballModelTransform == null)
                ResolveBallModel();

            if (_ballModelTransform == null || Rigidbody == null || SphereCollider == null)
                return;

            if (!_hasPreviousVisualPosition)
            {
                _previousVisualPosition = Position;
                _hasPreviousVisualPosition = true;
            }

            Vector3 displacement = Position - _previousVisualPosition;
            Vector3 displacementVelocity = deltaTime > 0.0001f ? displacement / deltaTime : Vector3.zero;
            _previousVisualPosition = Position;

            Vector3 velocity = Rigidbody.velocity;
            if (Owner != null)
            {
                // During close control the ball is teleported each frame, so displacement gives the best roll hint.
                velocity = displacementVelocity;
            }
            else if (displacementVelocity.sqrMagnitude > velocity.sqrMagnitude)
            {
                velocity = displacementVelocity;
            }

            float speed = velocity.magnitude;
            if (speed <= _minRollSpeed)
                return;

            Vector3 planarVelocity = velocity;
            planarVelocity.y = 0f;

            float radius = Mathf.Max(0.01f, SphereCollider.radius);
            float angularSpeedDeg = Mathf.Rad2Deg * (speed / radius) * Mathf.Max(0f, _visualRollSpeedMultiplier);
            angularSpeedDeg = Mathf.Min(angularSpeedDeg, _maxVisualRollDegreesPerSecond);

            Vector3 rollAxis;
            if (planarVelocity.sqrMagnitude > 0.0001f)
            {
                rollAxis = Vector3.Cross(Vector3.up, planarVelocity.normalized);
            }
            else
            {
                // In air or near-vertical travel, keep a subtle spin so the ball still feels alive.
                rollAxis = Rigidbody.angularVelocity.sqrMagnitude > 0.0001f
                    ? Rigidbody.angularVelocity.normalized
                    : _ballModelTransform.right;
                angularSpeedDeg *= Mathf.Clamp01(_airSpinMultiplier);
            }

            _ballModelTransform.Rotate(rollAxis, angularSpeedDeg * deltaTime, Space.World);
        }

        /// <summary>
        /// Applies friction to this instance
        /// </summary>
        public void ApplyFriction()
        {
            //get the direction the ball is travelling
            _frictionVector = Rigidbody.velocity.normalized;
            _frictionVector.y = 0f;

            //calculate the actual friction
            _frictionVector *= -1 * _friction;

            //calculate the raycast start positiotn
            _rayCastStartPosition = transform.position + SphereCollider.radius * Vector3.up;

            //check if the ball is touching with the pitch
            //if yes apply the ground friction force
            //else apply the air friction
            _isGrounded = Physics.Raycast(_rayCastStartPosition,
                Vector3.down,
                out _hit,
                _rayCastDistance,
                _groundMask);

            //apply friction if grounded
            if (_isGrounded && Time.time >= _ignoreFrictionUntilTime)
                Rigidbody.AddForce(_frictionVector);

#if UNITY_EDITOR
            Debug.DrawRay(_rayCastStartPosition, 
                Vector3.down * _rayCastDistance, 
                Color.red);
#endif

        }

        /// <summary>
        /// Finds the power needed to kick an entity to reach it's destination
        /// with the specifed velocity
        /// </summary>
        /// <param name="from">The initial position</param>
        /// <param name="to">The final position</param>
        /// <param name="finalVelocity">The initial velocity</param>
        /// <returns></returns>
        public float FindPower(Vector3 from, Vector3 to, float finalVelocity)
        {
            // v^2 = u^2 + 2as => u^2 = v^2 - 2as => u = root(v^2 - 2as)
            return Mathf.Sqrt(Mathf.Pow(finalVelocity, 2f) - (2 * -_friction * Vector3.Distance(from, to)));
        }

        /// <summary>
        /// Kicks the ball to the target
        /// </summary>
        /// <param name="to"></param>
        /// <param name="power"></param>
        public void Kick(Vector3 to, float power)
        {
            Vector3 from = Position;
            Vector3 toTarget = to - from;
            Vector3 planarDirection = new Vector3(toTarget.x, 0f, toTarget.z);
            float planarDistance = planarDirection.magnitude;

            if (planarDistance <= 0.0001f)
                planarDirection = transform.forward;
            else
                planarDirection /= planarDistance;

            float launchSpeed = Mathf.Max(0.1f, power);
            launchSpeed = ApplyDistancePowerBoost(planarDistance, launchSpeed);

            if (_useCasualFlight
                && _useBallisticKickForLongDistance
                && planarDistance >= Mathf.Max(0f, _ballisticKickDistanceStart))
            {
                Launch(launchSpeed, to);
                return;
            }

            float liftVelocity = EvaluateLiftVelocity(planarDistance, launchSpeed);

            Vector3 velocity = planarDirection * launchSpeed;
            velocity.y = liftVelocity;

            //change the velocity
            Rigidbody.velocity = velocity;
            LastKickLaunchSpeed = velocity.magnitude;
            _ignoreFrictionUntilTime = Time.time + Mathf.Max(0f, _postKickFrictionDelay);

            //invoke the ball launched event
            BallLaunched temp = OnBallLaunched;
            if (temp != null)
                temp.Invoke(0f, launchSpeed, NormalizedPosition, to);
        }

        float ApplyDistancePowerBoost(float planarDistance, float requestedPower)
        {
            if (!_useCasualFlight || !_autoPowerByDistance)
                return requestedPower;

            float start = Mathf.Max(0f, _powerBoostDistanceStart);
            float end = Mathf.Max(start + 0.1f, _powerBoostDistanceMax);
            float distance01 = Mathf.InverseLerp(start, end, planarDistance);
            if (distance01 <= 0f)
                return requestedPower;

            float curve = _distancePowerBoostCurve != null
                ? Mathf.Clamp01(_distancePowerBoostCurve.Evaluate(distance01))
                : distance01;

            float boost = Mathf.Max(0f, _maxDistancePowerBoost) * curve;
            return requestedPower + boost;
        }

        float EvaluateLiftVelocity(float planarDistance, float launchSpeed)
        {
            if (!_useCasualFlight)
                return 0f;

            if (planarDistance <= _groundPassMaxDistance)
                return 0f;

            float distanceStart = Mathf.Max(0f, _liftDistanceStart);
            float distanceMax = Mathf.Max(distanceStart + 0.1f, _liftDistanceMax);
            float powerStart = Mathf.Max(0f, _liftPowerStart);
            float powerMax = Mathf.Max(powerStart + 0.1f, _liftPowerMax);

            float distance01 = Mathf.InverseLerp(distanceStart, distanceMax, planarDistance);
            float power01 = Mathf.InverseLerp(powerStart, powerMax, launchSpeed);
            float curve = _distanceLiftCurve != null ? _distanceLiftCurve.Evaluate(distance01) : distance01;
            float lift01;

            if (_useAdaptiveFlightProfiles)
            {
                float drivenCurve = _drivenDistanceLiftCurve != null
                    ? _drivenDistanceLiftCurve.Evaluate(distance01)
                    : curve;
                float lobCurve = _lobDistanceLiftCurve != null
                    ? _lobDistanceLiftCurve.Evaluate(distance01)
                    : curve;

                float distanceWeight = Mathf.Clamp01(_distanceWeightForLob);
                float styleLob = Mathf.Clamp01(distance01 * distanceWeight + power01 * (1f - distanceWeight));

                float drivenLift01 = Mathf.Clamp01(drivenCurve * power01) * Mathf.Max(0f, _drivenLiftMultiplier);
                float lobLift01 = Mathf.Clamp01(lobCurve * power01) * Mathf.Max(0f, _lobLiftMultiplier);
                lift01 = Mathf.Clamp01(Mathf.Lerp(drivenLift01, lobLift01, styleLob));
            }
            else
            {
                lift01 = Mathf.Clamp01(curve * power01);
            }

            float minLift = Mathf.Max(0f, _minLiftVelocity);
            float maxLift = Mathf.Max(minLift, _maxLiftVelocity);
            return Mathf.Lerp(minLift, maxLift, lift01);
        }

        public void Launch(float power, Vector3 final)
        {
            //set the initial position
            Vector3 initial = Position;

            //find the direction vectors
            Vector3 toTarget = final - initial;
            Vector3 toTargetXZ = toTarget;
            toTargetXZ.y = 0;

            //find the time to target
            float time = toTargetXZ.magnitude / power;

            // calculate starting speeds for xz and y. Physics forumulase deltaX = v0 * t + 1/2 * a * t * t
            // where a is "-gravity" but only on the y plane, and a is 0 in xz plane.
            // so xz = v0xz * t => v0xz = xz / t
            // and y = v0y * t - 1/2 * gravity * t * t => v0y * t = y + 1/2 * gravity * t * t => v0y = y / t + 1/2 * gravity * t
            toTargetXZ = toTargetXZ.normalized * toTargetXZ.magnitude / time;

            //set the y-velocity
            Vector3 velocity = toTargetXZ;
            velocity.y = toTarget.y / time + (0.5f * _gravity * time);

            //return the velocity
            Rigidbody.velocity = velocity;
            LastKickLaunchSpeed = velocity.magnitude;
            _ignoreFrictionUntilTime = Time.time + Mathf.Max(0f, _postKickFrictionDelay);

            //invoke the ball launched event
            BallLaunched temp = OnBallLaunched;
            if (temp != null)
                temp.Invoke(time, power, initial, final);
        }

        public void Trap()
        {
            Rigidbody.angularVelocity = Vector3.zero;
            Rigidbody.velocity = Vector3.zero;
            _hasPreviousVisualPosition = false;
        }

        public float HeightAbovePitch => Mathf.Max(0f, Position.y);

        public float TimeToCoverDistance(Vector3 from, Vector3 to, float initialVelocity, bool factorInFriction = true)
        {
            //find the distance
            float distance = Vector3.Distance(from, to);

            //if I'm not factoring friction or I'm factoring in friction but no friction has been specified
            //simply assume there is no friction(ball is self accelerating)
            if(!factorInFriction || (factorInFriction && _friction == 0))
            {
                return distance / initialVelocity;
            }
            else
            {
                // v^2 = u^2 + 2as
                float v_squared = Mathf.Pow(initialVelocity, 2f) + (2 * _friction * Vector3.Distance(from, to));

                //if v_squared is less thatn or equal to zero it means we can't reach the target
                if (v_squared <= 0)
                    return -1.0f;

                // t = v-u
                //     ---
                //      a
                return (Mathf.Sqrt(v_squared) - initialVelocity) / (_friction);
            }
        }

        /// <summary>
        /// Get the normalized ball position
        /// </summary>
        public Vector3 NormalizedPosition
        {
            get
            {
                return new Vector3(transform.position.x, 0f, transform.position.z);
            }

            set
            {
                transform.position = new Vector3(value.x, 0f, value.z);
            }
        }

        public Vector3 Position
        {
            get
            {
                return transform.position;
            }

            set
            {
                transform.position = value;
            }
        }

        private void OnValidate()
        {
            _friction = Mathf.Max(0f, _friction);
            _gravity = Mathf.Max(0f, _gravity);
            _visualRollSpeedMultiplier = Mathf.Max(0f, _visualRollSpeedMultiplier);
            _minRollSpeed = Mathf.Clamp(_minRollSpeed, 0f, 1f);
            _airSpinMultiplier = Mathf.Clamp(_airSpinMultiplier, 0f, 2f);
            _maxVisualRollDegreesPerSecond = Mathf.Clamp(_maxVisualRollDegreesPerSecond, 90f, 2880f);
            _drivenLiftMultiplier = Mathf.Clamp(_drivenLiftMultiplier, 0f, 2f);
            _lobLiftMultiplier = Mathf.Clamp(_lobLiftMultiplier, 0f, 2f);
            _distanceWeightForLob = Mathf.Clamp01(_distanceWeightForLob);

            _groundPassMaxDistance = Mathf.Max(0f, _groundPassMaxDistance);
            _liftDistanceStart = Mathf.Max(0f, _liftDistanceStart);
            _liftDistanceMax = Mathf.Max(_liftDistanceStart + 0.1f, _liftDistanceMax);
            _liftPowerStart = Mathf.Max(0f, _liftPowerStart);
            _liftPowerMax = Mathf.Max(_liftPowerStart + 0.1f, _liftPowerMax);
            _ballisticKickDistanceStart = Mathf.Max(0f, _ballisticKickDistanceStart);
            _minLiftVelocity = Mathf.Max(0f, _minLiftVelocity);
            _maxLiftVelocity = Mathf.Max(_minLiftVelocity, _maxLiftVelocity);
            _powerBoostDistanceStart = Mathf.Max(0f, _powerBoostDistanceStart);
            _powerBoostDistanceMax = Mathf.Max(_powerBoostDistanceStart + 0.1f, _powerBoostDistanceMax);
            _maxDistancePowerBoost = Mathf.Max(0f, _maxDistancePowerBoost);
            _postKickFrictionDelay = Mathf.Clamp(_postKickFrictionDelay, 0f, 0.5f);

            if (_distanceLiftCurve == null || _distanceLiftCurve.length == 0)
            {
                _distanceLiftCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.5f, 0.55f),
                    new Keyframe(1f, 1f));
            }

            if (_distancePowerBoostCurve == null || _distancePowerBoostCurve.length == 0)
            {
                _distancePowerBoostCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.5f, 0.45f),
                    new Keyframe(1f, 1f));
            }

            if (_drivenDistanceLiftCurve == null || _drivenDistanceLiftCurve.length == 0)
            {
                _drivenDistanceLiftCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.5f, 0.35f),
                    new Keyframe(1f, 0.7f));
            }

            if (_lobDistanceLiftCurve == null || _lobDistanceLiftCurve.length == 0)
            {
                _lobDistanceLiftCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.5f, 0.75f),
                    new Keyframe(1f, 1f));
            }

            if (ball_model != null)
                _ballModelTransform = ball_model.transform;
            else if (ball_model_position != null)
                _ballModelTransform = ball_model_position;
        }
    }
}
