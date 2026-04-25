using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class EmojyUserReactionUI : MonoBehaviour
{
    public bool isRealPlayerUI;
    public GameObject emojyPrefab;
    public TMP_Text emojyText;
    public Button openAndCloseChatButton;
    public GameObject closeChatIcon;

    private Coroutine hideRoutine;
    private bool hasPendingReaction;
    private string pendingReactionText = string.Empty;
    private float pendingReactionVisibleSeconds;

    private void Awake()
    {
        HideImmediate();
        SetChatToggleState(false);
    }

    private void OnEnable()
    {
        if (!hasPendingReaction)
            return;

        string queuedText = pendingReactionText;
        float queuedVisibleSeconds = pendingReactionVisibleSeconds;

        hasPendingReaction = false;
        pendingReactionText = string.Empty;
        pendingReactionVisibleSeconds = 0f;

        ShowReaction(queuedText, queuedVisibleSeconds);
    }

    private void OnDisable()
    {
        if (hideRoutine == null)
            return;

        StopCoroutine(hideRoutine);
        hideRoutine = null;
    }

    public void ShowReaction(string reactionText, float visibleSeconds = 3f)
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            hasPendingReaction = true;
            pendingReactionText = reactionText;
            pendingReactionVisibleSeconds = Mathf.Max(0f, visibleSeconds);
            return;
        }

        if (emojyText != null)
            emojyText.text = reactionText;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        if (emojyPrefab != null)
            emojyPrefab.SetActive(true);

        if (visibleSeconds > 0f)
            hideRoutine = StartCoroutine(HideAfterDelay(visibleSeconds));
    }

    public void HideImmediate()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (emojyPrefab != null)
            emojyPrefab.SetActive(false);
    }

    public void SetChatToggleState(bool isOpen)
    {
        if (closeChatIcon != null)
            closeChatIcon.SetActive(isOpen);
    }

    public void OnChatToggleButtonPressed()
    {
        EmojySystem resolvedSystem = FindObjectOfType<EmojySystem>();
        if (resolvedSystem == null)
        {
            Debug.LogWarning("EmojySystem is missing from scene.", this);
            return;
        }

        resolvedSystem.OnChatToggleButtonPressed();
    }

    private IEnumerator HideAfterDelay(float visibleSeconds)
    {
        yield return new WaitForSeconds(visibleSeconds);
        hideRoutine = null;

        if (emojyPrefab != null)
            emojyPrefab.SetActive(false);
    }
}
