using System.Collections;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class RoundTeamsResuldInWord : MonoBehaviour
{
    public static RoundTeamsResuldInWord Instance { get; private set; }

    [Header("References")]
    public CanvasGroup canvasGroup;
    public TMP_Text blue_Name;
    public TMP_Text blue_Score;

    [Space]
    public TMP_Text red_Name;
    public TMP_Text red_Score;

    [Header("Intro Timing")]
    [SerializeField]
    [Min(0f)]
    float delayBeforeShowSeconds = 2f;

    [SerializeField]
    [Min(0f)]
    float visibleSeconds = 2f;

    [SerializeField]
    [Min(0f)]
    float delayAfterHideSeconds = 2f;

    [SerializeField]
    [Min(0.01f)]
    float fadeDurationSeconds = 0.3f;

    [Header("KickOff Panel")]
    [SerializeField]
    [Min(0f)]
    float kickOffPanelVisibleSeconds = 2f;

    [SerializeField]
    [Min(0.01f)]
    float kickOffPanelFadeSeconds = 0.2f;

    Coroutine _playingRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        SetVisibleImmediate(false);
        RefreshInfo();
    }

    public void PlayIntro()
    {
        EnsurePanelHostActive();
        if (!isActiveAndEnabled)
            return;

        if (_playingRoutine != null)
            StopCoroutine(_playingRoutine);

        _playingRoutine = StartCoroutine(PlayIntroSequence());
    }

    public void PlayKickOffPanel()
    {
        EnsurePanelHostActive();
        if (!isActiveAndEnabled)
            return;

        if (_playingRoutine != null)
            StopCoroutine(_playingRoutine);

        _playingRoutine = StartCoroutine(PlayKickOffPanelSequence());
    }

    public float KickOffPanelTotalDuration
    {
        get
        {
            float fade = Mathf.Max(0.01f, kickOffPanelFadeSeconds);
            float visible = Mathf.Max(0f, kickOffPanelVisibleSeconds);
            return (fade * 2f) + visible;
        }
    }

    public IEnumerator PlayIntroSequence()
    {
        RefreshInfo();
        SetVisibleImmediate(false);

        float preDelay = Mathf.Max(0f, delayBeforeShowSeconds);
        if (preDelay > 0f)
            yield return new WaitForSeconds(preDelay);

        yield return FadeCanvas(0f, 1f, Mathf.Max(0.01f, fadeDurationSeconds));

        float showTime = Mathf.Max(0f, visibleSeconds);
        if (showTime > 0f)
            yield return new WaitForSeconds(showTime);

        yield return FadeCanvas(1f, 0f, Mathf.Max(0.01f, fadeDurationSeconds));
        SetVisibleImmediate(false);

        float postDelay = Mathf.Max(0f, delayAfterHideSeconds);
        if (postDelay > 0f)
            yield return new WaitForSeconds(postDelay);

        _playingRoutine = null;
    }

    IEnumerator PlayKickOffPanelSequence()
    {
        RefreshInfo();

        yield return FadeCanvas(0f, 1f, Mathf.Max(0.01f, kickOffPanelFadeSeconds));

        float showTime = Mathf.Max(0f, kickOffPanelVisibleSeconds);
        if (showTime > 0f)
            yield return new WaitForSeconds(showTime);

        yield return FadeCanvas(1f, 0f, Mathf.Max(0.01f, kickOffPanelFadeSeconds));
        SetVisibleImmediate(false);

        _playingRoutine = null;
    }

    public void RefreshInfo()
    {
        string blueName = "Player";
        string redName = "Opponent";
        int blueScore = 0;
        int redScore = 0;

        if (UserData.Instance != null && UserData.Instance.Data != null &&
            !string.IsNullOrWhiteSpace(UserData.Instance.Data.playerName))
        {
            blueName = UserData.Instance.Data.playerName;
        }

        if (AiData.Instance != null && !string.IsNullOrWhiteSpace(AiData.Instance.AiName))
            redName = AiData.Instance.AiName;

        if (MatchManager.Instance != null)
        {
            if (MatchManager.Instance.TeamAway != null)
                blueScore = Mathf.Max(0, MatchManager.Instance.TeamAway.Goals);

            if (MatchManager.Instance.TeamHome != null)
                redScore = Mathf.Max(0, MatchManager.Instance.TeamHome.Goals);
        }

        if (blue_Name != null)
            blue_Name.text = blueName;

        if (red_Name != null)
            red_Name.text = redName;

        if (blue_Score != null)
            blue_Score.text = blueScore.ToString();

        if (red_Score != null)
            red_Score.text = redScore.ToString();
    }

    IEnumerator FadeCanvas(float from, float to, float duration)
    {
        if (canvasGroup == null)
            yield break;

        canvasGroup.gameObject.SetActive(true);
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = Mathf.Clamp01(from);

        if (duration <= 0.01f)
        {
            canvasGroup.alpha = Mathf.Clamp01(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = Mathf.Clamp01(to);
    }

    void SetVisibleImmediate(bool visible)
    {
        if (canvasGroup == null)
            return;

        if (!canvasGroup.gameObject.activeSelf)
            canvasGroup.gameObject.SetActive(true);

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    void EnsurePanelHostActive()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (canvasGroup != null && !canvasGroup.gameObject.activeSelf)
            canvasGroup.gameObject.SetActive(true);
    }

    void OnDisable()
    {
        if (_playingRoutine != null)
            StopCoroutine(_playingRoutine);

        _playingRoutine = null;
        SetVisibleImmediate(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
