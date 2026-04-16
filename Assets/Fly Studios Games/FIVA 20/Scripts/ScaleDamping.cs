using UnityEngine;

public class ScaleDamping : MonoBehaviour
{
    [Header("Scale Pulse")]
    [SerializeField]
    [Min(0f)]
    private float addScale = 0.3f;

    [SerializeField]
    [Min(0.01f)]
    private float cycleDuration = 0.8f;

    [SerializeField]
    private bool useUnscaledTime = true;

    [SerializeField]
    private bool playOnEnable = true;

    [SerializeField]
    private bool randomStartPoint = false;

    [SerializeField]
    private AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Intro Scale In")]
    [SerializeField]
    private bool useIntroScaleIn = true;

    [SerializeField]
    [Min(0.01f)]
    private float introDuration = 0.25f;

    [SerializeField]
    [Min(1f)]
    private float introBounceForce = 1.15f;

    [Header("Transition Smoothing")]
    [SerializeField]
    [Min(0f)]
    private float introToPulseBlendDuration = 0.08f;

    [SerializeField]
    [Min(0f)]
    private float pulseSmoothingTime = 0.03f;

    private Vector3 baseScale;
    private float timer;
    private bool isPlaying;
    private Coroutine introRoutine;
    private float introToPulseBlendRemaining;
    private Vector3 introToPulseBlendStartScale;
    private Vector3 pulseScaleVelocity;

    private void Awake()
    {
        baseScale = transform.localScale;

        if (randomStartPoint)
        {
            timer = Random.value * cycleDuration;
        }
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            StartPulseWithIntro();
        }
    }

    private void OnDisable()
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        introToPulseBlendRemaining = 0f;
        pulseScaleVelocity = Vector3.zero;
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        timer += dt;

        float normalized = Mathf.PingPong(timer / Mathf.Max(0.01f, cycleDuration), 1f);
        float curved = pulseCurve != null ? pulseCurve.Evaluate(normalized) : normalized;

        Vector3 targetScale = baseScale + (Vector3.one * addScale);
        Vector3 pulseScale = Vector3.LerpUnclamped(baseScale, targetScale, curved);

        if (introToPulseBlendRemaining > 0f)
        {
            float blendDuration = Mathf.Max(0.0001f, introToPulseBlendDuration);
            introToPulseBlendRemaining = Mathf.Max(0f, introToPulseBlendRemaining - dt);

            float blendT = 1f - (introToPulseBlendRemaining / blendDuration);
            blendT = blendT * blendT * (3f - (2f * blendT));

            transform.localScale = Vector3.LerpUnclamped(introToPulseBlendStartScale, pulseScale, blendT);
            return;
        }

        if (pulseSmoothingTime > 0f)
        {
            transform.localScale = Vector3.SmoothDamp(
                transform.localScale,
                pulseScale,
                ref pulseScaleVelocity,
                pulseSmoothingTime,
                Mathf.Infinity,
                dt);
        }
        else
        {
            transform.localScale = pulseScale;
        }
    }

    public void Play()
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        introToPulseBlendRemaining = 0f;
        pulseScaleVelocity = Vector3.zero;

        isPlaying = true;
    }

    public void Stop(bool resetToBaseScale = true)
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        introToPulseBlendRemaining = 0f;
        pulseScaleVelocity = Vector3.zero;

        isPlaying = false;

        if (resetToBaseScale)
        {
            transform.localScale = baseScale;
        }
    }

    public void RefreshBaseScale()
    {
        baseScale = transform.localScale;
    }

    void StartPulseWithIntro()
    {
        if (introRoutine != null)
            StopCoroutine(introRoutine);

        introToPulseBlendRemaining = 0f;
        pulseScaleVelocity = Vector3.zero;

        if (!useIntroScaleIn)
        {
            Play();
            return;
        }

        // Ensure pulse starts from a deterministic phase after intro.
        timer = 0f;

        introRoutine = StartCoroutine(PlayIntroScaleInThenPulse());
    }

    System.Collections.IEnumerator PlayIntroScaleInThenPulse()
    {
        isPlaying = false;

        float duration = Mathf.Max(0.01f, introDuration);
        float elapsed = 0f;

        transform.localScale = Vector3.zero;

        while (elapsed < duration)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += dt;

            float percent = Mathf.Clamp01(elapsed / duration);
            float bounce = Mathf.Sin(percent * Mathf.PI);
            float scaleAmount = percent + (bounce * (Mathf.Max(1f, introBounceForce) - 1f));

            transform.localScale = baseScale * scaleAmount;
            yield return null;
        }

        transform.localScale = baseScale;
        introRoutine = null;
        BeginPulseAfterIntro();
    }

    void BeginPulseAfterIntro()
    {
        isPlaying = true;
        pulseScaleVelocity = Vector3.zero;

        introToPulseBlendStartScale = transform.localScale;
        introToPulseBlendRemaining = Mathf.Max(0f, introToPulseBlendDuration);
    }
}
