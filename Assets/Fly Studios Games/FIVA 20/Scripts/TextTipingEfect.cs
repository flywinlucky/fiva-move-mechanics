using System.Collections;
using UnityEngine;
using TMPro;

public class TextTipingEfect : MonoBehaviour
{
    [SerializeField]
    [Min(0.001f)]
    float characterDelay = 0.05f;

    [SerializeField]
    bool playOnEnable = true;

    TMP_Text _tmpText;
    string _fullText;
    Coroutine _typingRoutine;

    void Awake()
    {
        _tmpText = GetComponent<TMP_Text>();
        if (_tmpText != null)
            _fullText = _tmpText.text;
    }

    void OnEnable()
    {
        if (playOnEnable)
            PlayTyping();
    }

    public void PlayTyping()
    {
        if (_tmpText == null)
            _tmpText = GetComponent<TMP_Text>();

        if (_tmpText == null)
            return;

        _fullText = _tmpText.text;

        if (_typingRoutine != null)
            StopCoroutine(_typingRoutine);

        _typingRoutine = StartCoroutine(TypeRoutine());
    }

    public void SetTextAndPlay(string value)
    {
        if (_tmpText == null)
            _tmpText = GetComponent<TMP_Text>();

        if (_tmpText == null)
            return;

        _tmpText.text = value;
        _fullText = value;
        PlayTyping();
    }

    IEnumerator TypeRoutine()
    {
        _tmpText.maxVisibleCharacters = 0;
        _tmpText.ForceMeshUpdate();

        int totalVisibleCharacters = _tmpText.textInfo.characterCount;
        if (totalVisibleCharacters == 0)
        {
            _typingRoutine = null;
            yield break;
        }

        float wait = Mathf.Max(0.001f, characterDelay);
        for (int i = 1; i <= totalVisibleCharacters; i++)
        {
            _tmpText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(wait);
        }

        _typingRoutine = null;
    }
}
