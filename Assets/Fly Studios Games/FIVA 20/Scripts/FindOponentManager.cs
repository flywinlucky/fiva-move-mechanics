using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
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

    [Header("Generated Opponent")]
    [SerializeField]
    TextAsset namesCsvFile;

    [SerializeField]
    bool useCsvNicknames = true;

    [SerializeField]
    [Range(0.05f, 0.40f)]
    float minTrophyVariancePercent = 0.10f;

    [SerializeField]
    [Range(0.05f, 0.50f)]
    float maxTrophyVariancePercent = 0.20f;

    [SerializeField]
    [Min(0)]
    int minTrophiesIfNoUserData = 20;

    [SerializeField]
    [Min(1)]
    int maxTrophiesIfNoUserData = 180;

    Coroutine _searchRoutine;
    int _lastTipIndex = -1;
    readonly List<string> _csvNicknames = new List<string>();

    public bool IsSearching { get; private set; }

    void Awake()
    {
        BuildCsvNicknameCache();

        if (findOpponentPanel != null)
            findOpponentPanel.SetActive(false);

        UpdateSearchMessage(string.Empty);
    }

    public void BeginFindOpponent()
    {
        if (IsSearching)
            return;

        if (findOpponentPanel != null)
            findOpponentPanel.SetActive(true);

        IsSearching = true;
        UpdateSearchMessage(searchingMessage);

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
    }

    IEnumerator FakeFindRoutine()
    {
        float maxDuration = Mathf.Max(minSearchDurationSeconds, maxSearchDurationSeconds);
        float waitDuration = Random.Range(Mathf.Max(0.1f, minSearchDurationSeconds), maxDuration);
        yield return new WaitForSecondsRealtime(waitDuration);

        _searchRoutine = null;
        IsSearching = false;

        UpdateSearchMessage(foundMessage);

        GenerateAndStoreOpponentData();

        // Auto continue to match scene once fake matchmaking has completed.
        LoadGameScene();
    }

    void GenerateAndStoreOpponentData()
    {
        string aiName = GenerateOpponentName();
        int aiTrophies = GenerateOpponentTrophies();

        AiData bridge = AiData.GetOrCreate();
        bridge.SetAiData(aiName, aiTrophies);
    }

    string GenerateOpponentName()
    {
        if (useCsvNicknames && _csvNicknames.Count > 0)
            return _csvNicknames[Random.Range(0, _csvNicknames.Count)];

        return "Opponent";
    }

    void BuildCsvNicknameCache()
    {
        _csvNicknames.Clear();

        if (namesCsvFile == null || string.IsNullOrWhiteSpace(namesCsvFile.text))
            return;

        string[] lines = namesCsvFile.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        HashSet<string> unique = new HashSet<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Skip header row from names.csv.
            if (i == 0 && line.StartsWith("name1,"))
                continue;

            string[] cols = line.Split(',');
            if (cols.Length < 3)
                continue;

            string relationship = cols[1].Trim();
            string nickname = cols[2].Trim().Trim('"');
            if (!string.Equals(relationship, "has_nickname", System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.IsNullOrWhiteSpace(nickname))
                continue;

            if (unique.Add(nickname))
                _csvNicknames.Add(nickname);
        }
    }

    int GenerateOpponentTrophies()
    {
        int userTrophies = 0;
        if (UserData.Instance != null && UserData.Instance.Data != null)
            userTrophies = Mathf.Max(0, UserData.Instance.Data.trophies);

        if (userTrophies <= 0)
            return Random.Range(Mathf.Max(0, minTrophiesIfNoUserData), Mathf.Max(1, maxTrophiesIfNoUserData + 1));

        float minPercent = Mathf.Clamp(minTrophyVariancePercent, 0.01f, 0.75f);
        float maxPercent = Mathf.Max(minPercent, Mathf.Clamp(maxTrophyVariancePercent, 0.01f, 0.95f));
        float variancePercent = Random.Range(minPercent, maxPercent);

        int offset = Mathf.Max(1, Mathf.RoundToInt(userTrophies * variancePercent));
        bool add = Random.value >= 0.5f;
        int result = add ? userTrophies + offset : userTrophies - offset;
        return Mathf.Max(0, result);
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
        BuildCsvNicknameCache();
        minSearchDurationSeconds = Mathf.Max(0.1f, minSearchDurationSeconds);
        maxSearchDurationSeconds = Mathf.Max(minSearchDurationSeconds, maxSearchDurationSeconds);
        minTrophiesIfNoUserData = Mathf.Max(0, minTrophiesIfNoUserData);
        maxTrophiesIfNoUserData = Mathf.Max(minTrophiesIfNoUserData + 1, maxTrophiesIfNoUserData);
        minTrophyVariancePercent = Mathf.Clamp(minTrophyVariancePercent, 0.01f, 0.75f);
        maxTrophyVariancePercent = Mathf.Clamp(maxTrophyVariancePercent, minTrophyVariancePercent, 0.95f);
    }
}
