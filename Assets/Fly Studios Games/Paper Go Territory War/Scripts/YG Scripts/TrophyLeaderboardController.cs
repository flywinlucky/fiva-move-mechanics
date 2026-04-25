using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using YG;

[DisallowMultipleComponent]
public class TrophyLeaderboardController : MonoBehaviour
{
    [Header("Leaderboard")]
    [SerializeField] private string leaderboardName = string.Empty;
    [SerializeField] private LeaderboardYG leaderboardView;
    [SerializeField] private bool autoFindLeaderboardView = true;
    [SerializeField] private bool refreshLeaderboardOnEnable = true;
    [SerializeField] private bool refreshLeaderboardAfterSubmit = true;

    [Header("Data")]
    [SerializeField] private UserData userData;

    [Header("Request Timing")]
    [SerializeField] [Min(1f)] private float minSecondsBetweenSubmits = 1.1f;
    [SerializeField] [Min(0f)] private float refreshDelaySeconds = 0.75f;

    private Coroutine submitCoroutine;
    private Coroutine refreshCoroutine;
    private int lastSubmittedScore = int.MinValue;
    private int pendingScore;
    private bool hasPendingScore;
    private bool warnedMissingLeaderboardName;

    private void Awake()
    {
        ResolveUserData();
        ResolveLeaderboardView();
        ApplyLeaderboardNameToView();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        YG2.onGetSDKData += HandleYGDataReady;

        BindUserData();
        ResolveLeaderboardView();
        ApplyLeaderboardNameToView();

        if (YG2.isSDKEnabled)
            HandleYGDataReady();

        if (refreshLeaderboardOnEnable)
            RequestLeaderboardRefresh(0.1f);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        YG2.onGetSDKData -= HandleYGDataReady;
        UnbindUserData();

        if (submitCoroutine != null)
        {
            StopCoroutine(submitCoroutine);
            submitCoroutine = null;
        }

        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
            refreshCoroutine = null;
        }
    }

    private void OnValidate()
    {
        minSecondsBetweenSubmits = Mathf.Max(1f, minSecondsBetweenSubmits);
        refreshDelaySeconds = Mathf.Max(0f, refreshDelaySeconds);
    }

    public void SubmitCurrentTrophies()
    {
        ResolveUserData();

        if (userData == null)
            return;

        QueueScoreUpload(GetCurrentTrophies());
    }

    public void RefreshLeaderboardView()
    {
        RequestLeaderboardRefresh(0f);
    }

    private void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        ResolveLeaderboardView();
        ApplyLeaderboardNameToView();

        if (refreshLeaderboardOnEnable)
            RequestLeaderboardRefresh(0.15f);
    }

    private void HandleYGDataReady()
    {
        ResolveUserData();
        ResolveLeaderboardView();
        ApplyLeaderboardNameToView();

        if (userData != null)
            QueueScoreUpload(GetCurrentTrophies());

        if (refreshLeaderboardOnEnable)
            RequestLeaderboardRefresh(0.15f);
    }

    private void HandleUserDataChanged(UserProfileData _)
    {
        if (userData == null)
            return;

        QueueScoreUpload(GetCurrentTrophies());
    }

    private void BindUserData()
    {
        ResolveUserData();

        if (userData == null)
            return;

        userData.OnUserDataChanged -= HandleUserDataChanged;
        userData.OnUserDataChanged += HandleUserDataChanged;
    }

    private void UnbindUserData()
    {
        if (userData == null)
            return;

        userData.OnUserDataChanged -= HandleUserDataChanged;
    }

    private void ResolveUserData()
    {
        UserData resolvedUserData = UserData.Instance;
        if (resolvedUserData == null)
            resolvedUserData = FindObjectOfType<UserData>();

        if (userData == resolvedUserData)
            return;

        if (userData != null)
            userData.OnUserDataChanged -= HandleUserDataChanged;

        userData = resolvedUserData;
    }

    private int GetCurrentTrophies()
    {
        if (userData == null || userData.Data == null)
            return 0;

        return Mathf.Max(0, userData.Data.trophies);
    }

    private void ResolveLeaderboardView()
    {
        if (leaderboardView != null)
            return;

        if (!autoFindLeaderboardView)
            return;

        LeaderboardYG[] leaderboards = FindObjectsOfType<LeaderboardYG>(true);
        if (leaderboards == null || leaderboards.Length == 0)
            return;

        string trimmedName = GetConfiguredLeaderboardName();

        foreach (LeaderboardYG candidate in leaderboards)
        {
            if (candidate == null)
                continue;

            if (!string.IsNullOrWhiteSpace(trimmedName) && candidate.nameLB == trimmedName)
            {
                leaderboardView = candidate;
                return;
            }
        }

        leaderboardView = leaderboards[0];
    }

    private string GetConfiguredLeaderboardName()
    {
        if (!string.IsNullOrWhiteSpace(leaderboardName))
            return leaderboardName.Trim();

        if (leaderboardView != null && !string.IsNullOrWhiteSpace(leaderboardView.nameLB))
            return leaderboardView.nameLB.Trim();

        return string.Empty;
    }

    private string GetEffectiveLeaderboardName()
    {
        return GetConfiguredLeaderboardName();
    }

    private void ApplyLeaderboardNameToView()
    {
        if (leaderboardView == null)
            return;

        string effectiveName = GetEffectiveLeaderboardName();
        if (string.IsNullOrWhiteSpace(effectiveName))
            return;

        leaderboardView.nameLB = effectiveName;
    }

    private void QueueScoreUpload(int score)
    {
        int sanitizedScore = Mathf.Max(0, score);

        if (sanitizedScore == lastSubmittedScore && !hasPendingScore)
            return;

        pendingScore = sanitizedScore;
        hasPendingScore = true;

        if (submitCoroutine == null && isActiveAndEnabled)
            submitCoroutine = StartCoroutine(SubmitScoreRoutine());
    }

    private IEnumerator SubmitScoreRoutine()
    {
        while (hasPendingScore)
        {
            if (!CanSubmitScore())
            {
                yield return null;
                continue;
            }

            int scoreToSubmit = pendingScore;
            hasPendingScore = false;

            string targetLeaderboardName = GetEffectiveLeaderboardName();
            if (string.IsNullOrWhiteSpace(targetLeaderboardName))
            {
                yield return null;
                continue;
            }

            YG2.SetLeaderboard(targetLeaderboardName, scoreToSubmit);
            lastSubmittedScore = scoreToSubmit;

            if (refreshLeaderboardAfterSubmit)
                RequestLeaderboardRefresh(refreshDelaySeconds);

            yield return new WaitForSecondsRealtime(minSecondsBetweenSubmits);
        }

        submitCoroutine = null;
    }

    private bool CanSubmitScore()
    {
        if (!isActiveAndEnabled)
            return false;

        string targetLeaderboardName = GetEffectiveLeaderboardName();
        if (string.IsNullOrWhiteSpace(targetLeaderboardName))
        {
            if (!warnedMissingLeaderboardName)
            {
                warnedMissingLeaderboardName = true;
                Debug.LogWarning("[TrophyLeaderboardController] Missing leaderboard name. Assign leaderboardName or LeaderboardYG.nameLB.", this);
            }

            return false;
        }

        warnedMissingLeaderboardName = false;

        if (!YG2.isSDKEnabled)
            return false;

        if (!YG2.infoYG.Leaderboards.enable)
            return false;

#if Authorization_yg
        if (!YG2.player.auth)
            return false;
#endif

        return userData != null;
    }

    private void RequestLeaderboardRefresh(float delaySeconds)
    {
        ResolveLeaderboardView();
        ApplyLeaderboardNameToView();

        if (leaderboardView == null || !isActiveAndEnabled)
            return;

        if (refreshCoroutine != null)
            StopCoroutine(refreshCoroutine);

        refreshCoroutine = StartCoroutine(RefreshLeaderboardRoutine(Mathf.Max(0f, delaySeconds)));
    }

    private IEnumerator RefreshLeaderboardRoutine(float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        ResolveLeaderboardView();
        ApplyLeaderboardNameToView();

        if (leaderboardView != null)
            leaderboardView.UpdateLB();

        refreshCoroutine = null;
    }
}