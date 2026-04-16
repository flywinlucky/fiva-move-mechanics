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

    private Vector3 baseScale;
    private float timer;
    private bool isPlaying;

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
            Play();
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
        isPlaying = true;
    }

    public void Stop(bool resetToBaseScale = true)
    {
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
}
