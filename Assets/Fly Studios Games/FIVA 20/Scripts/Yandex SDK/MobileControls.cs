using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YG;

public class MobileControls : MonoBehaviour
{
    public GameObject mobileControlsPanel;
    // Start is called before the first frame update
    void Start()
    {
        bool deviceIsMobile = YG2.envir.isMobile;
        if (deviceIsMobile)
        {
            mobileControlsPanel.SetActive(true);
        }
        else
        {
            mobileControlsPanel.SetActive(false);
        }
    }
}