using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public enum EmojyMessageType
{
    GoodLuck,
    WellPlayed,
    NiceMove,
    Thanks,
    Sorry,
    Wow,
    GoodGame,
    Oops
}

[Serializable]
public class EmojyButtonBinding
{
    public Button button;
    public EmojyMessageType emojyMessage = EmojyMessageType.GoodLuck;
}

public class EmojySystem : MonoBehaviour
{
    public enum BotProfile
    {
        Randomized,
        Balanced,
        Pro,
        Troll,
        Silent,
        Noob
    }

    private enum ReactionEventType
    {
        MatchStart,
        PlayerStrongMove,
        PlayerMistake,
        OpponentStrongMove,
        OpponentMistake,
        PlayerPass,
        OpponentPass,
        MatchEnd,
        CounterChat
    }

    private static readonly EmojyMessageType[] ProMessages =
    {
        EmojyMessageType.GoodLuck,
        EmojyMessageType.WellPlayed,
        EmojyMessageType.GoodGame
    };

    private static readonly EmojyMessageType[] TrollMessages =
    {
        EmojyMessageType.Thanks,
        EmojyMessageType.Oops,
        EmojyMessageType.NiceMove
    };

    private static readonly EmojyMessageType[] NoobMessages =
    {
        EmojyMessageType.Wow,
        EmojyMessageType.Sorry,
        EmojyMessageType.Oops
    };

    private sealed class RuntimeButtonListener
    {
        public Button button;
        public UnityAction action;
    }

    private struct ActiveProfileSettings
    {
        public BotProfile profile;
        public float reactionMultiplier;
        public float tauntBias;
        public float apologyBias;
        public float praiseBias;
    }

    private static readonly EmojyMessageType[] AllEmojiMessages =
    {
        EmojyMessageType.GoodLuck,
        EmojyMessageType.WellPlayed,
        EmojyMessageType.NiceMove,
        EmojyMessageType.Thanks,
        EmojyMessageType.Sorry,
        EmojyMessageType.Wow,
        EmojyMessageType.GoodGame,
        EmojyMessageType.Oops
    };

    [Header("Player Buttons")]
    public List<EmojyButtonBinding> playerEmojiButtons = new List<EmojyButtonBinding>();

    [Header("Chat Panel")]
    public GameObject emojyPrefabPanel;

    [Header("Reaction UIs")]
    public EmojyUserReactionUI player1ReactionUI;
    public EmojyUserReactionUI oponentReactionUI;

    [Header("Bot Behaviour")]
    public BotProfile botProfile = BotProfile.Randomized;
    [Range(0f, 1f)] public float baseReactionChance = 0.35f;
    [Range(0f, 1f)] public float counterChatChance = 0.6f;
    public Vector2 reactionDelayRange = new Vector2(1.2f, 3f);
    public float reactionVisibleSeconds = 3f;
    public float reactionCooldownSeconds = 4f;
    public float tiltMuteSeconds = 30f;
    [Min(1)] public int tiltEventsBeforeMute = 3;
    public bool bindPlayerButtonsOnEnable = true;

    [Header("Football Context")]
    [Range(0f, 1f)] public float goalReactionChance = 0.72f;
    [Range(0f, 1f)] public float possessionSwingReactionChance = 0.26f;
    [Min(0.5f)] public float possessionReactionMinInterval = 6f;
    [Range(0f, 1f)] public float comebackReactionBonus = 0.18f;

    private readonly List<RuntimeButtonListener> runtimeButtonListeners = new List<RuntimeButtonListener>();
    private static readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>(16);
    private Coroutine botReactionCoroutine;
    private ActiveProfileSettings activeProfile;
    private float botMutedUntil = -1f;
    private float lastBotReactionTime = -999f;
    private int botNegativeEventStreak;
    private int queuedReactionPriority = int.MinValue;
    private int playerEmojiSpamCount;
    private int emojiPanelOpenedFrame = -1;
    private float lastPlayerEmojiTime = -999f;
    private bool botMatchActive;
    private EmojyMessageType? lastBotReaction;
    private float matchStartedTime = -1f;
    private int trackedPlayerGoals;
    private int trackedOpponentGoals;
    private bool hasTrackedScore;
    private bool hasTrackedPossession;
    private bool lastKnownPlayerPossession;
    private float lastPossessionReactionTime = -999f;
    private float profileMood;

    private void OnEnable()
    {
        if (bindPlayerButtonsOnEnable)
            RegisterPlayerButtons();

        SetEmojiPanelOpen(false);

        if (player1ReactionUI != null)
            player1ReactionUI.HideImmediate();

        if (oponentReactionUI != null)
            oponentReactionUI.HideImmediate();
    }

    private void OnDisable()
    {
        UnregisterPlayerButtons();

        if (botReactionCoroutine != null)
        {
            StopCoroutine(botReactionCoroutine);
            botReactionCoroutine = null;
        }

        queuedReactionPriority = int.MinValue;
        hasTrackedScore = false;
        hasTrackedPossession = false;
    }

    private void LateUpdate()
    {
        if (!IsEmojiPanelOpen())
            return;

        if (Time.frameCount == emojiPanelOpenedFrame)
            return;

        if (ShouldClosePanelThisFrame())
            SetEmojiPanelOpen(false);
    }

    public void ToggleEmojiPanel()
    {
        SetEmojiPanelOpen(!IsEmojiPanelOpen());
    }

    public void OnChatToggleButtonPressed()
    {
        ToggleEmojiPanel();
    }

    public void SetEmojiPanelOpen(bool isOpen)
    {
        if (emojyPrefabPanel != null)
            emojyPrefabPanel.SetActive(isOpen);

        emojiPanelOpenedFrame = isOpen ? Time.frameCount : -1;

        SyncChatToggleState(isOpen);
    }

    public void NotifyMatchStarted(bool againstBot, int playerGoals = 0, int opponentGoals = 0)
    {
        botMatchActive = againstBot;
        playerEmojiSpamCount = 0;
        botNegativeEventStreak = 0;
        botMutedUntil = -1f;
        lastBotReactionTime = -999f;
        lastBotReaction = null;
        ChooseBotProfile();

        matchStartedTime = Time.time;
        trackedPlayerGoals = Mathf.Max(0, playerGoals);
        trackedOpponentGoals = Mathf.Max(0, opponentGoals);
        hasTrackedScore = true;
        hasTrackedPossession = false;
        lastKnownPlayerPossession = false;
        lastPossessionReactionTime = -999f;
        profileMood = GetInitialMoodForProfile(activeProfile.profile);

        if (!botMatchActive)
            return;

        EmojyMessageType? message = SelectMessageForEvent(ReactionEventType.MatchStart, 0, 0, false, false, null);
        QueueBotReaction(message, baseReactionChance + 0.20f, 10);
    }

    public void NotifyGoalScored(bool playerScored, int playerGoals, int opponentGoals)
    {
        if (!botMatchActive)
            return;

        int safePlayerGoals = Mathf.Max(0, playerGoals);
        int safeOpponentGoals = Mathf.Max(0, opponentGoals);

        if (!hasTrackedScore)
        {
            trackedPlayerGoals = safePlayerGoals;
            trackedOpponentGoals = safeOpponentGoals;
            hasTrackedScore = true;
        }

        int beforeDiff = trackedPlayerGoals - trackedOpponentGoals;
        int afterDiff = safePlayerGoals - safeOpponentGoals;

        bool equalizer = beforeDiff != 0 && afterDiff == 0;
        bool leadSwing = (beforeDiff <= 0 && afterDiff > 0) || (beforeDiff >= 0 && afterDiff < 0);
        bool scoreGapGrowing = Mathf.Abs(afterDiff) >= 2 && Mathf.Abs(afterDiff) > Mathf.Abs(beforeDiff);
        bool openingGoal = safePlayerGoals + safeOpponentGoals <= 1;
        bool lateMoment = IsLateMatchMoment();

        AdjustMoodAfterGoal(playerScored, afterDiff);

        EmojyMessageType? goalMessage = SelectGoalReactionMessage(playerScored, afterDiff, equalizer, leadSwing, lateMoment, scoreGapGrowing);

        float eventChance = goalReactionChance + (playerScored ? 0.05f : 0.10f);
        if (equalizer)
            eventChance += 0.12f;
        if (leadSwing)
            eventChance += comebackReactionBonus;
        if (scoreGapGrowing)
            eventChance += 0.08f;
        if (openingGoal)
            eventChance -= 0.08f;
        if (lateMoment)
            eventChance += 0.08f;

        eventChance += Mathf.Clamp(profileMood * 0.08f, -0.05f, 0.08f);

        int priority = playerScored ? 82 : 88;
        QueueBotReaction(goalMessage, eventChance, priority, true);

        trackedPlayerGoals = safePlayerGoals;
        trackedOpponentGoals = safeOpponentGoals;
    }

    public void NotifyPossessionChanged(bool playerHasPossession)
    {
        if (!botMatchActive)
            return;

        if (!hasTrackedPossession)
        {
            hasTrackedPossession = true;
            lastKnownPlayerPossession = playerHasPossession;
            return;
        }

        if (lastKnownPlayerPossession == playerHasPossession)
            return;

        lastKnownPlayerPossession = playerHasPossession;

        if (Time.time - lastPossessionReactionTime < Mathf.Max(0.5f, possessionReactionMinInterval))
            return;

        lastPossessionReactionTime = Time.time;

        if (playerHasPossession)
        {
            RegisterBotNegativeEvent();
            float chance = possessionSwingReactionChance + (activeProfile.apologyBias * 0.12f);
            QueueBotReaction(SelectMessageForEvent(ReactionEventType.OpponentMistake, 0, 0, false, false, null), chance, 28);
            return;
        }

        ClearBotNegativeEventStreak();
        float tauntBonus = GetSpamTauntBonus() + (activeProfile.tauntBias * 0.10f);
        QueueBotReaction(SelectMessageForEvent(ReactionEventType.PlayerMistake, 0, 0, false, false, null), possessionSwingReactionChance + tauntBonus, 32);
    }

    public void NotifyPlayerMoveResolved(int moveScore, int capturedCount)
    {
        if (!botMatchActive)
            return;

        bool spectacularMove = capturedCount >= 4 || moveScore >= 500;
        bool strongMove = capturedCount >= 1 || moveScore >= 180;
        bool weakMove = capturedCount == 0 && moveScore <= 0;

        if (spectacularMove)
        {
            RegisterBotNegativeEvent();
            QueueBotReaction(SelectMessageForEvent(ReactionEventType.PlayerStrongMove, moveScore, capturedCount, true, false, null), baseReactionChance + 0.50f, 60);
            return;
        }

        if (strongMove)
        {
            RegisterBotNegativeEvent();
            QueueBotReaction(SelectMessageForEvent(ReactionEventType.PlayerStrongMove, moveScore, capturedCount, false, false, null), baseReactionChance + 0.20f, 40);
            return;
        }

        if (weakMove && UnityEngine.Random.value < 0.2f)
        {
            ClearBotNegativeEventStreak();
            QueueBotReaction(SelectMessageForEvent(ReactionEventType.PlayerMistake, moveScore, capturedCount, false, false, null), baseReactionChance + 0.05f + GetSpamTauntBonus(), 25);
        }
    }

    public void NotifyOpponentMoveResolved(int moveScore, int capturedCount)
    {
        if (!botMatchActive)
            return;

        bool spectacularMove = capturedCount >= 4 || moveScore >= 500;
        bool strongMove = capturedCount >= 1 || moveScore >= 180;
        bool weakMove = capturedCount == 0 && moveScore <= 0;

        if (spectacularMove)
        {
            ClearBotNegativeEventStreak();
            QueueBotReaction(SelectMessageForEvent(ReactionEventType.OpponentStrongMove, moveScore, capturedCount, true, false, null), baseReactionChance + 0.35f, 45);
            return;
        }

        if (strongMove)
        {
            ClearBotNegativeEventStreak();
            QueueBotReaction(SelectMessageForEvent(ReactionEventType.OpponentStrongMove, moveScore, capturedCount, false, false, null), baseReactionChance + 0.10f, 30);
            return;
        }

        if (weakMove && UnityEngine.Random.value < 0.3f)
        {
            RegisterBotNegativeEvent();
            QueueBotReaction(SelectMessageForEvent(ReactionEventType.OpponentMistake, moveScore, capturedCount, false, false, null), baseReactionChance + 0.15f, 30);
        }
    }

    public void NotifyPlayerPassed()
    {
        if (!botMatchActive)
            return;

        ClearBotNegativeEventStreak();
        QueueBotReaction(SelectMessageForEvent(ReactionEventType.PlayerPass, 0, 0, false, false, null), baseReactionChance + 0.10f + GetSpamTauntBonus(), 35);
    }

    public void NotifyOpponentPassed()
    {
        if (!botMatchActive)
            return;

        RegisterBotNegativeEvent();
        QueueBotReaction(SelectMessageForEvent(ReactionEventType.OpponentPass, 0, 0, false, false, null), baseReactionChance + 0.20f, 45);
    }

    public void NotifyGameEnded(bool playerWon, bool isDraw)
    {
        if (!botMatchActive)
            return;

        EmojyMessageType? message = SelectMessageForEvent(ReactionEventType.MatchEnd, 0, 0, false, playerWon, null);
        if (isDraw)
            message = PickMessage(EmojyMessageType.GoodGame, EmojyMessageType.WellPlayed, EmojyMessageType.Wow);

        QueueBotReaction(message, baseReactionChance + 0.55f, 100, true);
    }

    public void SendPlayerEmojiByIndex(int emojiIndex)
    {
        if (emojiIndex < 0 || emojiIndex >= AllEmojiMessages.Length)
            return;

        SendPlayerEmoji(AllEmojiMessages[emojiIndex]);
    }

    public void SendPlayerEmoji(EmojyMessageType emojiMessage)
    {
        string emojiText = GetMessageText(emojiMessage);

        if (player1ReactionUI != null)
            player1ReactionUI.ShowReaction(emojiText, reactionVisibleSeconds);

        TrackPlayerEmojiUsage();
        SetEmojiPanelOpen(false);

        if (!botMatchActive)
            return;

        if (UnityEngine.Random.value > counterChatChance)
            return;

        EmojyMessageType? counterMessage = SelectMessageForEvent(ReactionEventType.CounterChat, 0, 0, false, false, emojiText);
        QueueBotReaction(counterMessage, counterChatChance, 70, true);
    }

    public void SendPlayerEmoji(string emojiText)
    {
        if (TryGetMessageType(emojiText, out EmojyMessageType emojiMessage))
            SendPlayerEmoji(emojiMessage);
    }

    private void RegisterPlayerButtons()
    {
        UnregisterPlayerButtons();

        for (int i = 0; i < playerEmojiButtons.Count; i++)
        {
            EmojyButtonBinding binding = playerEmojiButtons[i];
            if (binding == null || binding.button == null)
                continue;

            EmojyMessageType capturedMessage = binding.emojyMessage;
            UnityAction action = () => SendPlayerEmoji(capturedMessage);
            binding.button.onClick.AddListener(action);
            runtimeButtonListeners.Add(new RuntimeButtonListener
            {
                button = binding.button,
                action = action
            });
        }
    }

    private void UnregisterPlayerButtons()
    {
        for (int i = 0; i < runtimeButtonListeners.Count; i++)
        {
            RuntimeButtonListener listener = runtimeButtonListeners[i];
            if (listener.button != null && listener.action != null)
                listener.button.onClick.RemoveListener(listener.action);
        }

        runtimeButtonListeners.Clear();
    }

    private bool IsEmojiPanelOpen()
    {
        return emojyPrefabPanel != null && emojyPrefabPanel.activeSelf;
    }

    private bool ShouldClosePanelThisFrame()
    {
        if (Input.GetMouseButtonUp(0))
            return !ShouldIgnoreCloseFromPointer(Input.mousePosition);

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            TouchPhase phase = touch.phase;
            if (phase != TouchPhase.Ended && phase != TouchPhase.Canceled)
                continue;

            if (!ShouldIgnoreCloseFromPointer(touch.position))
                return true;
        }

        return false;
    }

    private bool ShouldIgnoreCloseFromPointer(Vector2 screenPosition)
    {
        if (IsPointerOverUiObject(screenPosition, emojyPrefabPanel))
            return true;

        if (player1ReactionUI != null &&
            player1ReactionUI.openAndCloseChatButton != null &&
            IsPointerOverUiObject(screenPosition, player1ReactionUI.openAndCloseChatButton.gameObject))
        {
            return true;
        }

        if (oponentReactionUI != null &&
            oponentReactionUI.openAndCloseChatButton != null &&
            IsPointerOverUiObject(screenPosition, oponentReactionUI.openAndCloseChatButton.gameObject))
        {
            return true;
        }

        return false;
    }

    private static bool IsPointerOverUiObject(Vector2 screenPosition, GameObject targetObject)
    {
        if (targetObject == null || EventSystem.current == null)
            return false;

        uiRaycastResults.Clear();
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };
        EventSystem.current.RaycastAll(pointerData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject hitObject = uiRaycastResults[i].gameObject;
            if (hitObject == null)
                continue;

            if (hitObject == targetObject || hitObject.transform.IsChildOf(targetObject.transform))
                return true;
        }

        return false;
    }

    private void SyncChatToggleState(bool isOpen)
    {
        if (player1ReactionUI != null)
            player1ReactionUI.SetChatToggleState(isOpen);

        if (oponentReactionUI != null)
            oponentReactionUI.SetChatToggleState(isOpen);
    }

    private void ChooseBotProfile()
    {
        BotProfile selectedProfile = botProfile;
        if (selectedProfile == BotProfile.Randomized)
        {
            float roll = UnityEngine.Random.value;
            if (roll < 0.30f) selectedProfile = BotProfile.Balanced;
            else if (roll < 0.50f) selectedProfile = BotProfile.Pro;
            else if (roll < 0.72f) selectedProfile = BotProfile.Troll;
            else if (roll < 0.84f) selectedProfile = BotProfile.Silent;
            else selectedProfile = BotProfile.Noob;
        }

        activeProfile = CreateProfileSettings(selectedProfile);
    }

    private ActiveProfileSettings CreateProfileSettings(BotProfile profile)
    {
        ActiveProfileSettings settings = new ActiveProfileSettings
        {
            profile = profile,
            reactionMultiplier = 1f,
            tauntBias = 0.5f,
            apologyBias = 0.5f,
            praiseBias = 0.5f
        };

        switch (profile)
        {
            case BotProfile.Pro:
                settings.reactionMultiplier = 0.7f;
                settings.tauntBias = 0.15f;
                settings.apologyBias = 0.2f;
                settings.praiseBias = 0.95f;
                break;
            case BotProfile.Troll:
                settings.reactionMultiplier = 1.1f;
                settings.tauntBias = 0.95f;
                settings.apologyBias = 0.2f;
                settings.praiseBias = 0.35f;
                break;
            case BotProfile.Silent:
                settings.reactionMultiplier = 0f;
                settings.tauntBias = 0f;
                settings.apologyBias = 0f;
                settings.praiseBias = 0f;
                break;
            case BotProfile.Noob:
                settings.reactionMultiplier = 1.25f;
                settings.tauntBias = 0.4f;
                settings.apologyBias = 0.85f;
                settings.praiseBias = 0.8f;
                break;
            case BotProfile.Balanced:
                settings.reactionMultiplier = 1f;
                settings.tauntBias = 0.55f;
                settings.apologyBias = 0.45f;
                settings.praiseBias = 0.75f;
                break;
        }

        return settings;
    }

    private void TrackPlayerEmojiUsage()
    {
        if (Time.time - lastPlayerEmojiTime <= 8f)
            playerEmojiSpamCount++;
        else
            playerEmojiSpamCount = 1;

        lastPlayerEmojiTime = Time.time;
    }

    private void RegisterBotNegativeEvent()
    {
        int threshold = tiltEventsBeforeMute;
        if (activeProfile.profile == BotProfile.Noob)
            threshold = Mathf.Max(1, threshold - 1);
        else if (activeProfile.profile == BotProfile.Pro || activeProfile.profile == BotProfile.Troll)
            threshold += 1;

        botNegativeEventStreak++;
        if (botNegativeEventStreak < threshold)
            return;

        botNegativeEventStreak = 0;
        botMutedUntil = Time.time + tiltMuteSeconds;

        if (botReactionCoroutine != null)
        {
            StopCoroutine(botReactionCoroutine);
            botReactionCoroutine = null;
            queuedReactionPriority = int.MinValue;
        }
    }

    private void ClearBotNegativeEventStreak()
    {
        botNegativeEventStreak = 0;
    }

    private float GetInitialMoodForProfile(BotProfile profile)
    {
        switch (profile)
        {
            case BotProfile.Pro:
                return -0.1f;
            case BotProfile.Troll:
                return 0.25f;
            case BotProfile.Noob:
                return 0.05f;
            case BotProfile.Silent:
                return 0f;
            default:
                return 0.1f;
        }
    }

    private bool IsLateMatchMoment()
    {
        if (matchStartedTime < 0f)
            return false;

        return Time.time - matchStartedTime >= 95f;
    }

    private void AdjustMoodAfterGoal(bool playerScored, int scoreDiffAfterGoal)
    {
        float pressure = Mathf.Clamp01(Mathf.Abs(scoreDiffAfterGoal) / 3f);
        float swing = 0.16f + (pressure * 0.08f);

        if (playerScored)
            profileMood -= swing;
        else
            profileMood += swing;

        profileMood = Mathf.Clamp(profileMood, -1f, 1f);
    }

    private EmojyMessageType? SelectGoalReactionMessage(bool playerScored, int scoreDiffAfterGoal, bool equalizer, bool leadSwing, bool lateMoment, bool scoreGapGrowing)
    {
        if (playerScored)
        {
            if (activeProfile.profile == BotProfile.Pro)
                return equalizer || leadSwing
                    ? PickMessage(EmojyMessageType.WellPlayed, EmojyMessageType.Wow)
                    : PickMessage(EmojyMessageType.WellPlayed, EmojyMessageType.NiceMove);

            if (activeProfile.profile == BotProfile.Noob)
                return scoreDiffAfterGoal <= -2
                    ? PickMessage(EmojyMessageType.Sorry, EmojyMessageType.Wow, EmojyMessageType.Oops)
                    : PickMessage(EmojyMessageType.Wow, EmojyMessageType.Sorry, EmojyMessageType.WellPlayed);

            if (activeProfile.profile == BotProfile.Troll)
            {
                if (scoreDiffAfterGoal >= 1 && !equalizer)
                    return PickMessage(EmojyMessageType.Thanks, EmojyMessageType.Oops, EmojyMessageType.NiceMove);
                if (scoreDiffAfterGoal <= -2 || leadSwing)
                    return PickMessage(EmojyMessageType.Oops, EmojyMessageType.Wow, EmojyMessageType.Sorry);
                return PickMessage(EmojyMessageType.Oops, EmojyMessageType.Thanks, EmojyMessageType.WellPlayed);
            }

            if (scoreGapGrowing || leadSwing)
                return PickMessage(EmojyMessageType.Wow, EmojyMessageType.WellPlayed, EmojyMessageType.Oops);

            return activeProfile.praiseBias >= activeProfile.tauntBias
                ? PickMessage(EmojyMessageType.WellPlayed, EmojyMessageType.NiceMove, EmojyMessageType.Wow)
                : PickMessage(EmojyMessageType.Oops, EmojyMessageType.Thanks, EmojyMessageType.WellPlayed);
        }

        if (activeProfile.profile == BotProfile.Pro)
            return lateMoment
                ? PickMessage(EmojyMessageType.GoodGame, EmojyMessageType.WellPlayed)
                : PickMessage(EmojyMessageType.WellPlayed, EmojyMessageType.GoodGame);

        if (activeProfile.profile == BotProfile.Noob)
            return scoreDiffAfterGoal >= 2
                ? PickMessage(EmojyMessageType.Wow, EmojyMessageType.Thanks, EmojyMessageType.Oops)
                : PickMessage(EmojyMessageType.Thanks, EmojyMessageType.Wow, EmojyMessageType.GoodGame);

        if (activeProfile.profile == BotProfile.Troll)
        {
            if (scoreDiffAfterGoal >= 2 || scoreGapGrowing)
                return PickMessage(EmojyMessageType.Thanks, EmojyMessageType.Oops, EmojyMessageType.GoodGame);

            if (equalizer || leadSwing)
                return PickMessage(EmojyMessageType.Thanks, EmojyMessageType.Wow, EmojyMessageType.Oops);

            return PickMessage(EmojyMessageType.Thanks, EmojyMessageType.Oops, EmojyMessageType.NiceMove);
        }

        if (equalizer || leadSwing)
            return PickMessage(EmojyMessageType.Thanks, EmojyMessageType.WellPlayed, EmojyMessageType.Wow);

        if (scoreDiffAfterGoal >= 2)
            return PickMessage(EmojyMessageType.Thanks, EmojyMessageType.GoodGame, EmojyMessageType.Oops);

        return PickMessage(EmojyMessageType.Thanks, EmojyMessageType.WellPlayed, EmojyMessageType.GoodGame);
    }

    private float GetSpamTauntBonus()
    {
        if (playerEmojiSpamCount >= 3)
            return 0.15f;

        return 0f;
    }

    private void QueueBotReaction(EmojyMessageType? reactionType, float eventChance, int priority, bool ignoreCooldown = false)
    {
        if (!reactionType.HasValue)
            return;

        if (!botMatchActive || activeProfile.profile == BotProfile.Silent)
            return;

        if (Time.time < botMutedUntil)
            return;

        float adjustedChance = Mathf.Clamp01(eventChance * activeProfile.reactionMultiplier);
        if (UnityEngine.Random.value > adjustedChance)
            return;

        if (!ignoreCooldown && Time.time - lastBotReactionTime < reactionCooldownSeconds)
            return;

        if (botReactionCoroutine != null)
        {
            if (priority <= queuedReactionPriority)
                return;

            StopCoroutine(botReactionCoroutine);
        }

        botReactionCoroutine = StartCoroutine(ShowBotReactionAfterDelay(reactionType.Value, priority));
    }

    private IEnumerator ShowBotReactionAfterDelay(EmojyMessageType reactionType, int priority)
    {
        queuedReactionPriority = priority;
        float minDelay = Mathf.Max(0f, reactionDelayRange.x);
        float maxDelay = Mathf.Max(minDelay, reactionDelayRange.y);
        yield return new WaitForSeconds(UnityEngine.Random.Range(minDelay, maxDelay));

        if (oponentReactionUI != null)
            oponentReactionUI.ShowReaction(GetMessageText(reactionType), reactionVisibleSeconds);

        lastBotReactionTime = Time.time;
        lastBotReaction = reactionType;
        botReactionCoroutine = null;
        queuedReactionPriority = int.MinValue;
    }

    private EmojyMessageType? SelectMessageForEvent(ReactionEventType eventType, int moveScore, int capturedCount, bool spectacular, bool playerWon, string playerMessage)
    {
        switch (eventType)
        {
            case ReactionEventType.MatchStart:
                if (activeProfile.profile == BotProfile.Troll && UnityEngine.Random.value < 0.35f)
                    return PickMessage(EmojyMessageType.GoodLuck, EmojyMessageType.Thanks);
                if (activeProfile.profile == BotProfile.Silent)
                    return null;
                return PickMessage(EmojyMessageType.GoodLuck);

            case ReactionEventType.PlayerStrongMove:
                if (activeProfile.profile == BotProfile.Noob)
                    return spectacular ? PickMessage(EmojyMessageType.Wow, EmojyMessageType.WellPlayed) : PickMessage(EmojyMessageType.Wow, EmojyMessageType.NiceMove);
                return spectacular ? PickMessage(EmojyMessageType.Wow, EmojyMessageType.WellPlayed, EmojyMessageType.NiceMove) : PickMessage(EmojyMessageType.WellPlayed, EmojyMessageType.NiceMove);

            case ReactionEventType.PlayerMistake:
                if (activeProfile.profile == BotProfile.Pro)
                    return UnityEngine.Random.value < 0.7f ? null : PickMessage(EmojyMessageType.Oops);
                if (activeProfile.profile == BotProfile.Noob)
                    return PickMessage(EmojyMessageType.Wow, EmojyMessageType.Oops);
                return activeProfile.tauntBias > 0.7f
                    ? PickMessage(EmojyMessageType.Thanks, EmojyMessageType.NiceMove, EmojyMessageType.Oops)
                    : PickMessage(EmojyMessageType.Oops, EmojyMessageType.Thanks);

            case ReactionEventType.OpponentStrongMove:
                if (spectacular)
                {
                    if (activeProfile.profile == BotProfile.Troll)
                        return PickMessage(EmojyMessageType.Thanks, EmojyMessageType.Wow, EmojyMessageType.Oops);
                    if (activeProfile.profile == BotProfile.Pro)
                        return PickMessage(EmojyMessageType.WellPlayed, EmojyMessageType.GoodGame);
                    return PickMessage(EmojyMessageType.Thanks, EmojyMessageType.Wow, EmojyMessageType.WellPlayed);
                }

                if (activeProfile.profile == BotProfile.Pro)
                    return PickMessage(EmojyMessageType.WellPlayed, EmojyMessageType.Thanks);
                if (activeProfile.profile == BotProfile.Noob)
                    return PickMessage(EmojyMessageType.Wow, EmojyMessageType.Thanks);
                return activeProfile.tauntBias > 0.7f
                    ? PickMessage(EmojyMessageType.Thanks, EmojyMessageType.Oops, EmojyMessageType.WellPlayed)
                    : PickMessage(EmojyMessageType.Thanks, EmojyMessageType.WellPlayed);

            case ReactionEventType.OpponentMistake:
                if (activeProfile.apologyBias > 0.6f)
                    return PickMessage(EmojyMessageType.Sorry, EmojyMessageType.Oops);
                return PickMessage(EmojyMessageType.Oops, EmojyMessageType.Sorry);

            case ReactionEventType.PlayerPass:
                if (activeProfile.profile == BotProfile.Pro)
                    return null;
                return activeProfile.tauntBias > 0.7f
                    ? PickMessage(EmojyMessageType.Thanks, EmojyMessageType.Oops)
                    : PickMessage(EmojyMessageType.Oops, EmojyMessageType.NiceMove);

            case ReactionEventType.OpponentPass:
                if (activeProfile.profile == BotProfile.Troll && UnityEngine.Random.value < 0.5f)
                    return null;
                return PickMessage(EmojyMessageType.Sorry, EmojyMessageType.Oops);

            case ReactionEventType.MatchEnd:
                if (playerWon)
                {
                    if (activeProfile.profile == BotProfile.Troll && UnityEngine.Random.value < 0.4f)
                        return PickMessage(EmojyMessageType.GoodGame, EmojyMessageType.Oops);
                    return PickMessage(EmojyMessageType.GoodGame, EmojyMessageType.WellPlayed);
                }

                if (activeProfile.profile == BotProfile.Pro)
                    return PickMessage(EmojyMessageType.GoodGame, EmojyMessageType.WellPlayed);
                if (activeProfile.profile == BotProfile.Noob)
                    return PickMessage(EmojyMessageType.Wow, EmojyMessageType.GoodGame);
                return activeProfile.tauntBias > 0.7f
                    ? PickMessage(EmojyMessageType.Thanks, EmojyMessageType.GoodGame, EmojyMessageType.Oops)
                    : PickMessage(EmojyMessageType.GoodGame, EmojyMessageType.WellPlayed, EmojyMessageType.Thanks);

            case ReactionEventType.CounterChat:
                return SelectCounterMessage(playerMessage);
        }

        return null;
    }

    private EmojyMessageType? SelectCounterMessage(string playerMessage)
    {
        if (!TryGetMessageType(playerMessage, out EmojyMessageType playerEmojiMessage))
            return PickMessage(EmojyMessageType.Thanks, EmojyMessageType.WellPlayed, EmojyMessageType.Wow);

        switch (playerEmojiMessage)
        {
            case EmojyMessageType.GoodLuck:
                return activeProfile.profile == BotProfile.Troll ? PickMessage(EmojyMessageType.Thanks, EmojyMessageType.GoodLuck) : PickMessage(EmojyMessageType.GoodLuck);
            case EmojyMessageType.WellPlayed:
            case EmojyMessageType.NiceMove:
                return PickMessage(EmojyMessageType.Thanks, EmojyMessageType.WellPlayed);
            case EmojyMessageType.Thanks:
                if (activeProfile.tauntBias > 0.7f)
                    return PickMessage(EmojyMessageType.Oops, EmojyMessageType.Thanks);
                return PickMessage(EmojyMessageType.Thanks, EmojyMessageType.WellPlayed);
            case EmojyMessageType.Sorry:
                return activeProfile.tauntBias > 0.7f ? PickMessage(EmojyMessageType.Thanks, EmojyMessageType.Oops) : PickMessage(EmojyMessageType.WellPlayed, EmojyMessageType.Sorry);
            case EmojyMessageType.Wow:
                return PickMessage(EmojyMessageType.Wow, EmojyMessageType.Thanks);
            case EmojyMessageType.GoodGame:
                return PickMessage(EmojyMessageType.GoodGame, EmojyMessageType.WellPlayed);
            case EmojyMessageType.Oops:
                return activeProfile.tauntBias > 0.7f ? PickMessage(EmojyMessageType.Thanks, EmojyMessageType.Oops) : PickMessage(EmojyMessageType.Oops, EmojyMessageType.Sorry);
        }

        return PickMessage(EmojyMessageType.Thanks, EmojyMessageType.WellPlayed, EmojyMessageType.Wow);
    }

    private EmojyMessageType? PickMessage(params EmojyMessageType[] desiredMessages)
    {
        List<EmojyMessageType> candidates = new List<EmojyMessageType>();
        for (int i = 0; i < desiredMessages.Length; i++)
        {
            if (!candidates.Contains(desiredMessages[i]))
                candidates.Add(desiredMessages[i]);
        }

        if (candidates.Count == 0)
            return null;

        ApplyProfileMessageFilter(candidates);

        if (candidates.Count == 0)
            return null;

        if (candidates.Count > 1 && lastBotReaction.HasValue)
            candidates.RemoveAll(candidate => candidate == lastBotReaction.Value);

        if (candidates.Count == 0)
            return desiredMessages[0];

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void ApplyProfileMessageFilter(List<EmojyMessageType> candidates)
    {
        switch (activeProfile.profile)
        {
            case BotProfile.Pro:
                FilterCandidates(candidates, ProMessages);
                break;
            case BotProfile.Troll:
                FilterCandidates(candidates, TrollMessages);
                break;
            case BotProfile.Silent:
                candidates.Clear();
                break;
            case BotProfile.Noob:
                FilterCandidates(candidates, NoobMessages);
                break;
        }
    }

    private static void FilterCandidates(List<EmojyMessageType> candidates, EmojyMessageType[] allowedMessages)
    {
        candidates.RemoveAll(candidate => Array.IndexOf(allowedMessages, candidate) < 0);
    }

    public static string GetMessageMeaning(EmojyMessageType messageType)
    {
        switch (messageType)
        {
            case EmojyMessageType.GoodLuck:
                return "Salut standard de inceput sau o usoara intimidare politicoasa.";
            case EmojyMessageType.WellPlayed:
                return "Recunoastere autentica pentru o mutare buna sau un final respectuos.";
            case EmojyMessageType.NiceMove:
                return "Poate fi lauda sincera sau ironie fina cand adversarul greseste.";
            case EmojyMessageType.Thanks:
                return "Triumf, provocare sau multumire agresiva dupa avantaj.";
            case EmojyMessageType.Sorry:
                return "Regret aparent, umanizare sau ironie subtila dupa noroc.";
            case EmojyMessageType.Wow:
                return "Surpriza reala la o faza rara, spectaculoasa sau neasteptata.";
            case EmojyMessageType.GoodGame:
                return "Incheiere de meci, sportivitate sau calm dupa rezultat.";
            case EmojyMessageType.Oops:
                return "Sarcasm, superioritate sau reactie la o greseala evidenta.";
            default:
                return string.Empty;
        }
    }

    public static string GetMessageUseCase(EmojyMessageType messageType)
    {
        switch (messageType)
        {
            case EmojyMessageType.GoodLuck:
                return "Start meci";
            case EmojyMessageType.WellPlayed:
                return "Jucatorul sau botul face o mutare buna";
            case EmojyMessageType.NiceMove:
                return "Faza buna sau ironie dupa o greseala simpla";
            case EmojyMessageType.Thanks:
                return "Botul castiga avantaj sau raspunde provocator";
            case EmojyMessageType.Sorry:
                return "Botul greseste sau castiga cu noroc";
            case EmojyMessageType.Wow:
                return "Captura mare, faza rara, rasturnare de situatie";
            case EmojyMessageType.GoodGame:
                return "Final de meci";
            case EmojyMessageType.Oops:
                return "Player-ul greseste sau botul te inteapa";
            default:
                return string.Empty;
        }
    }

    public static string GetProfileSummary(BotProfile profile)
    {
        switch (profile)
        {
            case BotProfile.Randomized:
                return "Alege un profil diferit la inceputul fiecarui meci pentru varietate umana.";
            case BotProfile.Balanced:
                return "Mix intre politețe, realism si putina provocare contextuala.";
            case BotProfile.Pro:
                return "Serios si respectuos. Foloseste doar Good luck!, Well played! si Good game!.";
            case BotProfile.Troll:
                return "Provocator. Foloseste Thanks!, Oops si Nice move! mai ales ironic.";
            case BotProfile.Silent:
                return "Tacut complet. Simuleaza un jucator foarte concentrat.";
            case BotProfile.Noob:
                return "Emotional si instabil. Foloseste des Wow!, Sorry! si Oops.";
            default:
                return string.Empty;
        }
    }

    private static string GetMessageText(EmojyMessageType messageType)
    {
        switch (messageType)
        {
            case EmojyMessageType.GoodLuck:
                return "Good luck!";
            case EmojyMessageType.WellPlayed:
                return "Well played!";
            case EmojyMessageType.NiceMove:
                return "Nice move!";
            case EmojyMessageType.Thanks:
                return "Thanks!";
            case EmojyMessageType.Sorry:
                return "Sorry!";
            case EmojyMessageType.Wow:
                return "Wow!";
            case EmojyMessageType.GoodGame:
                return "Good game!";
            case EmojyMessageType.Oops:
                return "Oops";
            default:
                return string.Empty;
        }
    }

    private static bool TryGetMessageType(string message, out EmojyMessageType messageType)
    {
        string normalized = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "good luck":
            case "good luck!":
                messageType = EmojyMessageType.GoodLuck;
                return true;
            case "well played":
            case "well played!":
                messageType = EmojyMessageType.WellPlayed;
                return true;
            case "nice move":
            case "nice move!":
                messageType = EmojyMessageType.NiceMove;
                return true;
            case "thanks":
            case "thanks!":
                messageType = EmojyMessageType.Thanks;
                return true;
            case "sorry":
            case "sorry!":
                messageType = EmojyMessageType.Sorry;
                return true;
            case "wow":
            case "wow!":
                messageType = EmojyMessageType.Wow;
                return true;
            case "good game":
            case "good game!":
                messageType = EmojyMessageType.GoodGame;
                return true;
            case "oops":
            case "oops!":
                messageType = EmojyMessageType.Oops;
                return true;
            default:
                messageType = EmojyMessageType.GoodLuck;
                return false;
        }
    }
}
