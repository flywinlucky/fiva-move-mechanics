using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

namespace UCGUP
{
    public class GameManager : MonoBehaviour
{
    public static GameManager _instance;
    int _currentPanel;
    public Transform _panels;
    public TextMeshProUGUI _panelUGUI;
    public Texture2D _cursorIcon;
    // Start is called before the first frame update
    void Start()
    {
        _instance = this;
        _currentPanel = 0;
        _panelUGUI.text = _panels.GetChild(_currentPanel).name;
        _panels.GetChild(_currentPanel).gameObject.SetActive(true);
    }

    public void PreviousButton()
    {
        _currentPanel--;
        if (_currentPanel == -1)
        {
            _currentPanel = 34;
            _panels.GetChild(0).gameObject.SetActive(false);
        }

        else
        {
            _panels.GetChild(_currentPanel + 1).gameObject.SetActive(false);
        }

        _panels.GetChild(_currentPanel).gameObject.SetActive(true);
        _panelUGUI.text = _panels.GetChild(_currentPanel).name;


    }

    public void NextButton()
    {
        _currentPanel++;
        if (_currentPanel == 35)
        {
            _currentPanel = 0;
            _panels.GetChild(34).gameObject.SetActive(false);
        }
        else
        {
            _panels.GetChild(_currentPanel - 1).gameObject.SetActive(false);
        }

        _panels.GetChild(_currentPanel).gameObject.SetActive(true);
        _panelUGUI.text = _panels.GetChild(_currentPanel).name;

    }
}
}

