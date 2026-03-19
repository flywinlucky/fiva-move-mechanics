using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] Button backButton;
    [SerializeField] Button storeShopPanelButton;
    [SerializeField] Button dailyRewardsPanelButton;

    [Header("Panels")]
    [SerializeField] GameObject statusBar;
    [SerializeField] GameObject mainMenu;
    [SerializeField] GameObject storeShopPanel;
    [SerializeField] GameObject dailyRewardsPanel;

    readonly List<GameObject> _managedPanels = new List<GameObject>();
    GameObject _activePanel;

    void Awake()
    {
        AutoWireButtonsIfMissing();
        BuildManagedPanelList();
        EnsureStatusBarActive();
    }

    void OnEnable()
    {
        RegisterButtonListeners();

        // Start from main menu by default and keep only one main panel active.
        ShowPanel(mainMenu, true);
    }

    void OnDisable()
    {
        UnregisterButtonListeners();
    }

    void RegisterButtonListeners()
    {
        AutoWireButtonsIfMissing();

        if (backButton != null)
            backButton.onClick.AddListener(BackToMainMenu);

        if (storeShopPanelButton != null)
            storeShopPanelButton.onClick.AddListener(OpenStoreShopPanel);

        if (dailyRewardsPanelButton != null)
            dailyRewardsPanelButton.onClick.AddListener(OpenDailyRewardsPanel);
    }

    void UnregisterButtonListeners()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(BackToMainMenu);

        if (storeShopPanelButton != null)
            storeShopPanelButton.onClick.RemoveListener(OpenStoreShopPanel);

        if (dailyRewardsPanelButton != null)
            dailyRewardsPanelButton.onClick.RemoveListener(OpenDailyRewardsPanel);
    }

    void BuildManagedPanelList()
    {
        _managedPanels.Clear();
        AddPanelIfMissing(mainMenu);
        AddPanelIfMissing(storeShopPanel);
        AddPanelIfMissing(dailyRewardsPanel);
    }

    void AddPanelIfMissing(GameObject panel)
    {
        if (panel == null)
            return;

        if (!_managedPanels.Contains(panel))
            _managedPanels.Add(panel);
    }

    void EnsureStatusBarActive()
    {
        if (statusBar != null && !statusBar.activeSelf)
            statusBar.SetActive(true);
    }

    void AutoWireButtonsIfMissing()
    {
        if (backButton == null)
            backButton = FindButtonByNameContains("back");

        if (storeShopPanelButton == null)
            storeShopPanelButton = FindButtonByNameContains("shop");

        if (dailyRewardsPanelButton == null)
            dailyRewardsPanelButton = FindButtonByNameContains("reward");
    }

    Button FindButtonByNameContains(string nameContains)
    {
        if (string.IsNullOrEmpty(nameContains))
            return null;

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            string buttonName = button.name;
            if (!string.IsNullOrEmpty(buttonName)
                && buttonName.ToLowerInvariant().Contains(nameContains.ToLowerInvariant()))
            {
                return button;
            }
        }

        return null;
    }

    public void BackToMainMenu()
    {
        ShowPanel(mainMenu);
    }

    public void OpenStoreShopPanel()
    {
        ShowPanel(storeShopPanel);
    }

    public void OpenDailyRewardsPanel()
    {
        ShowPanel(dailyRewardsPanel);
    }

    public void OpenMainMenuPanel()
    {
        ShowPanel(mainMenu);
    }

    public void ShowPanel(GameObject panelToShow)
    {
        ShowPanel(panelToShow, false);
    }

    void ShowPanel(GameObject panelToShow, bool forceRefresh)
    {
        EnsureStatusBarActive();

        if (panelToShow == null)
        {
            Debug.LogWarning("[UIManager] Panel to show is null.", this);
            return;
        }

        BuildManagedPanelList();

        bool alreadyActive = _activePanel == panelToShow && panelToShow.activeSelf;
        if (alreadyActive && !forceRefresh)
            return;

        for (int i = 0; i < _managedPanels.Count; i++)
        {
            GameObject panel = _managedPanels[i];
            if (panel == null)
                continue;

            bool shouldBeActive = panel == panelToShow;
            if (panel.activeSelf != shouldBeActive)
                panel.SetActive(shouldBeActive);
        }

        _activePanel = panelToShow;
        EnsureStatusBarActive();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            AutoWireButtonsIfMissing();
            BuildManagedPanelList();
        }
    }
#endif
}
