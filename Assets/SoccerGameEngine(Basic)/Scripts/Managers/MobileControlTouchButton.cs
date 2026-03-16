using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Managers
{
    public enum MobileControlActionType
    {
        SprintHold,
        PassTap,
        ShootTap
    }

    [DisallowMultipleComponent]
    public class MobileControlTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IEndDragHandler, IPointerExitHandler, ICancelHandler
    {
        [SerializeField]
        MobileControlActionType _action = MobileControlActionType.PassTap;

        int _activePointerId = int.MinValue;
        bool _isPressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            int pointerId = eventData != null ? eventData.pointerId : int.MinValue;
            Press(pointerId);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            int pointerId = eventData != null ? eventData.pointerId : int.MinValue;
            TryReleaseForPointer(pointerId);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isPressed)
                return;

            // Sprint should stay active while finger is held, even if dragged slightly.
            if (_action == MobileControlActionType.SprintHold)
                return;

            int pointerId = eventData != null ? eventData.pointerId : int.MinValue;
            TryReleaseForPointer(pointerId);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isPressed)
                return;

            // Sprint should stay active while finger is held, even if pointer exits button bounds.
            if (_action == MobileControlActionType.SprintHold)
                return;

            int pointerId = eventData != null ? eventData.pointerId : int.MinValue;
            TryReleaseForPointer(pointerId);
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (_isPressed)
                Release();
        }

        // Optional EventTrigger hooks (can be wired from UI EventTrigger component).
        public void PointerDownEvent()
        {
            Press(int.MinValue);
        }

        public void PointerUpEvent()
        {
            Release();
        }

        public void PointerDownEvent(BaseEventData eventData)
        {
            int pointerId = int.MinValue;
            if (eventData is PointerEventData pointerEvent)
                pointerId = pointerEvent.pointerId;

            Press(pointerId);
        }

        public void PointerUpEvent(BaseEventData eventData)
        {
            int pointerId = int.MinValue;
            if (eventData is PointerEventData pointerEvent)
                pointerId = pointerEvent.pointerId;

            TryReleaseForPointer(pointerId);
        }

        public void ForceRelease()
        {
            Release();
        }

        void OnDisable()
        {
            if (_isPressed)
                Release();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && _isPressed)
                Release();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _isPressed)
                Release();
        }

        void Press(int pointerId)
        {
            if (_isPressed)
                return;

            _activePointerId = pointerId;
            _isPressed = true;

            if (MobileControlsInput.Instance == null)
                return;

            switch (_action)
            {
                case MobileControlActionType.SprintHold:
                    MobileControlsInput.Instance.SprintDown();
                    break;

                case MobileControlActionType.PassTap:
                    MobileControlsInput.Instance.PressPass();
                    break;

                case MobileControlActionType.ShootTap:
                    MobileControlsInput.Instance.PressShoot();
                    break;
            }
        }

        void TryReleaseForPointer(int pointerId)
        {
            if (!_isPressed)
                return;

            if (pointerId == int.MinValue || _activePointerId == int.MinValue || _activePointerId == pointerId)
                Release();
        }

        void Release()
        {
            if (!_isPressed)
                return;

            if (MobileControlsInput.Instance != null && _action == MobileControlActionType.SprintHold)
                MobileControlsInput.Instance.SprintUp();

            _isPressed = false;
            _activePointerId = int.MinValue;
        }
    }
}
