using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.SoccerGameEngine_Basic_.Scripts.UI
{
    /// <summary>
    /// Fades the minimap when the tracked world target (ball) is visually under it on screen.
    /// Attach this to the minimap root UI object and assign a CanvasGroup.
    /// </summary>
    public class MiniMapAutoFadeByBallOcclusion : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        RectTransform _minimapRect;

        public GameObject minimapObject;
        public Button minimapOpenAndCloseButton;
        public Image minimapBackgroundImage;
        public Color minimapOpenColor;
        public Color minimapClosedColor;

        [SerializeField]
        CanvasGroup _canvasGroup;

        [SerializeField]
        Transform _trackedTarget;

        [SerializeField]
        bool _autoTrackBall = true;

        [Header("Fade")]
        [SerializeField]
        [Range(0.05f, 1f)]
        float _visibleAlpha = 1f;

        [SerializeField]
        [Range(0.05f, 1f)]
        float _fadedAlpha = 0.28f;

        [SerializeField]
        [Min(0.01f)]
        float _fadeSmoothTime = 0.12f;

        [SerializeField]
        [Min(0f)]
        float _screenPaddingPixels = 24f;

        [SerializeField]
        [Min(0f)]
        float _preFadeOffsetPixels = 36f;

        [SerializeField]
        [Min(0f)]
        float _targetYOffset = 0.2f;

        float _alphaVelocity;
        Camera _worldCamera;
        Canvas _parentCanvas;
        bool _isMinimapOpen;

        void Awake()
        {
            ResolveMiniMapReferences();
            RegisterToggleButtonListener();

            _parentCanvas = _minimapRect != null ? _minimapRect.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            _worldCamera = Camera.main;

            // Always start with minimap closed, regardless of any external bool/default state.
            SetMinimapOpenState(false);
        }

        void OnDestroy()
        {
            UnregisterToggleButtonListener();
        }

        void LateUpdate()
        {
            if (!_isMinimapOpen)
                return;

            ResolveTrackedTargetIfNeeded();
            ResolveWorldCameraIfNeeded();

            float targetAlpha = Mathf.Clamp01(_visibleAlpha);
            if (IsTargetOccludedByMinimap())
                targetAlpha = Mathf.Clamp01(_fadedAlpha);

            _canvasGroup.alpha = Mathf.SmoothDamp(
                _canvasGroup.alpha,
                targetAlpha,
                ref _alphaVelocity,
                Mathf.Max(0.01f, _fadeSmoothTime),
                Mathf.Infinity,
                Time.unscaledDeltaTime);
        }

        void ResolveMiniMapReferences()
        {
            if (minimapObject == null)
                minimapObject = gameObject;

            if (_minimapRect == null)
                _minimapRect = minimapObject.transform as RectTransform;

            if (_minimapRect == null)
                _minimapRect = transform as RectTransform;

            if (_canvasGroup == null && minimapObject != null)
                _canvasGroup = minimapObject.GetComponent<CanvasGroup>();

            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_canvasGroup == null)
            {
                GameObject target = minimapObject != null ? minimapObject : gameObject;
                _canvasGroup = target.AddComponent<CanvasGroup>();
            }
        }

        void RegisterToggleButtonListener()
        {
            if (minimapOpenAndCloseButton == null)
                return;

            minimapOpenAndCloseButton.onClick.RemoveListener(ToggleMinimapOpenClose);
            minimapOpenAndCloseButton.onClick.AddListener(ToggleMinimapOpenClose);
        }

        void UnregisterToggleButtonListener()
        {
            if (minimapOpenAndCloseButton == null)
                return;

            minimapOpenAndCloseButton.onClick.RemoveListener(ToggleMinimapOpenClose);
        }

        public void ToggleMinimapOpenClose()
        {
            SetMinimapOpenState(!_isMinimapOpen);
        }

        void SetMinimapOpenState(bool isOpen)
        {
            _isMinimapOpen = isOpen;

            if (minimapObject != null && minimapObject != gameObject)
                minimapObject.SetActive(isOpen);

            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = isOpen;
                _canvasGroup.blocksRaycasts = isOpen;

                if (!isOpen)
                {
                    _alphaVelocity = 0f;
                    _canvasGroup.alpha = 0f;
                }
                else
                {
                    _canvasGroup.alpha = Mathf.Clamp01(_visibleAlpha);
                }
            }

            UpdateMinimapArrowVisual();
        }

        void UpdateMinimapArrowVisual()
        {
            if (minimapBackgroundImage == null)
                return;

            minimapBackgroundImage.color = _isMinimapOpen ? minimapOpenColor : minimapClosedColor;
        }

        void ResolveTrackedTargetIfNeeded()
        {
            if (_trackedTarget != null)
                return;

            if (!_autoTrackBall || Ball.Instance == null)
                return;

            _trackedTarget = Ball.Instance.transform;
        }

        void ResolveWorldCameraIfNeeded()
        {
            if (_worldCamera != null)
                return;

            _worldCamera = Camera.main;
        }

        bool IsTargetOccludedByMinimap()
        {
            if (_minimapRect == null || _canvasGroup == null || _trackedTarget == null || _worldCamera == null)
                return false;

            Vector3 worldPoint = _trackedTarget.position + Vector3.up * _targetYOffset;
            Vector3 screenPoint3D = _worldCamera.WorldToScreenPoint(worldPoint);
            if (screenPoint3D.z <= 0f)
                return false;

            Vector2 screenPoint = new Vector2(screenPoint3D.x, screenPoint3D.y);
            Rect minimapScreenRect = GetScreenRect(_minimapRect, GetCanvasEventCamera());
            float totalPadding = Mathf.Max(0f, _screenPaddingPixels + _preFadeOffsetPixels);
            minimapScreenRect.xMin -= totalPadding;
            minimapScreenRect.yMin -= totalPadding;
            minimapScreenRect.xMax += totalPadding;
            minimapScreenRect.yMax += totalPadding;

            return minimapScreenRect.Contains(screenPoint);
        }

        Rect GetScreenRect(RectTransform rectTransform, Camera uiCamera)
        {
            Vector3[] worldCorners = new Vector3[4];
            rectTransform.GetWorldCorners(worldCorners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCamera, worldCorners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(uiCamera, worldCorners[2]);

            float xMin = Mathf.Min(min.x, max.x);
            float yMin = Mathf.Min(min.y, max.y);
            float xMax = Mathf.Max(min.x, max.x);
            float yMax = Mathf.Max(min.y, max.y);

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        Camera GetCanvasEventCamera()
        {
            if (_parentCanvas == null)
                return null;

            if (_parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return _parentCanvas.worldCamera;
        }

        void OnValidate()
        {
            _visibleAlpha = Mathf.Clamp01(_visibleAlpha);
            _fadedAlpha = Mathf.Clamp01(_fadedAlpha);
            _fadeSmoothTime = Mathf.Max(0.01f, _fadeSmoothTime);
            _screenPaddingPixels = Mathf.Max(0f, _screenPaddingPixels);
            _preFadeOffsetPixels = Mathf.Max(0f, _preFadeOffsetPixels);
            _targetYOffset = Mathf.Max(0f, _targetYOffset);

            if (Application.isPlaying)
                UpdateMinimapArrowVisual();
        }
    }
}
