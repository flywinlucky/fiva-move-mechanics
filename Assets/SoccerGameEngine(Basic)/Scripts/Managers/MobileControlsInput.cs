using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Managers
{
    [DisallowMultipleComponent]
    public class MobileControlsInput : MonoBehaviour
    {
        public static MobileControlsInput Instance { get; private set; }

        [Header("Mobile Controls")]
        [SerializeField]
        bool _isMobileControls;

        [SerializeField]
        UltimateJoystick _movementJoystick;

        [SerializeField]
        [Range(0f, 1f)]
        float _movementDeadZone = 0.1f;

        [SerializeField]
        bool _normalizeMovement = true;

        [SerializeField]
        bool _allowUiActionButtonsOnDesktop = true;

        bool _sprintHeld;
        bool _passQueued;
        bool _shootQueued;

        bool ActionButtonsEnabled => _isMobileControls || _allowUiActionButtonsOnDesktop;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MobileControlsInput] Multiple instances found. Keeping the first instance.", this);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        Vector2 ReadMovementInternal()
        {
            if (!_isMobileControls || _movementJoystick == null)
                return Vector2.zero;

            Vector2 movement = new Vector2(_movementJoystick.HorizontalAxis, _movementJoystick.VerticalAxis);
            float magnitude = movement.magnitude;
            if (magnitude <= _movementDeadZone)
                return Vector2.zero;

            if (_normalizeMovement && magnitude > 1f)
                movement /= magnitude;

            return movement;
        }

        bool ConsumePassInternal()
        {
            if (!ActionButtonsEnabled)
            {
                _passQueued = false;
                return false;
            }

            bool pressed = _passQueued;
            _passQueued = false;
            return pressed;
        }

        bool ConsumeShootInternal()
        {
            if (!ActionButtonsEnabled)
            {
                _shootQueued = false;
                return false;
            }

            bool pressed = _shootQueued;
            _shootQueued = false;
            return pressed;
        }

        void ResetQueuedInputs()
        {
            _sprintHeld = false;
            _passQueued = false;
            _shootQueued = false;
        }

        public void SetMobileControlsEnabled(bool enabled)
        {
            _isMobileControls = enabled;

            if (!enabled && !_allowUiActionButtonsOnDesktop)
                ResetQueuedInputs();
        }

        public void ToggleMobileControls()
        {
            SetMobileControlsEnabled(!_isMobileControls);
        }

        public void SetSprintPressed(bool pressed)
        {
            _sprintHeld = ActionButtonsEnabled && pressed;
        }

        public void SprintDown()
        {
            SetSprintPressed(true);
        }

        public void SprintUp()
        {
            SetSprintPressed(false);
        }

        public void PressPass()
        {
            if (ActionButtonsEnabled)
                _passQueued = true;
        }

        public void PressShoot()
        {
            if (ActionButtonsEnabled)
                _shootQueued = true;
        }

        public static bool IsEnabled => Instance != null && Instance._isMobileControls;

        public static Vector2 ReadMovementInput()
        {
            return Instance != null ? Instance.ReadMovementInternal() : Vector2.zero;
        }

        public static bool IsSprintHeld()
        {
            return Instance != null && Instance.ActionButtonsEnabled && Instance._sprintHeld;
        }

        public static bool ConsumePassPressed()
        {
            return Instance != null && Instance.ConsumePassInternal();
        }

        public static bool ConsumeShootPressed()
        {
            return Instance != null && Instance.ConsumeShootInternal();
        }

        public bool IsMobileControls
        {
            get => _isMobileControls;
            set => SetMobileControlsEnabled(value);
        }

        public UltimateJoystick MovementJoystick
        {
            get => _movementJoystick;
            set => _movementJoystick = value;
        }
    }
}
