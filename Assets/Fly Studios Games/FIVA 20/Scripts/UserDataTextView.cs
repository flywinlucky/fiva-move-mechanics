using TMPro;
using UnityEngine;

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

    void OnEnable()
    {
        ResolveUserDataReference();

        if (userData != null)
            userData.OnUserDataChanged += HandleUserDataChanged;

        RefreshUI();
    }

    void OnDisable()
    {
        if (userData != null)
            userData.OnUserDataChanged -= HandleUserDataChanged;
    }

    public void RefreshUI()
    {
        ResolveUserDataReference();

        if (userData == null || userData.Data == null)
        {
            ApplyText(emptyNameFallback, 0);
            return;
        }

        UserProfileData data = userData.Data;
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
