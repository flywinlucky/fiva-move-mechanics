using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UserDataTrophyDebugButtons : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    UserData userData;

    [SerializeField]
    Button addTrophyButton;

    [SerializeField]
    Button decreaseTrophyButton;

    [SerializeField]
    Button resetProgressButton;

    [Header("Debug Values")]
    [SerializeField]
    [Min(1)]
    int addAmount = 23;

    [SerializeField]
    [Min(1)]
    int decreaseAmount = 23;

    void Awake()
    {
        ResolveUserData();
    }

    void OnEnable()
    {
        BindButtons();
    }

    void OnDisable()
    {
        UnbindButtons();
    }

    void ResolveUserData()
    {
        if (userData != null)
            return;

        userData = UserData.Instance;
        if (userData != null)
            return;

        userData = FindObjectOfType<UserData>();
    }

    void BindButtons()
    {
        if (addTrophyButton != null)
        {
            addTrophyButton.onClick.RemoveListener(OnAddTrophyClicked);
            addTrophyButton.onClick.AddListener(OnAddTrophyClicked);
        }

        if (decreaseTrophyButton != null)
        {
            decreaseTrophyButton.onClick.RemoveListener(OnDecreaseTrophyClicked);
            decreaseTrophyButton.onClick.AddListener(OnDecreaseTrophyClicked);
        }

        if (resetProgressButton != null)
        {
            resetProgressButton.onClick.RemoveListener(OnResetProgressClicked);
            resetProgressButton.onClick.AddListener(OnResetProgressClicked);
        }
    }

    void UnbindButtons()
    {
        if (addTrophyButton != null)
            addTrophyButton.onClick.RemoveListener(OnAddTrophyClicked);

        if (decreaseTrophyButton != null)
            decreaseTrophyButton.onClick.RemoveListener(OnDecreaseTrophyClicked);

        if (resetProgressButton != null)
            resetProgressButton.onClick.RemoveListener(OnResetProgressClicked);
    }

    public void OnAddTrophyClicked()
    {
        ResolveUserData();
        if (userData == null)
            return;

        int amount = Mathf.Max(1, addAmount);
        userData.AddTrophies(amount);
        Debug.Log($"[UserDataTrophyDebugButtons] +{amount} trophies. Total: {userData.Data.trophies}");
    }

    public void OnDecreaseTrophyClicked()
    {
        ResolveUserData();
        if (userData == null)
            return;

        int amount = Mathf.Max(1, decreaseAmount);
        int before = userData.Data.trophies;
        userData.SpendTrophies(amount);
        int delta = before - userData.Data.trophies;
        Debug.Log($"[UserDataTrophyDebugButtons] -{delta} trophies. Total: {userData.Data.trophies}");
    }

    public void OnResetProgressClicked()
    {
        ResolveUserData();
        if (userData == null)
            return;

        string preservedName = userData.Data != null ? userData.Data.playerName : "Player";
        userData.ResetProgressKeepName();

        int currentTrophies = userData.Data != null ? userData.Data.trophies : 0;
        Debug.Log($"[UserDataTrophyDebugButtons] Progress reset complete. Name kept: {preservedName}. Trophies: {currentTrophies}");
    }
}
