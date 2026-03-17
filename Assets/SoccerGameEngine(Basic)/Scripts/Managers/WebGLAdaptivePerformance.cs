using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Managers
{
    [DisallowMultipleComponent]
    public class WebGLAdaptivePerformance : MonoBehaviour
    {
        static WebGLAdaptivePerformance _instance;

        [Header("Runtime")]
        [SerializeField]
        bool _enableInEditor = false;

        [SerializeField]
        [Range(20, 120)]
        int _targetFrameRate = 60;

        [SerializeField]
        [Range(0.5f, 5f)]
        float _sampleWindowSeconds = 1.25f;

        [SerializeField]
        [Range(1f, 15f)]
        float _qualityStepCooldownSeconds = 4f;

        [Header("Adaptive Thresholds")]
        [SerializeField]
        [Range(10f, 120f)]
        float _lowerQualityFpsThreshold = 52f;

        [SerializeField]
        [Range(10f, 120f)]
        float _increaseQualityFpsThreshold = 58f;

        [SerializeField]
        [Range(0f, 10f)]
        float _hysteresisFps = 2f;

        [Header("Quality Bounds")]
        [SerializeField]
        int _minQualityIndex = 0;

        [SerializeField]
        int _maxQualityIndexForWebGL = 2;

        float _sampleTime;
        int _sampleFrames;
        float _lastQualityChangeTime;

        bool ShouldRun
        {
            get
            {
#if UNITY_WEBGL
                return true;
#else
                return _enableInEditor;
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            GameObject gameObject = new GameObject("WebGLAdaptivePerformance");
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<WebGLAdaptivePerformance>();
        }

        void Awake()
        {
            if (!ShouldRun)
            {
                Destroy(gameObject);
                return;
            }

            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = Mathf.Max(20, _targetFrameRate);
            QualitySettings.vSyncCount = 0;

            int maxIndex = Mathf.Clamp(_maxQualityIndexForWebGL, 0, QualitySettings.names.Length - 1);
            int minIndex = Mathf.Clamp(_minQualityIndex, 0, maxIndex);
            _minQualityIndex = minIndex;
            _maxQualityIndexForWebGL = maxIndex;

            int current = QualitySettings.GetQualityLevel();
            int clampedStart = Mathf.Clamp(current, minIndex, maxIndex);
            if (clampedStart != current)
                QualitySettings.SetQualityLevel(clampedStart, true);

            _lastQualityChangeTime = Time.unscaledTime;
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        void Update()
        {
            if (!ShouldRun)
                return;

            _sampleFrames++;
            _sampleTime += Time.unscaledDeltaTime;

            if (_sampleTime < _sampleWindowSeconds)
                return;

            float fps = _sampleFrames / Mathf.Max(0.0001f, _sampleTime);
            _sampleFrames = 0;
            _sampleTime = 0f;

            if (Time.unscaledTime - _lastQualityChangeTime < _qualityStepCooldownSeconds)
                return;

            int currentQuality = QualitySettings.GetQualityLevel();
            int minQuality = Mathf.Clamp(_minQualityIndex, 0, QualitySettings.names.Length - 1);
            int maxQuality = Mathf.Clamp(_maxQualityIndexForWebGL, minQuality, QualitySettings.names.Length - 1);

            float lowerThreshold = Mathf.Max(5f, _lowerQualityFpsThreshold - _hysteresisFps);
            float upperThreshold = Mathf.Max(lowerThreshold + 1f, _increaseQualityFpsThreshold + _hysteresisFps);

            if (fps < lowerThreshold && currentQuality > minQuality)
            {
                QualitySettings.SetQualityLevel(currentQuality - 1, true);
                _lastQualityChangeTime = Time.unscaledTime;
                return;
            }

            if (fps > upperThreshold && currentQuality < maxQuality)
            {
                QualitySettings.SetQualityLevel(currentQuality + 1, true);
                _lastQualityChangeTime = Time.unscaledTime;
            }
        }
    }
}
