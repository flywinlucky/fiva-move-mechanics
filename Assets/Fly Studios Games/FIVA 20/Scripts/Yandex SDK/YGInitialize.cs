using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YG;

public class YGInitialize : MonoBehaviour
{
    private void Start()
    {
        YG2.GameReadyAPI();
        Debug.Log("GameReady");

       YG2.SwitchLanguage("en");
    }
}