using System.Collections;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class RoundTeamsResuldInWord : MonoBehaviour
{
    const string VsLabel = "VS";
    const string GoLabel = "GO!";

    public static RoundTeamsResuldInWord Instance { get; private set; }

    [Header("References")]
    public TMP_Text blue_Name;
    public TMP_Text blue_Score;
    public CanvasGroup bluePanelHost;

    [Space]
    public TMP_Text red_Name;
    public TMP_Text red_Score;
    public CanvasGroup redPanelHost;

    [Space]
    public TMP_Text roundCountdownText;

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

    [SerializeField]
    [Range(1, 10)]
    int kickOffCountdownStartFrom = 3;

    [SerializeField]
    [Min(0.01f)]
    float kickOffCountdownStepSeconds = 0.45f;

    [SerializeField]
    [Min(0.01f)]
    float kickOffGoVisibleSeconds = 0.45f;

    Coroutine _playingRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

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
            float countdown = Mathf.Max(1, kickOffCountdownStartFrom) * Mathf.Max(0.01f, kickOffCountdownStepSeconds);
            float go = Mathf.Max(0.01f, kickOffGoVisibleSeconds);
            return (fade * 2f) + visible + countdown + go;
        }
    }

    public void SetKickOffPanelTiming(float visibleSeconds, float fadeSeconds)
    {
        kickOffPanelVisibleSeconds = Mathf.Max(0f, visibleSeconds);
        kickOffPanelFadeSeconds = Mathf.Max(0.01f, fadeSeconds);
    }

    public IEnumerator PlayIntroSequence()
    {
        RefreshInfo();
        SetVisibleImmediate(false);

        float preDelay = Mathf.Max(0f, delayBeforeShowSeconds);
        if (preDelay > 0f)
            yield return new WaitForSeconds(preDelay);

        ShowRoundText(VsLabel);
        yield return FadePanels(0f, 1f, Mathf.Max(0.01f, fadeDurationSeconds));

        float showTime = Mathf.Max(0f, visibleSeconds);
        if (showTime > 0f)
            yield return new WaitForSeconds(showTime);

        yield return FadePanels(1f, 0f, Mathf.Max(0.01f, fadeDurationSeconds));
        HideRoundText(resetToVs: true);
        SetVisibleImmediate(false);

        float postDelay = Mathf.Max(0f, delayAfterHideSeconds);
        if (postDelay > 0f)
            yield return new WaitForSeconds(postDelay);

        _playingRoutine = null;
    }

    IEnumerator PlayKickOffPanelSequence()
    {
        RefreshInfo();
        SetVisibleImmediate(false);

        ShowRoundText(VsLabel);
        yield return FadePanels(0f, 1f, Mathf.Max(0.01f, kickOffPanelFadeSeconds));

        float showTime = Mathf.Max(0f, kickOffPanelVisibleSeconds);
        if (showTime > 0f)
            yield return new WaitForSeconds(showTime);

        // Hide the blue/red panel hosts first, then play center countdown.
        yield return FadePanels(1f, 0f, Mathf.Max(0.01f, kickOffPanelFadeSeconds));
        yield return PlayCountdownSequence();

        HideRoundText(resetToVs: true);
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

    IEnumerator FadePanels(float from, float to, float duration)
    {
        if (bluePanelHost == null && redPanelHost == null)
            yield break;

        PreparePanelHostForFade(bluePanelHost, from);
        PreparePanelHostForFade(redPanelHost, from);

        if (duration <= 0.01f)
        {
            SetPanelAlpha(bluePanelHost, to);
            SetPanelAlpha(redPanelHost, to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(from, to, t);
            SetPanelAlpha(bluePanelHost, alpha);
            SetPanelAlpha(redPanelHost, alpha);
            yield return null;
        }

        SetPanelAlpha(bluePanelHost, to);
        SetPanelAlpha(redPanelHost, to);
    }

    void SetVisibleImmediate(bool visible)
    {
        SetPanelVisibleImmediate(bluePanelHost, visible);
        SetPanelVisibleImmediate(redPanelHost, visible);

        if (visible)
            ShowRoundText(VsLabel);
        else
            HideRoundText(resetToVs: true);
    }

    void EnsurePanelHostActive()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (bluePanelHost != null && !bluePanelHost.gameObject.activeSelf)
            bluePanelHost.gameObject.SetActive(true);

        if (redPanelHost != null && !redPanelHost.gameObject.activeSelf)
            redPanelHost.gameObject.SetActive(true);

        if (roundCountdownText != null && !roundCountdownText.gameObject.activeSelf)
            roundCountdownText.gameObject.SetActive(true);
    }

    IEnumerator PlayCountdownSequence()
    {
        if (roundCountdownText == null)
            yield break;

        int startFrom = Mathf.Max(1, kickOffCountdownStartFrom);
        float stepDuration = Mathf.Max(0.01f, kickOffCountdownStepSeconds);

        for (int i = startFrom; i >= 1; i--)
        {
            ShowRoundText(i.ToString());
            yield return new WaitForSeconds(stepDuration);
        }

        ShowRoundText(GoLabel);
        yield return new WaitForSeconds(Mathf.Max(0.01f, kickOffGoVisibleSeconds));
    }

    void ShowRoundText(string text)
    {
        if (roundCountdownText == null)
            return;

        roundCountdownText.text = text;

        if (!roundCountdownText.gameObject.activeSelf)
            roundCountdownText.gameObject.SetActive(true);

        Color color = roundCountdownText.color;
        color.a = 1f;
        roundCountdownText.color = color;
    }

    void HideRoundText(bool resetToVs)
    {
        if (roundCountdownText == null)
            return;

        if (resetToVs)
            roundCountdownText.text = VsLabel;

        Color color = roundCountdownText.color;
        color.a = 0f;
        roundCountdownText.color = color;

        if (roundCountdownText.gameObject.activeSelf)
            roundCountdownText.gameObject.SetActive(false);
    }

    void PreparePanelHostForFade(CanvasGroup panelHost, float alpha)
    {
        if (panelHost == null)
            return;

        if (!panelHost.gameObject.activeSelf)
            panelHost.gameObject.SetActive(true);

        panelHost.blocksRaycasts = false;
        panelHost.interactable = false;
        panelHost.alpha = Mathf.Clamp01(alpha);
    }

    void SetPanelVisibleImmediate(CanvasGroup panelHost, bool visible)
    {
        if (panelHost == null)
            return;

        if (!panelHost.gameObject.activeSelf)
            panelHost.gameObject.SetActive(true);

        panelHost.blocksRaycasts = false;
        panelHost.interactable = false;
        panelHost.alpha = visible ? 1f : 0f;
    }

    void SetPanelAlpha(CanvasGroup panelHost, float alpha)
    {
        if (panelHost == null)
            return;

        panelHost.alpha = Mathf.Clamp01(alpha);
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
