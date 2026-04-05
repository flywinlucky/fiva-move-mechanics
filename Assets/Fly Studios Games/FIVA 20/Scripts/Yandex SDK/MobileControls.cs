using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YG;

public class MobileControls : MonoBehaviour
{
    public GameObject mobileControlsPanel;
    public Camera mainCamera;
    // Start is called before the first frame update
    void Start()
    {
        bool deviceIsMobile = YG2.envir.isMobile;
        if (deviceIsMobile)
        {
            mobileControlsPanel.SetActive(true);
              mainCamera.fieldOfView = 42f;
        }
        else
        {
            mobileControlsPanel.SetActive(false);

            mainCamera.fieldOfView = 48f;
        }
    }
}