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

    private Vector3 baseScale;
    private float timer;
    private bool isPlaying;
    private Coroutine introRoutine;

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
        transform.localScale = Vector3.LerpUnclamped(baseScale, targetScale, curved);
    }

    public void Play()
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        isPlaying = true;
    }

    public void Stop(bool resetToBaseScale = true)
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

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

        if (!useIntroScaleIn)
        {
            Play();
            return;
        }

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
        isPlaying = true;
    }
}
