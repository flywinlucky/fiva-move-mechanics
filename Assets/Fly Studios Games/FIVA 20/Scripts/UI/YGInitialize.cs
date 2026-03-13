using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using YG;

public class YGInitialize : MonoBehaviour
{
    private void Start()
    {
        YG2.GameReadyAPI();
        Debug.Log("✅ GameReady() called — game is now ready for interaction.");

        ChangeLanguage("en");
    }

    public void ChangeLanguage(string prefixLanguage)
    {
        Debug.Log("INTER YG : Language changed to " + prefixLanguage);

        MethodInfo switchLanguageMethod = typeof(YG2).GetMethod("SwitchLanguage", BindingFlags.Public | BindingFlags.Static);
        if (switchLanguageMethod != null)
        {
            switchLanguageMethod.Invoke(null, new object[] { prefixLanguage });
            return;
        }

        Debug.LogWarning("YG2.SwitchLanguage is not available in current PluginYourGames build.");
    }
}