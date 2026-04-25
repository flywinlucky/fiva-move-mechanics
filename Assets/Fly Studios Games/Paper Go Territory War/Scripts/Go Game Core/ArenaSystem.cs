using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using AssetKits.ParticleImage;

[System.Serializable]
public class ArenaDefinition
{
    public string arenaName = "Beginner";
    [Min(0)] public int requiredTrophies;
}

public class ArenaSystem : MonoBehaviour
{
    private const string PendingTrophyGainPrefsKey = "ArenaSystem.PendingTrophyGain";

    public const int DefaultWinTrophyChange = 30;
    public const int DefaultLossTrophyChange = 25;

    [Header("UI")]
    public ParticleImage trophyParticleImage;
    public Slider currentProgressSlider;
    public TMP_Text currentArenaNameText;
    public TMP_Text currentArenaNumberText;
    public TMP_Text currentTrophyProgressText;

    [Header("UI Animation")]
    [SerializeField] [Min(0.01f)] private float sliderSmoothTime = 0.18f;
    [SerializeField] [Min(0.01f)] private float trophyTextSmoothTime = 0.18f;

    [Header("Trophy Economy")]
    [SerializeField] [Min(0)] private int winTrophyChange = DefaultWinTrophyChange;
    [SerializeField] [Min(0)] private int lossTrophyChange = DefaultLossTrophyChange;
    [SerializeField] private bool useArenaGateProtection = true;

    [Header("Opponent Trophy Simulation")]
    [SerializeField] private bool useCurrentTrophiesAsOpponentBase = true;
    [SerializeField] [Min(0)] private int fixedOpponentTrophies;
    [SerializeField] private int opponentTrophyOffset;

    [Header("Arenas")]
    [Tooltip("Assign a text file with lines like '001. Beginner - 0'.")]
    public TextAsset arenaLevelsList;
    [SerializeField] private List<ArenaDefinition> arenas = new List<ArenaDefinition>();

    [Header("Debug Match Result")]
    [SerializeField] private bool enableDebugHotkeys = true;
    [SerializeField] private KeyCode addTrophyKey = KeyCode.Z;
    [SerializeField] private KeyCode removeTrophyKey = KeyCode.X;
    public Button increaseTrophyButton;
    public Button decreaseTrophyButton;
    public Button resetDataButton;

    private UserData userData;
    private bool warnedMissingUserData;
    private float displayedSliderValue;
    private float displayedSliderVelocity;
    private float displayedTrophies;
    private float displayedTrophiesVelocity;
    private bool animationInitialized;
    private int currentDisplayedArenaRequirement = int.MinValue;
    private bool hasPendingMatchedOpponentTrophies;
    private int pendingMatchedOpponentTrophies;
    private bool warnedInvalidArenaLevelsList;

    public static void SetPendingTrophyGainAnimation(int gainedTrophies)
    {
        int sanitizedGain = Mathf.Max(0, gainedTrophies);
        if (sanitizedGain <= 0)
            return;

        int existingGain = Mathf.Max(0, PlayerPrefs.GetInt(PendingTrophyGainPrefsKey, 0));
        PlayerPrefs.SetInt(PendingTrophyGainPrefsKey, existingGain + sanitizedGain);
        PlayerPrefs.Save();
    }

    private struct ArenaProgress
    {
        public int currentArenaNumber;
        public string currentArenaName;
        public string nextArenaName;
        public int currentArenaRequirement;
        public int nextArenaRequirement;
        public bool isMaxArena;
        public float progress01;
    }

    private void OnEnable()
    {
        EnsureArenaSetup();
        RegisterDebugButtons();
        BindUserData();
        RefreshArenaUI();
    }

    private void OnValidate()
    {
        EnsureArenaSetup();
    }

    private void Start()
    {
        EnsureArenaSetup();
        BindUserData();
        RefreshArenaUI();
    }

    private void OnDisable()
    {
        UnregisterDebugButtons();

        if (userData != null)
            userData.OnUserDataChanged -= HandleUserDataChanged;

        userData = null;
    }

    private void Update()
    {
        if (enableDebugHotkeys)
        {
            if (Input.GetKeyDown(addTrophyKey))
                IncreaseTrophies();

            if (Input.GetKeyDown(removeTrophyKey))
                DecreaseTrophies();
        }

        AnimateArenaUI();
    }

    private void RegisterDebugButtons()
    {
        if (increaseTrophyButton != null)
        {
            increaseTrophyButton.onClick.RemoveListener(IncreaseTrophies);
            increaseTrophyButton.onClick.AddListener(IncreaseTrophies);
        }

        if (decreaseTrophyButton != null)
        {
            decreaseTrophyButton.onClick.RemoveListener(DecreaseTrophies);
            decreaseTrophyButton.onClick.AddListener(DecreaseTrophies);
        }

        if (resetDataButton != null)
        {
            resetDataButton.onClick.RemoveListener(ResetProgressData);
            resetDataButton.onClick.AddListener(ResetProgressData);
        }
    }

    private void UnregisterDebugButtons()
    {
        if (increaseTrophyButton != null)
            increaseTrophyButton.onClick.RemoveListener(IncreaseTrophies);

        if (decreaseTrophyButton != null)
            decreaseTrophyButton.onClick.RemoveListener(DecreaseTrophies);

        if (resetDataButton != null)
            resetDataButton.onClick.RemoveListener(ResetProgressData);
    }

    private void IncreaseTrophies()
    {
        int trophiesBefore = GetCurrentTrophies();
        ApplyBattleResult(true);

        if (GetCurrentTrophies() > trophiesBefore)
            PlayTrophyGainEffect();
    }

    private void DecreaseTrophies()
    {
        ApplyBattleResult(false);
    }

    private void ResetProgressData()
    {
        if (!TryGetUserData())
            return;

        ClearPendingMatchedOpponentTrophies();
        userData.ResetProgressKeepName();
        Debug.Log("[ArenaSystem] Player trophies and saved progress were reset locally and in cloud when authorized.", this);
    }

    public void ApplyBattleResult(bool playerWon)
    {
        if (!TryGetUserData())
            return;

        int playerTrophiesBefore = GetCurrentTrophies();
        int opponentTrophies = ResolveBattleOpponentTrophies(playerTrophiesBefore);
        ApplyBattleResult(playerWon, opponentTrophies);
    }

    public void ApplyBattleResult(bool playerWon, int opponentTrophies)
    {
        if (!TryGetUserData())
            return;

        EnsureArenaSetup();

        int playerTrophiesBefore = GetCurrentTrophies();
        int clampedOpponentTrophies = Mathf.Max(0, opponentTrophies);
        int trophyChange = CalculateBattleTrophyChange(playerTrophiesBefore, clampedOpponentTrophies, playerWon);

        if (trophyChange <= 0)
            return;

        if (playerWon)
        {
            userData.AddTrophies(trophyChange);
            ClearPendingMatchedOpponentTrophies();
            userData.Save();
            Debug.Log($"[ArenaSystem] Victory vs {clampedOpponentTrophies} trophies: +{trophyChange}. Total: {GetCurrentTrophies()}", this);
            return;
        }

        int floorTrophies = useArenaGateProtection ? CalculateArenaProgress(playerTrophiesBefore).currentArenaRequirement : 0;
        int targetTrophies = Mathf.Max(floorTrophies, playerTrophiesBefore - trophyChange);
        int appliedLoss = Mathf.Max(0, playerTrophiesBefore - targetTrophies);

        if (appliedLoss > 0)
            userData.SpendTrophies(appliedLoss);

        ClearPendingMatchedOpponentTrophies();
        userData.Save();
        Debug.Log($"[ArenaSystem] Defeat vs {clampedOpponentTrophies} trophies: -{appliedLoss}. Total: {GetCurrentTrophies()}", this);
    }

    public void ApplyDraw()
    {
        if (!TryGetUserData())
            return;

        int playerTrophies = GetCurrentTrophies();
        int opponentTrophies = ResolveBattleOpponentTrophies(playerTrophies);
        ClearPendingMatchedOpponentTrophies();
        Debug.Log($"[ArenaSystem] Draw vs {opponentTrophies} trophies: 0 trophies changed. Total: {playerTrophies}", this);
    }

    public void SetMatchedOpponentTrophies(int opponentTrophies)
    {
        pendingMatchedOpponentTrophies = Mathf.Max(0, opponentTrophies);
        hasPendingMatchedOpponentTrophies = true;
    }

    public int CalculateBattleTrophyChange(int playerTrophies, int opponentTrophies, bool playerWon)
    {
        return playerWon
            ? Mathf.Max(0, winTrophyChange)
            : Mathf.Max(0, lossTrophyChange);
    }

    public int ResolveConfiguredOpponentTrophies(int playerTrophies)
    {
        int baseOpponentTrophies = useCurrentTrophiesAsOpponentBase
            ? Mathf.Max(0, playerTrophies)
            : Mathf.Max(0, fixedOpponentTrophies);

        return Mathf.Max(0, baseOpponentTrophies + opponentTrophyOffset);
    }

    private int ResolveBattleOpponentTrophies(int playerTrophies)
    {
        if (hasPendingMatchedOpponentTrophies)
            return pendingMatchedOpponentTrophies;

        return ResolveConfiguredOpponentTrophies(playerTrophies);
    }

    private void ClearPendingMatchedOpponentTrophies()
    {
        hasPendingMatchedOpponentTrophies = false;
        pendingMatchedOpponentTrophies = 0;
    }

    private void HandleUserDataChanged(UserProfileData _)
    {
        RefreshArenaUI();
    }

    private void BindUserData()
    {
        if (userData != null)
            return;

        userData = UserData.Instance;
        if (userData == null)
            userData = FindObjectOfType<UserData>();

        if (userData != null)
        {
            userData.OnUserDataChanged -= HandleUserDataChanged;
            userData.OnUserDataChanged += HandleUserDataChanged;
        }
    }

    private bool TryGetUserData()
    {
        if (userData != null)
            return true;

        BindUserData();

        if (userData != null)
            return true;

        if (!warnedMissingUserData)
        {
            warnedMissingUserData = true;
            Debug.LogWarning("[ArenaSystem] UserData was not found in scene.", this);
        }

        return false;
    }

    private int GetCurrentTrophies()
    {
        if (!TryGetUserData() || userData.Data == null)
            return 0;

        return Mathf.Max(0, userData.Data.trophies);
    }

    private void EnsureArenaSetup()
    {
        if (arenas == null)
            arenas = new List<ArenaDefinition>();

        bool importedFromTextAsset = TryApplyArenaLevelsFromTextAsset();

        if (!importedFromTextAsset)
        {
            arenas.Clear();
            return;
        }

        arenas.RemoveAll(arena => arena == null);

        if (arenas.Count == 0)
            return;

        arenas.Sort((a, b) => a.requiredTrophies.CompareTo(b.requiredTrophies));

        if (arenas[0].requiredTrophies > 0)
            arenas.Insert(0, new ArenaDefinition { arenaName = "Beginner", requiredTrophies = 0 });
    }

    private bool TryApplyArenaLevelsFromTextAsset()
    {
        if (arenaLevelsList == null)
        {
            if (!warnedInvalidArenaLevelsList)
            {
                Debug.LogWarning("[ArenaSystem] arenaLevelsList is missing. Arena progression will not be loaded.", this);
                warnedInvalidArenaLevelsList = true;
            }

            return false;
        }

        if (TryParseArenaLevels(arenaLevelsList.text, out List<ArenaDefinition> parsedArenas))
        {
            arenas = parsedArenas;
            warnedInvalidArenaLevelsList = false;
            return true;
        }

        if (!warnedInvalidArenaLevelsList)
        {
            Debug.LogWarning("[ArenaSystem] The assigned arenaLevelsList TextAsset could not be parsed. Expected lines like '001. Beginner - 0'.", this);
            warnedInvalidArenaLevelsList = true;
        }

        return false;
    }

    private static bool TryParseArenaLevels(string source, out List<ArenaDefinition> parsedArenas)
    {
        parsedArenas = new List<ArenaDefinition>();

        if (string.IsNullOrWhiteSpace(source))
            return false;

        string[] lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || !char.IsDigit(line[0]))
                continue;

            string namePart;
            string trophiesPart;

            int spacedSeparatorIndex = line.LastIndexOf(" - ", StringComparison.Ordinal);
            if (spacedSeparatorIndex >= 0)
            {
                namePart = line.Substring(0, spacedSeparatorIndex).Trim();
                trophiesPart = line.Substring(spacedSeparatorIndex + 3).Trim();
            }
            else
            {
                int separatorIndex = line.LastIndexOf('-');
                if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
                    continue;

                namePart = line.Substring(0, separatorIndex).Trim();
                trophiesPart = line.Substring(separatorIndex + 1).Trim();
            }

            int prefixSeparatorIndex = namePart.IndexOf('.');
            if (prefixSeparatorIndex >= 0 && prefixSeparatorIndex < namePart.Length - 1)
                namePart = namePart.Substring(prefixSeparatorIndex + 1).Trim();

            if (string.IsNullOrWhiteSpace(namePart) || !int.TryParse(trophiesPart, out int requiredTrophies))
                continue;

            parsedArenas.Add(new ArenaDefinition
            {
                arenaName = namePart,
                requiredTrophies = Mathf.Max(0, requiredTrophies)
            });
        }

        return parsedArenas.Count > 0;
    }

    private void RefreshArenaUI()
    {
        EnsureArenaSetup();

        int trophies = GetCurrentTrophies();

        if (!Application.isPlaying)
        {
            ApplyImmediateArenaState(trophies);
            return;
        }

        if (!animationInitialized)
        {
            ApplyInitialArenaStateWithPossibleGainAnimation(trophies);
            return;
        }

        ApplyArenaState(Mathf.RoundToInt(displayedTrophies), displayedSliderValue);
    }

    private void AnimateArenaUI()
    {
        int targetTrophies = GetCurrentTrophies();

        if (!Application.isPlaying || !animationInitialized)
        {
            ApplyImmediateArenaState(targetTrophies);
            return;
        }

        if (Mathf.Abs(displayedTrophies - targetTrophies) < 0.05f)
        {
            displayedTrophies = targetTrophies;
            displayedTrophiesVelocity = 0f;
        }
        else
        {
            displayedTrophies = Mathf.SmoothDamp(displayedTrophies, targetTrophies, ref displayedTrophiesVelocity, trophyTextSmoothTime);
        }

        int shownTrophies = Mathf.RoundToInt(displayedTrophies);
        ArenaProgress progress = CalculateArenaProgress(shownTrophies);

        if (currentDisplayedArenaRequirement != progress.currentArenaRequirement)
        {
            currentDisplayedArenaRequirement = progress.currentArenaRequirement;
            displayedSliderValue = progress.progress01;
            displayedSliderVelocity = 0f;
        }
        else if (Mathf.Abs(displayedSliderValue - progress.progress01) < 0.001f)
        {
            displayedSliderValue = progress.progress01;
            displayedSliderVelocity = 0f;
        }
        else
        {
            displayedSliderValue = Mathf.SmoothDamp(displayedSliderValue, progress.progress01, ref displayedSliderVelocity, sliderSmoothTime);
        }

        ApplyArenaState(shownTrophies, displayedSliderValue);
    }

    private void ApplyImmediateArenaState(int trophies)
    {
        ArenaProgress progress = CalculateArenaProgress(trophies);
        displayedTrophies = trophies;
        displayedTrophiesVelocity = 0f;
        displayedSliderValue = progress.progress01;
        displayedSliderVelocity = 0f;
        currentDisplayedArenaRequirement = progress.currentArenaRequirement;
        animationInitialized = true;
        ApplyArenaState(trophies, progress.progress01);
    }

    private void ApplyInitialArenaStateWithPossibleGainAnimation(int currentTrophies)
    {
        int startTrophies = currentTrophies;
        int pendingGain = ConsumePendingTrophyGainAnimation();

        if (pendingGain > 0)
        {
            startTrophies = Mathf.Max(0, currentTrophies - pendingGain);
            PlayTrophyGainEffect();
        }

        ApplyImmediateArenaState(startTrophies);
    }

    private int ConsumePendingTrophyGainAnimation()
    {
        int pendingGain = Mathf.Max(0, PlayerPrefs.GetInt(PendingTrophyGainPrefsKey, 0));
        if (pendingGain <= 0)
            return 0;

        PlayerPrefs.DeleteKey(PendingTrophyGainPrefsKey);
        PlayerPrefs.Save();
        return pendingGain;
    }

    private void PlayTrophyGainEffect()
    {
        if (trophyParticleImage == null)
            return;

        trophyParticleImage.Stop(true);
        trophyParticleImage.Play();
    }

    private void ApplyArenaState(int trophies, float sliderValue)
    {
        ArenaProgress progress = CalculateArenaProgress(trophies);

        if (progress.currentArenaNumber <= 0)
        {
            if (currentArenaNumberText != null)
                currentArenaNumberText.text = "ARENA -";

            if (currentArenaNameText != null)
                currentArenaNameText.text = progress.currentArenaName;

            if (currentTrophyProgressText != null)
                currentTrophyProgressText.text = $"{trophies} trophies";

            if (currentProgressSlider != null)
            {
                currentProgressSlider.minValue = 0f;
                currentProgressSlider.maxValue = 1f;
                currentProgressSlider.value = 0f;
            }

            return;
        }

        if (currentArenaNumberText != null)
            currentArenaNumberText.text = $"ARENA {progress.currentArenaNumber}";

        if (currentArenaNameText != null)
            currentArenaNameText.text = progress.currentArenaName;

        if (currentTrophyProgressText != null)
        {
            if (progress.isMaxArena)
            {
                currentTrophyProgressText.text = $"{trophies} trophies (MAX Arena)";
            }
            else
            {
                currentTrophyProgressText.text = $"{trophies} / {progress.nextArenaRequirement}";
            }
        }

        if (currentProgressSlider != null)
        {
            currentProgressSlider.minValue = 0f;
            currentProgressSlider.maxValue = 1f;
            currentProgressSlider.value = sliderValue;
        }
    }

    private ArenaProgress CalculateArenaProgress(int trophies)
    {
        if (arenas == null || arenas.Count == 0)
        {
            return new ArenaProgress
            {
                currentArenaNumber = 0,
                currentArenaName = "No Arena List",
                nextArenaName = "No Arena List",
                currentArenaRequirement = 0,
                nextArenaRequirement = 0,
                isMaxArena = true,
                progress01 = 0f
            };
        }

        ArenaProgress result = new ArenaProgress
        {
            currentArenaNumber = 1,
            currentArenaName = arenas[0].arenaName,
            nextArenaName = arenas[0].arenaName,
            currentArenaRequirement = arenas[0].requiredTrophies,
            nextArenaRequirement = arenas[0].requiredTrophies,
            isMaxArena = true,
            progress01 = 1f
        };

        int currentIndex = 0;

        for (int i = 0; i < arenas.Count; i++)
        {
            if (trophies >= arenas[i].requiredTrophies)
                currentIndex = i;
            else
                break;
        }

        result.currentArenaNumber = currentIndex + 1;
        result.currentArenaName = arenas[currentIndex].arenaName;
        result.currentArenaRequirement = arenas[currentIndex].requiredTrophies;

        if (currentIndex >= arenas.Count - 1)
        {
            result.nextArenaName = arenas[currentIndex].arenaName;
            result.nextArenaRequirement = arenas[currentIndex].requiredTrophies;
            result.isMaxArena = true;
            result.progress01 = 1f;
            return result;
        }

        int nextIndex = currentIndex + 1;
        result.nextArenaName = arenas[nextIndex].arenaName;
        result.nextArenaRequirement = arenas[nextIndex].requiredTrophies;
        result.isMaxArena = false;

        int requiredRange = Mathf.Max(1, result.nextArenaRequirement - result.currentArenaRequirement);
        int gainedInArena = Mathf.Clamp(trophies - result.currentArenaRequirement, 0, requiredRange);
        result.progress01 = gainedInArena / (float)requiredRange;

        return result;
    }
}
