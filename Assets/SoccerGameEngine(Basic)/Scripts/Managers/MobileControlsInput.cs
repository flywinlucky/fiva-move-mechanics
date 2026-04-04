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
        bool _defendQueued;
        float _defendTapCharge;
        float _lastDefendTapTime;
        int _defendTapSequence;
        float _lastUserInteractionTime;

        const float DefendTapChargePerTap = 1f;
        const float DefendTapChargeMax = 8f;
        const float DefendTapDecayPerSecond = 1.2f;

        bool ActionButtonsEnabled => _isMobileControls || _allowUiActionButtonsOnDesktop;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MobileControlsInput] Multiple instances found. Keeping the first instance.", this);
                return;
            }

            Instance = this;
            _lastUserInteractionTime = Time.time;
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

            RegisterUserInteractionInternal();

            return movement;
        }

        void RegisterUserInteractionInternal()
        {
            _lastUserInteractionTime = Time.time;
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

        bool ConsumeDefendInternal()
        {
            if (!ActionButtonsEnabled)
            {
                _defendQueued = false;
                return false;
            }

            bool pressed = _defendQueued;
            _defendQueued = false;
            return pressed;
        }

        void RefreshDefendCharge()
        {
            if (_defendTapCharge <= 0f)
                return;

            float elapsed = Mathf.Max(0f, Time.time - _lastDefendTapTime);
            if (elapsed <= 0f)
                return;

            _defendTapCharge = Mathf.Max(0f, _defendTapCharge - (elapsed * DefendTapDecayPerSecond));
            _lastDefendTapTime = Time.time;
        }

        void RegisterDefendTapInternal()
        {
            if (!ActionButtonsEnabled)
                return;

            RefreshDefendCharge();
            _defendTapCharge = Mathf.Clamp(_defendTapCharge + DefendTapChargePerTap, 0f, DefendTapChargeMax);
            _lastDefendTapTime = Time.time;
            _defendTapSequence++;
            _defendQueued = true;
        }

        float ConsumeDefendDuelBonusInternal(bool userIsAttacker, float userStamina01, float opponentStamina01)
        {
            if (!ActionButtonsEnabled)
                return 0f;

            RefreshDefendCharge();
            if (_defendTapCharge <= 0.001f)
                return 0f;

            float tapPower01 = Mathf.Clamp01(_defendTapCharge / DefendTapChargeMax);
            float staminaDelta = Mathf.Clamp(userStamina01 - opponentStamina01, -1f, 1f);

            float staminaFactor = Mathf.Lerp(0.9f, 1.15f, (staminaDelta + 1f) * 0.5f);
            float minBonus = userIsAttacker ? 0.04f : 0.03f;
            float maxBonus = userIsAttacker ? 0.24f : 0.22f;
            float bonus = Mathf.Lerp(minBonus, maxBonus, tapPower01) * staminaFactor;

            float randomAdaptiveBoost = Random.Range(0f, 0.03f) * tapPower01;
            bonus += randomAdaptiveBoost;

            float consumeAmount = userIsAttacker ? 1.9f : 1.6f;
            _defendTapCharge = Mathf.Max(0f, _defendTapCharge - consumeAmount);
            _lastDefendTapTime = Time.time;

            return Mathf.Clamp(bonus, 0f, 0.30f);
        }

        void ResetQueuedInputs()
        {
            _sprintHeld = false;
            _passQueued = false;
            _shootQueued = false;
            _defendQueued = false;
            _defendTapCharge = 0f;
            _lastUserInteractionTime = Time.time;
        }

        void ResetQueuedTapActions()
        {
            _passQueued = false;
            _shootQueued = false;
            _defendQueued = false;
            _defendTapCharge = 0f;
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
            if (_sprintHeld)
                RegisterUserInteractionInternal();
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
            {
                _passQueued = true;
                RegisterUserInteractionInternal();
            }
        }

        public void PressShoot()
        {
            if (ActionButtonsEnabled)
            {
                _shootQueued = true;
                RegisterUserInteractionInternal();
            }
        }

        public void PressDefend()
        {
            RegisterDefendTapInternal();
            RegisterUserInteractionInternal();
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

        public static float ConsumeDefendDuelBonus(bool userIsAttacker, float userStamina01, float opponentStamina01)
        {
            if (Instance == null)
                return 0f;

            return Instance.ConsumeDefendDuelBonusInternal(userIsAttacker,
                Mathf.Clamp01(userStamina01),
                Mathf.Clamp01(opponentStamina01));
        }

        public static bool ConsumeDefendPressed()
        {
            return Instance != null && Instance.ConsumeDefendInternal();
        }

        public static bool WasDefendTappedRecently(float maxAgeSeconds)
        {
            if (Instance == null)
                return false;

            float age = Time.time - Instance._lastDefendTapTime;
            return age >= 0f && age <= Mathf.Max(0.01f, maxAgeSeconds);
        }

        public static int GetDefendTapSequence()
        {
            if (Instance == null)
                return 0;

            return Mathf.Max(0, Instance._defendTapSequence);
        }

        public static void ClearQueuedTapActions()
        {
            if (Instance == null)
                return;

            Instance.ResetQueuedTapActions();
        }

        public static float SecondsSinceLastUserInteraction()
        {
            if (Instance == null)
                return float.PositiveInfinity;

            return Mathf.Max(0f, Time.time - Instance._lastUserInteractionTime);
        }

        public static void RegisterExternalInteraction()
        {
            if (Instance == null)
                return;

            Instance.RegisterUserInteractionInternal();
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
