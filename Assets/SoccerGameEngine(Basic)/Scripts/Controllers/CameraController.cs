using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Controllers
{
    public class CameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField]
        Transform target;

        [SerializeField]
        bool _autoFindBallTarget = true;

        [SerializeField]
        bool _followBallOwnerWhenControlled = true;

        [SerializeField]
        bool _captureOffsetOnStart = true;

        [SerializeField]
        Vector3 _followOffset = Vector3.zero;

        [Header("Follow")]
        [SerializeField]
        [Min(0.01f)]
        float _smoothTime = 0.2f;

        [SerializeField]
        [Min(0.1f)]
        float _maxFollowSpeed = 100f;

        [SerializeField]
        bool _keepInitialY = true;

        [Header("Field Bounds")]
        [SerializeField]
        float _minX = -45f;

        [SerializeField]
        float _maxX = 45f;

        [SerializeField]
        float _minZ = -65f;

        [SerializeField]
        float _maxZ = 65f;

        [SerializeField]
        bool _drawBoundsGizmo = true;

        Vector3 _followVelocity;
        float _fixedY;
        bool _offsetInitialized;
        float _shakeDuration;
        float _shakeTimer;
        float _shakeMagnitude;
        float _shakeElapsed;
        Vector3 _shakeNoiseSeed;
        Vector3 _currentShakeOffset;
        Vector3 _shakeOffsetVelocity;

        [Header("Shake")]
        [SerializeField]
        [Min(0.1f)]
        float _shakeFrequency = 17f;

        [SerializeField]
        [Min(0.01f)]
        float _shakeSmoothTime = 0.08f;

        [SerializeField]
        Vector3 _shakeAxisMultiplier = new Vector3(1f, 0.85f, 1f);

        [Space]
        public float duration = 0;
        public float magnitude = 0;
        private void Awake()
        {
            _fixedY = transform.position.y;

            ClampBoundsIfInvalid();
            ResolveTargetIfNeeded();
            InitializeOffsetIfNeeded();

            _shakeNoiseSeed = new Vector3(
                Random.Range(0f, 1000f),
                Random.Range(0f, 1000f),
                Random.Range(0f, 1000f));

            transform.position = ClampToFieldBounds(transform.position);
        }

        private void LateUpdate()
        {
            ResolveTargetIfNeeded();
            if (target == null)
                return;

            Vector3 followPosition = GetFollowPosition();
            Vector3 desiredPosition = followPosition + _followOffset;

            if (_keepInitialY)
                desiredPosition.y = _fixedY;

            desiredPosition = ClampToFieldBounds(desiredPosition);

            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                _shakeElapsed += Time.deltaTime;

                float normalized = 1f - Mathf.Clamp01(_shakeTimer / Mathf.Max(0.01f, _shakeDuration));
                float envelope = 1f - (normalized * normalized);
                Vector3 targetOffset = EvaluateSmoothShakeOffset(envelope);

                _currentShakeOffset = Vector3.SmoothDamp(
                    _currentShakeOffset,
                    targetOffset,
                    ref _shakeOffsetVelocity,
                    Mathf.Max(0.01f, _shakeSmoothTime),
                    Mathf.Infinity,
                    Time.deltaTime);

                desiredPosition += _currentShakeOffset;
            }
            else if (_currentShakeOffset.sqrMagnitude > 0.000001f)
            {
                _currentShakeOffset = Vector3.SmoothDamp(
                    _currentShakeOffset,
                    Vector3.zero,
                    ref _shakeOffsetVelocity,
                    Mathf.Max(0.01f, _shakeSmoothTime),
                    Mathf.Infinity,
                    Time.deltaTime);

                desiredPosition += _currentShakeOffset;
            }

            transform.position = Vector3.SmoothDamp(transform.position,
                desiredPosition,
                ref _followVelocity,
                _smoothTime,
                _maxFollowSpeed,
                Time.deltaTime);
        }

        void Update()
        {
        if(Input.GetKeyDown(KeyCode.Space))
            {
                Shake(duration, magnitude);
                Debug.Log("Camera shake triggered with duration: " + duration + " and magnitude: " + magnitude);
            }
        }

        public void Shake(float duration, float magnitude)
        {
            float incomingDuration = Mathf.Max(0.01f, duration);
            float incomingMagnitude = Mathf.Max(0f, magnitude);

            if (incomingMagnitude <= 0f)
                return;

            if (_shakeTimer > 0f)
            {
                _shakeTimer = Mathf.Max(_shakeTimer, incomingDuration);
                _shakeDuration = Mathf.Max(_shakeDuration, _shakeTimer);
                _shakeMagnitude = Mathf.Max(_shakeMagnitude, incomingMagnitude);
                return;
            }

            _shakeDuration = incomingDuration;
            _shakeTimer = _shakeDuration;
            _shakeMagnitude = incomingMagnitude;
            _shakeElapsed = 0f;
            _shakeNoiseSeed = new Vector3(
                Random.Range(0f, 1000f),
                Random.Range(0f, 1000f),
                Random.Range(0f, 1000f));
        }

        Vector3 EvaluateSmoothShakeOffset(float envelope)
        {
            float frequency = Mathf.Max(0.1f, _shakeFrequency);
            float sampleTime = _shakeElapsed * frequency;

            float noiseX = Mathf.PerlinNoise(_shakeNoiseSeed.x, sampleTime) * 2f - 1f;
            float noiseY = Mathf.PerlinNoise(_shakeNoiseSeed.y, sampleTime + 23.17f) * 2f - 1f;
            float noiseZ = Mathf.PerlinNoise(_shakeNoiseSeed.z, sampleTime + 47.71f) * 2f - 1f;

            Vector3 axisNoise = new Vector3(noiseX, noiseY, noiseZ);
            Vector3 scaledNoise = Vector3.Scale(axisNoise, _shakeAxisMultiplier);

            return scaledNoise * (_shakeMagnitude * Mathf.Clamp01(envelope));
        }

        void ResolveTargetIfNeeded()
        {
            if (target != null)
                return;

            if (!_autoFindBallTarget || Ball.Instance == null)
                return;

            target = Ball.Instance.transform;
        }

        void InitializeOffsetIfNeeded()
        {
            if (_offsetInitialized)
                return;

            if (!_captureOffsetOnStart || target == null)
                return;

            _followOffset = transform.position - target.position;
            _offsetInitialized = true;
        }

        Vector3 GetFollowPosition()
        {
            if (_followBallOwnerWhenControlled
                && Ball.Instance != null
                && Ball.Instance.Owner != null)
            {
                return Ball.Instance.Owner.transform.position;
            }

            return target.position;
        }

        Vector3 ClampToFieldBounds(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, _minX, _maxX);
            position.z = Mathf.Clamp(position.z, _minZ, _maxZ);

            return position;
        }

        void ClampBoundsIfInvalid()
        {
            if (_minX > _maxX)
            {
                float temp = _minX;
                _minX = _maxX;
                _maxX = temp;
            }

            if (_minZ > _maxZ)
            {
                float temp = _minZ;
                _minZ = _maxZ;
                _maxZ = temp;
            }
        }

        public void SetTarget(Transform newTarget, bool recaptureOffset = false)
        {
            target = newTarget;

            if (recaptureOffset)
            {
                _offsetInitialized = false;
                InitializeOffsetIfNeeded();
            }
        }

        private void OnValidate()
        {
            _smoothTime = Mathf.Max(0.01f, _smoothTime);
            _maxFollowSpeed = Mathf.Max(0.1f, _maxFollowSpeed);
            _shakeFrequency = Mathf.Max(0.1f, _shakeFrequency);
            _shakeSmoothTime = Mathf.Max(0.01f, _shakeSmoothTime);

            ClampBoundsIfInvalid();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawBoundsGizmo)
                return;

            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3((_minX + _maxX) * 0.5f,
                transform.position.y,
                (_minZ + _maxZ) * 0.5f);
            Vector3 size = new Vector3(Mathf.Abs(_maxX - _minX),
                0.1f,
                Mathf.Abs(_maxZ - _minZ));

            Gizmos.DrawWireCube(center, size);
        }
    }
}
