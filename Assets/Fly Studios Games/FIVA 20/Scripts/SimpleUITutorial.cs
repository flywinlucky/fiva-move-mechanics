using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YG;
using UnityEngine.UI;

public class SimpleUITutorial : MonoBehaviour
{
    const string AutoOpenDismissedKey = "SimpleUITutorial.AutoOpenDismissed";

    public bool openOnStart;
    public GameObject desktop_ControlsPanel;
    public GameObject mobile_ControlsPanel;


    public Button open_TutorialButton;
    public Button close_TutorialButton;


    private void Awake()
    {
        if (open_TutorialButton != null)
        {
            open_TutorialButton.onClick.AddListener(OpenTutorial);
        }
        if (close_TutorialButton != null)
        {
            close_TutorialButton.onClick.AddListener(CloseTutorial);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        bool shouldAutoOpen = openOnStart && !IsAutoOpenDismissed();
        if (shouldAutoOpen)
        {
            OpenTutorial();
            return;
        }

        CloseTutorial();
    }

    public void CloseTutorial()
    {
        if (desktop_ControlsPanel != null)
            desktop_ControlsPanel.SetActive(false);

        if (mobile_ControlsPanel != null)
            mobile_ControlsPanel.SetActive(false);

        if (close_TutorialButton != null)
            close_TutorialButton.gameObject.SetActive(false);

        // If tutorial auto-open is enabled, remember user dismissed it once.
        if (openOnStart)
            SetAutoOpenDismissed(true);
    }

    public void OpenTutorial()
    {
        bool deviceIsMobile = YG2.envir.isMobile;
        if (deviceIsMobile)
        {
            if (mobile_ControlsPanel != null)
                mobile_ControlsPanel.SetActive(true);

            if (desktop_ControlsPanel != null)
                desktop_ControlsPanel.SetActive(false);

            if (close_TutorialButton != null)
                close_TutorialButton.gameObject.SetActive(true);
        }
        else
        {
            if (mobile_ControlsPanel != null)
                mobile_ControlsPanel.SetActive(false);

            if (desktop_ControlsPanel != null)
                desktop_ControlsPanel.SetActive(true);

            if (close_TutorialButton != null)
                close_TutorialButton.gameObject.SetActive(true);
        }

        if (open_TutorialButton != null)
            open_TutorialButton.gameObject.SetActive(true);
    }

    bool IsAutoOpenDismissed()
    {
        return PlayerPrefs.GetInt(AutoOpenDismissedKey, 0) == 1;
    }

    void SetAutoOpenDismissed(bool dismissed)
    {
        PlayerPrefs.SetInt(AutoOpenDismissedKey, dismissed ? 1 : 0);
        PlayerPrefs.Save();
    }
}