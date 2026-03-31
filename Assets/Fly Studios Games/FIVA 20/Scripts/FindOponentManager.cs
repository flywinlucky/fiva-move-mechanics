using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

public class FindOponentManager : MonoBehaviour
{
    [Header("UI")]
    [FormerlySerializedAs("findOponentPanel")]
    [SerializeField]
    GameObject findOpponentPanel;

    [FormerlySerializedAs("searchOponent_Text")]
    [SerializeField]
    TMP_Text searchOpponentText;

    [FormerlySerializedAs("tips_Text")]
    [SerializeField]
    TMP_Text tipsText;

    [Header("Fake Matchmaking")]
    [SerializeField]
    [Min(0.1f)]
    float minSearchDurationSeconds = 2f;

    [SerializeField]
    [Min(0.1f)]
    float maxSearchDurationSeconds = 3f;

    [SerializeField]
    string gameSceneName = "Game";

    [SerializeField]
    string searchingMessage = "Searching opponent...";

    [SerializeField]
    string foundMessage = "Opponent found!";

    [Header("Tips")]
    [SerializeField]
    string[] searchTips;

    Coroutine _searchRoutine;
    int _lastTipIndex = -1;

    public bool IsSearching { get; private set; }

    void Awake()
    {
        if (findOpponentPanel != null)
            findOpponentPanel.SetActive(false);

        UpdateSearchMessage(string.Empty);
        UpdateTipMessage(string.Empty);
    }

    public void BeginFindOpponent()
    {
        if (IsSearching)
            return;

        if (findOpponentPanel != null)
            findOpponentPanel.SetActive(true);

        IsSearching = true;
        UpdateSearchMessage(searchingMessage);
        ShowRandomTip();

        if (_searchRoutine != null)
            StopCoroutine(_searchRoutine);
        _searchRoutine = StartCoroutine(FakeFindRoutine());
    }

    public void CancelFindOpponent()
    {
        StopCurrentSearch();

        if (findOpponentPanel != null)
            findOpponentPanel.SetActive(false);

        UpdateSearchMessage(string.Empty);
        UpdateTipMessage(string.Empty);
    }

    IEnumerator FakeFindRoutine()
    {
        float maxDuration = Mathf.Max(minSearchDurationSeconds, maxSearchDurationSeconds);
        float waitDuration = Random.Range(Mathf.Max(0.1f, minSearchDurationSeconds), maxDuration);
        yield return new WaitForSecondsRealtime(waitDuration);

        _searchRoutine = null;
        IsSearching = false;

        UpdateSearchMessage(foundMessage);

        // Auto continue to match scene once fake matchmaking has completed.
        LoadGameScene();
    }

    void ShowRandomTip()
    {
        if (tipsText == null)
            return;

        if (searchTips == null || searchTips.Length == 0)
        {
            UpdateTipMessage(string.Empty);
            return;
        }

        int tipIndex = Random.Range(0, searchTips.Length);
        if (searchTips.Length > 1 && tipIndex == _lastTipIndex)
            tipIndex = (tipIndex + Random.Range(1, searchTips.Length)) % searchTips.Length;

        _lastTipIndex = tipIndex;
        UpdateTipMessage(searchTips[tipIndex]);
    }

    void StopCurrentSearch()
    {
        IsSearching = false;

        if (_searchRoutine != null)
        {
            StopCoroutine(_searchRoutine);
            _searchRoutine = null;
        }
    }

    void UpdateSearchMessage(string message)
    {
        if (searchOpponentText != null)
            searchOpponentText.text = message;
    }

    void UpdateTipMessage(string message)
    {
        if (tipsText != null)
            tipsText.text = message;
    }

    public void LoadGameScene()
    {
        if (string.IsNullOrWhiteSpace(gameSceneName))
            return;

        SceneManager.LoadScene(gameSceneName);
    }

    void OnDisable()
    {
        StopCurrentSearch();
    }

    void OnValidate()
    {
        minSearchDurationSeconds = Mathf.Max(0.1f, minSearchDurationSeconds);
        maxSearchDurationSeconds = Mathf.Max(minSearchDurationSeconds, maxSearchDurationSeconds);
    }
}
