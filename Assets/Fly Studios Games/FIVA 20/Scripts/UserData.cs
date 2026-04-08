using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using YG;

[Serializable]
public class UserProfileData
{
    public string playerName = "Player";
    public int trophies;
    public int matchesPlayed;
    public int matchesWon;
    public int goalsScored;
    public long saveRevision;
    public long lastSaveUnixTime;
}

public enum SaveFreshness
{
    Equal,
    CloudNewer,
    LocalNewer
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

    [Header("Yandex Cloud (PluginYG2)")]
    [SerializeField]
    bool useYandexCloudStorage = true;

    [SerializeField]
    bool preferAuthorizedNickname = true;

    [SerializeField]
    bool refreshBindingsOnSceneLoaded = true;

    [SerializeField]
    bool reloadCloudOnFocus = true;

    [Header("Debug")]
    [SerializeField]
    bool enableDebugTrophyHotkeys = true;

    [SerializeField]
    [Min(1)]
    int debugTrophyStep = 23;

    [SerializeField]
    UserProfileData _data = new UserProfileData();

    bool _isApplyingExternalData;

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

        LoadFromLocalBackup();

        TrySyncFromYGData();
    }

    void OnEnable()
    {
        YG2.onGetSDKData += HandleYGDataReady;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (YG2.isSDKEnabled)
            HandleYGDataReady();
    }

    void OnDisable()
    {
        YG2.onGetSDKData -= HandleYGDataReady;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryAdoptAuthorizedNickname();

        if (!refreshBindingsOnSceneLoaded)
            return;

        NotifyDataChanged(saveAfterNotify: false);
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            Save();
    }

    void OnApplicationFocus(bool hasFocus)
    {
#if Storage_yg
        if (!hasFocus || !reloadCloudOnFocus)
            return;

        if (ShouldUseYGCloud())
            YG.Insides.YGInsides.LoadProgress();
#endif
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
        StampLocalVersion();

        SaveToLocalBackup();

        if (ShouldUseYGCloud())
        {
            PushCurrentDataToYGSaves();
            YG2.SaveProgress();
        }
    }

    public void Load()
    {
        LoadFromLocalBackup();
        TrySyncFromYGData();
    }

    void LoadFromLocalBackup()
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

    void SaveToLocalBackup()
    {
        EnsureDataIntegrity();

        string json = JsonUtility.ToJson(_data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    void HandleYGDataReady()
    {
        TrySyncFromYGData();
        TryAdoptAuthorizedNickname();
    }

    public void ResetData(bool saveImmediately = true)
    {
        _data = CreateDefaultData();
        NotifyDataChanged(saveAfterNotify: saveImmediately);
    }

    public void ResetProgressKeepName(bool saveImmediately = true)
    {
        string preservedName = _data != null ? _data.playerName : defaultPlayerName;

        _data = CreateDefaultData();

        if (!string.IsNullOrWhiteSpace(preservedName))
            _data.playerName = preservedName.Trim();

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
            goalsScored = 0,
            saveRevision = 0,
            lastSaveUnixTime = 0
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
        _data.saveRevision = Math.Max(0, _data.saveRevision);
        _data.lastSaveUnixTime = Math.Max(0, _data.lastSaveUnixTime);
    }

    void NotifyDataChanged(bool saveAfterNotify = true)
    {
        EnsureDataIntegrity();
        OnUserDataChanged?.Invoke(_data);

        if (!_isApplyingExternalData && saveAfterNotify && autoSaveOnChange)
            Save();
    }

    bool ShouldUseYGCloud()
    {
#if Storage_yg
        return useYandexCloudStorage && YG2.isSDKEnabled;
#else
        return false;
#endif
    }

    void TrySyncFromYGData()
    {
#if Storage_yg
        if (!ShouldUseYGCloud())
            return;

        EnsureDataIntegrity();

        if (!HasMeaningfulYGSaves())
        {
            // Keep current local profile when cloud/local plugin save is still empty.
            // This prevents 0-value overwrites after scene changes or early SDK callbacks.
            NotifyDataChanged(saveAfterNotify: false);

            if (HasAnyProgress(_data))
            {
                PushCurrentDataToYGSaves();
                YG2.SaveProgress();
            }

            return;
        }

        SaveFreshness freshness = CompareFreshnessWithYG();

        if (freshness == SaveFreshness.CloudNewer)
        {
            ApplyYGDataToLocal();
            return;
        }

        if (freshness == SaveFreshness.LocalNewer)
        {
            PushCurrentDataToYGSaves();
            YG2.SaveProgress();
            NotifyDataChanged(saveAfterNotify: false);
            return;
        }

        // Equal freshness: keep local data and only refresh UI bindings.
        NotifyDataChanged(saveAfterNotify: false);
#endif
    }

    void ApplyYGDataToLocal()
    {
        _isApplyingExternalData = true;

        string resolvedName = ResolveNameFromYG();
        if (!string.IsNullOrWhiteSpace(resolvedName))
            _data.playerName = resolvedName;

        _data.trophies = Mathf.Max(0, YG2.saves.userTrophies);
        _data.matchesPlayed = Mathf.Max(0, YG2.saves.userMatchesPlayed);
        _data.matchesWon = Mathf.Max(0, YG2.saves.userMatchesWon);
        _data.goalsScored = Mathf.Max(0, YG2.saves.userGoalsScored);
        _data.saveRevision = Math.Max(0, YG2.saves.userSaveRevision);
        _data.lastSaveUnixTime = Math.Max(0, YG2.saves.userLastSaveUnixTime);

        EnsureDataIntegrity();
        SaveToLocalBackup();
        NotifyDataChanged(saveAfterNotify: false);

        _isApplyingExternalData = false;
    }

    SaveFreshness CompareFreshnessWithYG()
    {
#if Storage_yg
        long cloudRevision = Math.Max(0, YG2.saves.userSaveRevision);
        long localRevision = Math.Max(0, _data.saveRevision);
        if (cloudRevision > localRevision)
            return SaveFreshness.CloudNewer;
        if (cloudRevision < localRevision)
            return SaveFreshness.LocalNewer;

        long cloudTime = Math.Max(0, YG2.saves.userLastSaveUnixTime);
        long localTime = Math.Max(0, _data.lastSaveUnixTime);
        if (cloudTime > localTime)
            return SaveFreshness.CloudNewer;
        if (cloudTime < localTime)
            return SaveFreshness.LocalNewer;

        return SaveFreshness.Equal;
#else
        return SaveFreshness.Equal;
#endif
    }

    void StampLocalVersion()
    {
        EnsureDataIntegrity();

        _data.saveRevision = Math.Max(0, _data.saveRevision) + 1;

        long now = GetUnixNow();
        if (now <= _data.lastSaveUnixTime)
            now = _data.lastSaveUnixTime + 1;

        _data.lastSaveUnixTime = now;
    }

    long GetUnixNow()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    bool HasAnyProgress(UserProfileData data)
    {
        if (data == null)
            return false;

        if (!string.IsNullOrWhiteSpace(data.playerName) && !string.Equals(data.playerName, defaultPlayerName, StringComparison.OrdinalIgnoreCase))
            return true;

        return data.trophies > 0
            || data.matchesPlayed > 0
            || data.matchesWon > 0
            || data.goalsScored > 0;
    }

    bool HasMeaningfulYGSaves()
    {
#if Storage_yg
        if (YG2.saves == null)
            return false;

        if (YG2.saves.idSave > 0)
            return true;

        if (!string.IsNullOrWhiteSpace(YG2.saves.userPlayerName))
            return true;

        return YG2.saves.userTrophies > 0
            || YG2.saves.userMatchesPlayed > 0
            || YG2.saves.userMatchesWon > 0
            || YG2.saves.userGoalsScored > 0;
#else
        return false;
#endif
    }

    void PushCurrentDataToYGSaves()
    {
#if Storage_yg
        EnsureDataIntegrity();

        YG2.saves.userPlayerName = _data.playerName;
        YG2.saves.userTrophies = Mathf.Max(0, _data.trophies);
        YG2.saves.userMatchesPlayed = Mathf.Max(0, _data.matchesPlayed);
        YG2.saves.userMatchesWon = Mathf.Max(0, _data.matchesWon);
        YG2.saves.userGoalsScored = Mathf.Max(0, _data.goalsScored);
        YG2.saves.userSaveRevision = Math.Max(0, _data.saveRevision);
        YG2.saves.userLastSaveUnixTime = Math.Max(0, _data.lastSaveUnixTime);
#endif
    }

    string ResolveNameFromYG()
    {
#if Storage_yg
#if Authorization_yg
        if (preferAuthorizedNickname && IsAuthorizedYGName(YG2.player.name))
            return YG2.player.name;
#endif

        if (!string.IsNullOrWhiteSpace(YG2.saves.userPlayerName))
            return YG2.saves.userPlayerName;
#endif

        return _data != null ? _data.playerName : defaultPlayerName;
    }

    bool IsAuthorizedYGName(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return false;

        if (string.Equals(nickname, "unauthorized", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(nickname, "anonymous", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    void TryAdoptAuthorizedNickname()
    {
#if Authorization_yg
        if (!preferAuthorizedNickname)
            return;

        EnsureDataIntegrity();

        if (!IsUsingFallbackName(_data.playerName))
            return;

        string authorizedName = YG2.player.name;
        if (!IsAuthorizedYGName(authorizedName))
            return;

        _data.playerName = authorizedName;
        NotifyDataChanged();
#endif
    }

    bool IsUsingFallbackName(string currentName)
    {
        if (string.IsNullOrWhiteSpace(currentName))
            return true;

        if (string.Equals(currentName, defaultPlayerName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(currentName, "Player", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
