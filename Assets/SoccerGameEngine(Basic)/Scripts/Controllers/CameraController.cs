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

        private void Awake()
        {
            _fixedY = transform.position.y;

            ClampBoundsIfInvalid();
            ResolveTargetIfNeeded();
            InitializeOffsetIfNeeded();

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
                float t = Mathf.Clamp01(_shakeTimer / Mathf.Max(0.01f, _shakeDuration));
                Vector2 noise = Random.insideUnitCircle * (_shakeMagnitude * t);
                desiredPosition += new Vector3(noise.x, noise.y, 0f);
            }

            transform.position = Vector3.SmoothDamp(transform.position,
                desiredPosition,
                ref _followVelocity,
                _smoothTime,
                _maxFollowSpeed,
                Time.deltaTime);
        }

        public void Shake(float duration, float magnitude)
        {
            _shakeDuration = Mathf.Max(0.01f, duration);
            _shakeTimer = _shakeDuration;
            _shakeMagnitude = Mathf.Max(0f, magnitude);
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
