using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YG;
using UnityEngine.UI;
public class SimpleUITutorial : MonoBehaviour
{
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
        if (openOnStart)
        {
            OpenTutorial();
        }
    }

    public void CloseTutorial()
    {
        desktop_ControlsPanel.SetActive(false);
        mobile_ControlsPanel.SetActive(false);
        close_TutorialButton.gameObject.SetActive(false);
    }

    public void OpenTutorial()
    {
        bool deviceIsMobile = YG2.envir.isMobile;
        if (deviceIsMobile)
        {
            mobile_ControlsPanel.SetActive(true);
            desktop_ControlsPanel.SetActive(false);
              close_TutorialButton.gameObject.SetActive(true);
        }
        else
        {
            mobile_ControlsPanel.SetActive(false);
            desktop_ControlsPanel.SetActive(true);
              close_TutorialButton.gameObject.SetActive(true);
        }

        if (openOnStart)
        {
            //open_TutorialButton.gameObject.SetActive(false);
            close_TutorialButton.gameObject.SetActive(true);
        }
    }
}