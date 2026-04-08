using UnityEngine;

[DisallowMultipleComponent]
public class AiData : MonoBehaviour
{
    public static AiData Instance { get; private set; }

    [SerializeField]
    string aiName = "Opponent";

    [SerializeField]
    int aiTrophies;

    public string AiName => aiName;
    public int AiTrophies => aiTrophies;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static AiData GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        AiData existing = FindObjectOfType<AiData>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("AiDataBridge");
        return go.AddComponent<AiData>();
    }

    public void SetAiData(string nameValue, int trophiesValue)
    {
        aiName = string.IsNullOrWhiteSpace(nameValue) ? "Opponent" : nameValue.Trim();
        aiTrophies = Mathf.Max(0, trophiesValue);
    }
}
