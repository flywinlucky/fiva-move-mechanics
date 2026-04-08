using System;
using UnityEngine;

[Serializable]
public class UserProfileData
{
    public string playerName = "Player";
    public int trophies;
    public int matchesPlayed;
    public int matchesWon;
    public int goalsScored;
}

[DisallowMultipleComponent]
public class UserData : MonoBehaviour
{
    const string SaveKey = "FIVA_USER_PROFILE_DATA";

    public static UserData Instance { get; private set; }

    [Header("Defaults")]
    [SerializeField]
    string defaultPlayerName = "Player";

    [SerializeField]
    bool dontDestroyOnLoad = true;

    [SerializeField]
    bool autoSaveOnChange = true;

    [Header("Debug")]
    [SerializeField]
    bool enableDebugTrophyHotkeys = true;

    [SerializeField]
    [Min(1)]
    int debugTrophyStep = 23;

    [SerializeField]
    UserProfileData _data = new UserProfileData();

    public UserProfileData Data => _data;

    public event Action<UserProfileData> OnUserDataChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        Load();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            Save();
    }

    void Update()
    {
        if (!enableDebugTrophyHotkeys)
            return;

        int step = Mathf.Max(1, debugTrophyStep);

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            AddTrophies(step);
            Debug.Log($"[UserData] Debug +{step} trophies. Total: {_data.trophies}");
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            int previous = _data.trophies;
            SpendTrophies(step);
            int delta = previous - _data.trophies;
            Debug.Log($"[UserData] Debug -{delta} trophies. Total: {_data.trophies}");
        }
    }

    void OnApplicationQuit()
    {
        Save();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetPlayerName(string newName)
    {
        string sanitized = string.IsNullOrWhiteSpace(newName)
            ? defaultPlayerName
            : newName.Trim();

        if (_data.playerName == sanitized)
            return;

        _data.playerName = sanitized;
        NotifyDataChanged();
    }

    public void AddTrophies(int amount)
    {
        if (amount <= 0)
            return;

        _data.trophies += amount;
        NotifyDataChanged();
    }

    public bool SpendTrophies(int amount)
    {
        if (amount <= 0)
            return true;

        if (_data.trophies < amount)
            return false;

        _data.trophies -= amount;
        NotifyDataChanged();
        return true;
    }

    public void RegisterMatchPlayed(bool won, int goals)
    {
        _data.matchesPlayed++;
        if (won)
            _data.matchesWon++;

        _data.goalsScored += Mathf.Max(0, goals);
        NotifyDataChanged();
    }

    public void Save()
    {
        EnsureDataIntegrity();

        string json = JsonUtility.ToJson(_data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            _data = CreateDefaultData();
            NotifyDataChanged(saveAfterNotify: false);
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            _data = CreateDefaultData();
            NotifyDataChanged(saveAfterNotify: false);
            return;
        }

        UserProfileData loaded = JsonUtility.FromJson<UserProfileData>(json);
        _data = loaded ?? CreateDefaultData();
        EnsureDataIntegrity();
        NotifyDataChanged(saveAfterNotify: false);
    }

    public void ResetData(bool saveImmediately = true)
    {
        _data = CreateDefaultData();
        NotifyDataChanged(saveAfterNotify: saveImmediately);
    }

    UserProfileData CreateDefaultData()
    {
        return new UserProfileData
        {
            playerName = string.IsNullOrWhiteSpace(defaultPlayerName) ? "Player" : defaultPlayerName.Trim(),
            trophies = 0,
            matchesPlayed = 0,
            matchesWon = 0,
            goalsScored = 0
        };
    }

    void EnsureDataIntegrity()
    {
        if (_data == null)
        {
            _data = CreateDefaultData();
            return;
        }

        if (string.IsNullOrWhiteSpace(_data.playerName))
            _data.playerName = string.IsNullOrWhiteSpace(defaultPlayerName) ? "Player" : defaultPlayerName.Trim();

        _data.trophies = Mathf.Max(0, _data.trophies);
        _data.matchesPlayed = Mathf.Max(0, _data.matchesPlayed);
        _data.matchesWon = Mathf.Clamp(_data.matchesWon, 0, _data.matchesPlayed);
        _data.goalsScored = Mathf.Max(0, _data.goalsScored);
    }

    void NotifyDataChanged(bool saveAfterNotify = true)
    {
        EnsureDataIntegrity();
        OnUserDataChanged?.Invoke(_data);

        if (saveAfterNotify && autoSaveOnChange)
            Save();
    }
}
