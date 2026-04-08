using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

[DisallowMultipleComponent]
public class InGameLoadUserData : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text user_Name_DataText;
    public TMP_Text user_Trophies_DataText;

    void Awake()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void Start()
    {
        RefreshUI();
    }

    void OnEnable()
    {
        RefreshUI();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        UserData userData = UserData.Instance != null ? UserData.Instance : FindObjectOfType<UserData>();

        string userName = "Player";
        int userTrophies = 0;

        if (userData != null && userData.Data != null)
        {
            userName = string.IsNullOrWhiteSpace(userData.Data.playerName)
                ? "Player"
                : userData.Data.playerName;
            userTrophies = Mathf.Max(0, userData.Data.trophies);
        }

        if (user_Name_DataText != null)
            user_Name_DataText.text = userName;

        if (user_Trophies_DataText != null)
            user_Trophies_DataText.text = userTrophies.ToString();
    }
}
