using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class PlayerWiget : MonoBehaviour
{
    public Image playerWigetImage;
    public TMP_Text playerWigetText;

    public void SetPlayerWiget(Color color, string text)
    {
        if (playerWigetImage != null)
            playerWigetImage.color = color;

        if (playerWigetText != null)
            playerWigetText.text = text;
    }
}
