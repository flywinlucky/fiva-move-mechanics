using UnityEngine;

[DisallowMultipleComponent]
public class TimeControlDebugButtons : MonoBehaviour
{
    [Header("Debug Time Values")]
    [SerializeField]
    [Min(0f)]
    float fast5Value = 5f;

    [SerializeField]
    [Min(0f)]
    float fast10Value = 10f;

    [SerializeField]
    [Min(0f)]
    float resetValue = 1f;

    [SerializeField]
    [Min(0f)]
    float stopValue = 0f;

    public void SetFast5Time()
    {
        ApplyTimeScale(fast5Value);
    }

    public void SetFast10Time()
    {
        ApplyTimeScale(fast10Value);
    }

    public void AddFast5Time()
    {
        ApplyTimeScale(Time.timeScale + fast5Value);
    }

    public void AddFast10Time()
    {
        ApplyTimeScale(Time.timeScale + fast10Value);
    }

    public void StopTime()
    {
        ApplyTimeScale(stopValue);
    }

    public void ResetTime()
    {
        ApplyTimeScale(resetValue);
    }

    public void SetCustomTime(float value)
    {
        ApplyTimeScale(value);
    }

    void ApplyTimeScale(float value)
    {
        Time.timeScale = Mathf.Max(0f, value);
    }

    void OnDisable()
    {
        // Avoid leaving gameplay slowed/paused when object is disabled in play mode.
        if (Application.isPlaying)
            Time.timeScale = Mathf.Max(0f, resetValue);
    }
}