using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

[DisallowMultipleComponent]
public class InGameLoadAiData : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text ai_Name_DataText;
    public TMP_Text ai_Trophies_DataText;

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
        AiData bridge = AiData.Instance != null ? AiData.Instance : FindObjectOfType<AiData>();

        string aiName = bridge != null ? bridge.AiName : "Opponent";
        int aiTrophies = bridge != null ? bridge.AiTrophies : 0;

        if (ai_Name_DataText != null)
            ai_Name_DataText.text = string.IsNullOrWhiteSpace(aiName) ? "Opponent" : aiName;

        if (ai_Trophies_DataText != null)
            ai_Trophies_DataText.text = Mathf.Max(0, aiTrophies).ToString();
    }
}
