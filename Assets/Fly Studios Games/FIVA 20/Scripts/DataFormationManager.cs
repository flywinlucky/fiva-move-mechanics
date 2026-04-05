using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class FormationEntry
{
    public string formationName;
    public Sprite formationSprite;
}

public class DataFormationManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text formationName_Text;
    public Image formationImage;
    public Button prev_Formation_Button;
    public Button next_Formation_Button;

    [Header("Formation Data")]
    public List<FormationEntry> formations = new List<FormationEntry>();
    int currentFormationIndex;

    void Awake()
    {
        BindButtons();
    }

    void Start()
    {
        currentFormationIndex = Mathf.Clamp(currentFormationIndex, 0, Mathf.Max(0, formations.Count - 1));
        RefreshUI();
    }

    void OnDestroy()
    {
        UnbindButtons();
    }

    void BindButtons()
    {
        if (prev_Formation_Button != null)
        {
            prev_Formation_Button.onClick.RemoveListener(ShowPreviousFormation);
            prev_Formation_Button.onClick.AddListener(ShowPreviousFormation);
        }

        if (next_Formation_Button != null)
        {
            next_Formation_Button.onClick.RemoveListener(ShowNextFormation);
            next_Formation_Button.onClick.AddListener(ShowNextFormation);
        }
    }

    void UnbindButtons()
    {
        if (prev_Formation_Button != null)
            prev_Formation_Button.onClick.RemoveListener(ShowPreviousFormation);

        if (next_Formation_Button != null)
            next_Formation_Button.onClick.RemoveListener(ShowNextFormation);
    }

    public void ShowPreviousFormation()
    {
        if (formations == null || formations.Count == 0)
            return;

        currentFormationIndex--;
        if (currentFormationIndex < 0)
            currentFormationIndex = formations.Count - 1;

        RefreshUI();
    }

    public void ShowNextFormation()
    {
        if (formations == null || formations.Count == 0)
            return;

        currentFormationIndex++;
        if (currentFormationIndex >= formations.Count)
            currentFormationIndex = 0;

        RefreshUI();
    }

    public void SetFormationByIndex(int index)
    {
        if (formations == null || formations.Count == 0)
            return;

        currentFormationIndex = Mathf.Clamp(index, 0, formations.Count - 1);
        RefreshUI();
    }

    void RefreshUI()
    {
        bool hasData = formations != null && formations.Count > 0;

        if (!hasData)
        {
            if (formationName_Text != null)
                formationName_Text.text = string.Empty;

            if (formationImage != null)
                formationImage.sprite = null;

            if (prev_Formation_Button != null)
                prev_Formation_Button.interactable = false;

            if (next_Formation_Button != null)
                next_Formation_Button.interactable = false;

            return;
        }

        currentFormationIndex = Mathf.Clamp(currentFormationIndex, 0, formations.Count - 1);
        FormationEntry active = formations[currentFormationIndex];

        if (formationName_Text != null)
            formationName_Text.text = active != null ? active.formationName : string.Empty;

        if (formationImage != null)
            formationImage.sprite = active != null ? active.formationSprite : null;

        bool canNavigate = formations.Count > 1;
        if (prev_Formation_Button != null)
            prev_Formation_Button.interactable = canNavigate;

        if (next_Formation_Button != null)
            next_Formation_Button.interactable = canNavigate;
    }

    public string CurrentFormationName
    {
        get
        {
            if (formations == null || formations.Count == 0)
                return string.Empty;

            FormationEntry active = formations[Mathf.Clamp(currentFormationIndex, 0, formations.Count - 1)];
            return active != null ? active.formationName : string.Empty;
        }
    }

    public Sprite CurrentFormationSprite
    {
        get
        {
            if (formations == null || formations.Count == 0)
                return null;

            FormationEntry active = formations[Mathf.Clamp(currentFormationIndex, 0, formations.Count - 1)];
            return active != null ? active.formationSprite : null;
        }
    }
}
