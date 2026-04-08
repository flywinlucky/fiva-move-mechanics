using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class UserDataTextView : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    UserData userData;

    [SerializeField]
    TMP_Text playerNameText;

    [SerializeField]
    TMP_Text trophiesText;

    [Header("Formatting")]
    [SerializeField]
    string emptyNameFallback = "Player";

    UserData _boundUserData;

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        RebindUserData();

        RefreshUI();
        StartCoroutine(RefreshNextFrame());
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (_boundUserData != null)
            _boundUserData.OnUserDataChanged -= HandleUserDataChanged;

        _boundUserData = null;
    }

    System.Collections.IEnumerator RefreshNextFrame()
    {
        yield return null;
        RebindUserData();
        RefreshUI();
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindUserData();
        RefreshUI();
    }

    public void RefreshUI()
    {
        RebindUserData();

        if (_boundUserData == null || _boundUserData.Data == null)
        {
            ApplyText(emptyNameFallback, 0);
            return;
        }

        UserProfileData data = _boundUserData.Data;
        ApplyText(data.playerName, data.trophies);
    }

    void HandleUserDataChanged(UserProfileData data)
    {
        if (data == null)
        {
            ApplyText(emptyNameFallback, 0);
            return;
        }

        ApplyText(data.playerName, data.trophies);
    }

    void ApplyText(string playerName, int trophies)
    {
        if (playerNameText != null)
            playerNameText.text = string.IsNullOrWhiteSpace(playerName) ? emptyNameFallback : playerName;

        if (trophiesText != null)
            trophiesText.text = Mathf.Max(0, trophies).ToString();
    }

    void RebindUserData()
    {
        UserData previous = _boundUserData;

        ResolveUserDataReference();

        if (previous == userData)
            return;

        if (previous != null)
            previous.OnUserDataChanged -= HandleUserDataChanged;

        _boundUserData = userData;

        if (_boundUserData != null)
            _boundUserData.OnUserDataChanged += HandleUserDataChanged;
    }

    void ResolveUserDataReference()
    {
        if (userData != null)
            return;

        userData = UserData.Instance;
        if (userData != null)
            return;

        userData = FindObjectOfType<UserData>();
    }
}
