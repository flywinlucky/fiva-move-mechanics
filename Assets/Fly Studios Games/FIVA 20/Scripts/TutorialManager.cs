using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using System.Collections;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    enum TutorialPhase
    {
        None,
        Move,
        Sprint,
        GetBall,
        PassBall,
        ScoreGoal,
        Completed
    }

    [Serializable]
    class PhaseContentVisibility
    {
        public TutorialPhase phase = TutorialPhase.None;

        [Min(0f)]
        public float applyDelaySeconds = 0f;

        public bool applyDelayUseUnscaledTime = true;

        public List<GameObject> objectsToShow = new List<GameObject>();
        public List<GameObject> objectsToHide = new List<GameObject>();
        public List<TimedObjectToggle> delayedToggles = new List<TimedObjectToggle>();
    }

    [Serializable]
    class TimedObjectToggle
    {
        public GameObject target;
        public bool setActive = true;

        [Min(0f)]
        public float delaySeconds = 0f;

        public bool useUnscaledTime = true;
    }

    [SerializeField]
    public GameObject tutorialCanvas;
    [Header("Setup")]
    [SerializeField]
    bool runOnStart = true;

    [SerializeField]
    TMP_Text tutorialStepText;

    [Header("Phase Text")]
    [SerializeField]
    string moveStepText = "Move your player";

    [SerializeField]
    string sprintStepText = "Sprint at least 10 meters";

    [SerializeField]
    string getBallStepText = "Take control of the ball";

    [SerializeField]
    string passStepText = "Pass to a teammate";

    [SerializeField]
    string scoreStepText = "Score a goal";

    [SerializeField]
    string completedStepText = "Tutorial completed!";

    [Header("Move Step")]
    [SerializeField]
    [Min(0.1f)]
    float moveDistanceRequired = 4f;

    [Header("Sprint Step")]
    [SerializeField]
    [Min(0.1f)]
    float sprintDistanceRequired = 10f;

    [Header("Ball Step")]
    [SerializeField]
    bool placeBallAtGetBallStep = true;

    [SerializeField]
    Transform ballSpawnPoint;

    [Header("Phase Content Visibility")]
    [SerializeField]
    bool usePhaseContentVisibility = true;

    [SerializeField]
    List<PhaseContentVisibility> phaseContentVisibility = new List<PhaseContentVisibility>();

    [Header("Control Highlights")]
    [SerializeField]
    bool useControlHighlights = true;

    [SerializeField]
    CanvasGroup joystickHighlightGroup;

    [SerializeField]
    CanvasGroup passHighlightGroup;

    [SerializeField]
    CanvasGroup sprintHighlightGroup;

    [SerializeField]
    CanvasGroup shootHighlightGroup;

    [SerializeField]
    [Range(0f, 1f)]
    float inactiveControlAlpha = 0.25f;

    [SerializeField]
    [Range(0f, 1f)]
    float activeControlBaseAlpha = 0.9f;

    [SerializeField]
    [Range(0f, 1f)]
    float activeControlPulseAmplitude = 0.1f;

    [SerializeField]
    [Min(0.1f)]
    float activeControlPulseSpeed = 2.8f;

    [SerializeField]
    [Min(0.01f)]
    float highlightFadeSpeed = 8f;

    [Header("Performance")]
    [SerializeField]
    [Min(0.05f)]
    float userPlayerRefreshInterval = 0.35f;

    TutorialPhase _phase = TutorialPhase.None;
    Player _userPlayer;
    Player _passReferencePlayer;
    Vector3 _moveStartPosition;
    Vector3 _sprintLastPosition;
    float _sprintMovedDistance;
    bool _goalScoredInScoreStep;
    readonly List<Coroutine> _phaseVisibilityCoroutines = new List<Coroutine>();
    float _nextUserPlayerRefreshTime;

    void Start()
    {
        if (runOnStart)
            BeginTutorial();
    }

    void OnEnable()
    {
        if (MatchManager.Instance != null)
            MatchManager.Instance.OnGoalScored += HandleGoalScored;
    }

    void OnDisable()
    {
        CancelPendingPhaseVisibilityCoroutines();

        if (MatchManager.Instance != null)
            MatchManager.Instance.OnGoalScored -= HandleGoalScored;
    }

    public void BeginTutorial()
    {
        _goalScoredInScoreStep = false;
        _passReferencePlayer = null;
        RefreshUserPlayer();
        _nextUserPlayerRefreshTime = Time.unscaledTime;
        SetPhase(TutorialPhase.Move);
    }

    public void SkipToNextPhase()
    {
        AdvancePhase();
    }

    void Update()
    {
        UpdateControlHighlights();

        if (_phase == TutorialPhase.None || _phase == TutorialPhase.Completed)
            return;

        if ((_userPlayer == null || !_userPlayer.IsUserControlled)
            && Time.unscaledTime >= _nextUserPlayerRefreshTime)
        {
            RefreshUserPlayer();
            _nextUserPlayerRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, userPlayerRefreshInterval);
        }

        switch (_phase)
        {
            case TutorialPhase.Move:
                UpdateMoveStep();
                break;
            case TutorialPhase.Sprint:
                UpdateSprintStep();
                break;
            case TutorialPhase.GetBall:
                UpdateGetBallStep();
                break;
            case TutorialPhase.PassBall:
                UpdatePassStep();
                break;
            case TutorialPhase.ScoreGoal:
                UpdateScoreStep();
                break;
        }
    }

    void UpdateMoveStep()
    {
        if (_userPlayer == null)
            return;

        float movedDistance = Vector3.Distance(_moveStartPosition, _userPlayer.Position);
        if (movedDistance >= moveDistanceRequired)
            AdvancePhase();
    }

    void UpdateSprintStep()
    {
        if (_userPlayer == null)
            return;

        float frameMoveDistance = Vector3.Distance(_sprintLastPosition, _userPlayer.Position);
        _sprintLastPosition = _userPlayer.Position;

        if (_userPlayer.IsSprinting)
            _sprintMovedDistance += frameMoveDistance;

        if (_sprintMovedDistance >= sprintDistanceRequired)
            AdvancePhase();
    }

    void UpdateGetBallStep()
    {
        if (Ball.Instance == null)
            return;

        if (Ball.Instance.Owner != null && Ball.Instance.Owner.IsUserControlled)
            AdvancePhase();
    }

    void UpdatePassStep()
    {
        if (Ball.Instance == null)
            return;

        if (_passReferencePlayer == null)
            _passReferencePlayer = _userPlayer;

        Player owner = Ball.Instance.Owner;
        if (_passReferencePlayer == null || owner == null || owner == _passReferencePlayer)
            return;

        if (IsTeammate(_passReferencePlayer, owner))
            AdvancePhase();
    }

    void UpdateScoreStep()
    {
        if (_goalScoredInScoreStep)
            AdvancePhase();
    }

    void SetPhase(TutorialPhase phase)
    {
        _phase = phase;
        ApplyPhaseContentVisibility(_phase);
        UpdateControlHighlights(true);

        switch (_phase)
        {
            case TutorialPhase.Move:
                RefreshUserPlayer();
                if (_userPlayer != null)
                    _moveStartPosition = _userPlayer.Position;
                SetStepText(moveStepText);
                break;

            case TutorialPhase.Sprint:
                RefreshUserPlayer();
                _sprintMovedDistance = 0f;
                _sprintLastPosition = _userPlayer != null ? _userPlayer.Position : Vector3.zero;
                SetStepText(sprintStepText);
                break;

            case TutorialPhase.GetBall:
                SetStepText(getBallStepText);
                PrepareBallForGetBallStep();
                break;

            case TutorialPhase.PassBall:
                _passReferencePlayer = _userPlayer;
                SetStepText(passStepText);
                break;

            case TutorialPhase.ScoreGoal:
                _goalScoredInScoreStep = false;
                SetStepText(scoreStepText);
                break;

            case TutorialPhase.Completed:
                SetStepText(completedStepText);
                break;
        }
    }

    void ApplyPhaseContentVisibility(TutorialPhase phase)
    {
        if (!usePhaseContentVisibility || phaseContentVisibility == null || phaseContentVisibility.Count == 0)
            return;

        CancelPendingPhaseVisibilityCoroutines();

        for (int i = 0; i < phaseContentVisibility.Count; i++)
        {
            PhaseContentVisibility config = phaseContentVisibility[i];
            if (config == null || config.phase != phase)
                continue;

            float delay = Mathf.Max(0f, config.applyDelaySeconds);
            if (delay <= 0f)
            {
                ApplyObjectVisibilityList(config.objectsToHide, false);
                ApplyObjectVisibilityList(config.objectsToShow, true);
                ApplyDelayedObjectToggles(config.delayedToggles);
                continue;
            }

            Coroutine routine = StartCoroutine(ApplyPhaseContentVisibilityWithDelay(config,
                delay,
                config.applyDelayUseUnscaledTime));
            if (routine != null)
                _phaseVisibilityCoroutines.Add(routine);
        }
    }

    IEnumerator ApplyPhaseContentVisibilityWithDelay(PhaseContentVisibility config, float delaySeconds, bool useUnscaledTime)
    {
        if (useUnscaledTime)
        {
            float elapsed = 0f;
            while (elapsed < delaySeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(delaySeconds);
        }

        if (config == null)
            yield break;

        ApplyObjectVisibilityList(config.objectsToHide, false);
        ApplyObjectVisibilityList(config.objectsToShow, true);
        ApplyDelayedObjectToggles(config.delayedToggles);
    }

    void ApplyObjectVisibilityList(List<GameObject> objects, bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Count; i++)
        {
            GameObject go = objects[i];
            if (go == null)
                continue;

            if (go.activeSelf != active)
                go.SetActive(active);
        }
    }

    void ApplyDelayedObjectToggles(List<TimedObjectToggle> toggles)
    {
        if (toggles == null)
            return;

        for (int i = 0; i < toggles.Count; i++)
        {
            TimedObjectToggle toggle = toggles[i];
            if (toggle == null || toggle.target == null)
                continue;

            float delay = Mathf.Max(0f, toggle.delaySeconds);
            if (delay <= 0f)
            {
                if (toggle.target.activeSelf != toggle.setActive)
                    toggle.target.SetActive(toggle.setActive);

                continue;
            }

            Coroutine routine = StartCoroutine(ApplyDelayedToggleRoutine(toggle.target,
                toggle.setActive,
                delay,
                toggle.useUnscaledTime));
            if (routine != null)
                _phaseVisibilityCoroutines.Add(routine);
        }
    }

    IEnumerator ApplyDelayedToggleRoutine(GameObject target, bool active, float delaySeconds, bool useUnscaledTime)
    {
        if (useUnscaledTime)
        {
            float elapsed = 0f;
            while (elapsed < delaySeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(delaySeconds);
        }

        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    void CancelPendingPhaseVisibilityCoroutines()
    {
        if (_phaseVisibilityCoroutines.Count == 0)
            return;

        for (int i = 0; i < _phaseVisibilityCoroutines.Count; i++)
        {
            Coroutine routine = _phaseVisibilityCoroutines[i];
            if (routine != null)
                StopCoroutine(routine);
        }

        _phaseVisibilityCoroutines.Clear();
    }

    void AdvancePhase()
    {
        if (_phase == TutorialPhase.Completed)
            return;

        SetPhase(_phase + 1);
    }

    void PrepareBallForGetBallStep()
    {
        if (!placeBallAtGetBallStep || Ball.Instance == null || ballSpawnPoint == null)
            return;

        Ball.Instance.Owner = null;
        Ball.Instance.Rigidbody.isKinematic = false;
        Ball.Instance.NormalizedPosition = ballSpawnPoint.position;
        Ball.Instance.Trap();
    }

    void HandleGoalScored(string message)
    {
        if (_phase == TutorialPhase.ScoreGoal)
            _goalScoredInScoreStep = true;
    }

    void RefreshUserPlayer()
    {
        Player[] players = FindObjectsOfType<Player>();
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].IsUserControlled)
            {
                _userPlayer = players[i];
                return;
            }
        }

        _userPlayer = null;
    }

    bool IsTeammate(Player a, Player b)
    {
        if (a == null || b == null || a.TeamMembers == null)
            return false;

        for (int i = 0; i < a.TeamMembers.Count; i++)
        {
            if (a.TeamMembers[i] == b)
                return true;
        }

        return false;
    }

    void SetStepText(string value)
    {
        if (tutorialStepText == null)
            return;

        tutorialStepText.text = value;
    }

    void UpdateControlHighlights(bool snap = false)
    {
        if (!useControlHighlights)
            return;

        float pulse = Mathf.Sin(Time.unscaledTime * Mathf.Max(0.1f, activeControlPulseSpeed));
        float pulseBoost = (pulse * 0.5f + 0.5f) * Mathf.Clamp01(activeControlPulseAmplitude);
        float activeAlpha = Mathf.Clamp01(activeControlBaseAlpha + pulseBoost);
        float inactiveAlpha = Mathf.Clamp01(inactiveControlAlpha);

        float joystickTarget = inactiveAlpha;
        float passTarget = inactiveAlpha;
        float sprintTarget = inactiveAlpha;
        float shootTarget = inactiveAlpha;

        switch (_phase)
        {
            case TutorialPhase.Move:
                joystickTarget = activeAlpha;
                sprintTarget = inactiveAlpha + 0.1f;
                break;

            case TutorialPhase.Sprint:
                joystickTarget = Mathf.Max(joystickTarget, inactiveAlpha + 0.25f);
                sprintTarget = activeAlpha;
                break;

            case TutorialPhase.GetBall:
                joystickTarget = activeAlpha;
                sprintTarget = activeAlpha;
                break;

            case TutorialPhase.PassBall:
                passTarget = activeAlpha;
                joystickTarget = Mathf.Max(joystickTarget, inactiveAlpha + 0.2f);
                break;

            case TutorialPhase.ScoreGoal:
                shootTarget = activeAlpha;
                joystickTarget = Mathf.Max(joystickTarget, inactiveAlpha + 0.2f);
                break;

            case TutorialPhase.Completed:
                joystickTarget = inactiveAlpha;
                passTarget = inactiveAlpha;
                sprintTarget = inactiveAlpha;
                shootTarget = inactiveAlpha;
                break;
        }

        ApplyHighlightAlpha(joystickHighlightGroup, joystickTarget, snap);
        ApplyHighlightAlpha(passHighlightGroup, passTarget, snap);
        ApplyHighlightAlpha(sprintHighlightGroup, sprintTarget, snap);
        ApplyHighlightAlpha(shootHighlightGroup, shootTarget, snap);
    }

    void ApplyHighlightAlpha(CanvasGroup group, float targetAlpha, bool snap)
    {
        if (group == null)
            return;

        float nextAlpha = snap
            ? targetAlpha
            : Mathf.MoveTowards(group.alpha, targetAlpha, Mathf.Max(0.01f, highlightFadeSpeed) * Time.unscaledDeltaTime);

        group.alpha = Mathf.Clamp01(nextAlpha);
    }
}
