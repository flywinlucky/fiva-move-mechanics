using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using TMPro;
using UnityEngine;

public class MobileControlsButtons : MonoBehaviour
{
    public GameObject shotButton;
    public GameObject passButton;
    public GameObject sprintButton;
    public GameObject defendButton;
    public TMP_Text defendButton_Text;
    [Space]
    public GameObject JoystickArea;
    public GameObject Joystick_Text;
    public GameObject Joystick_togle;
    [Header("Runtime")]
    [SerializeField]
    bool autoToggleShotButton = true;

    [SerializeField]
    bool autoToggleDefendButton = true;

    [SerializeField]
    [Range(0.05f, 1f)]
    float refreshInterval = 0.12f;

    [Header("Defend Button Text")]
    [SerializeField]
    string defendModeText = "(M)\nDEFEND";

    [SerializeField]
    string takeModeText = "(M)\nTAKE";

    [SerializeField]
    string idleModeText = "";

    [Header("Auto Play")]
    [SerializeField]
    bool enableAutoPlayOnInactivity = true;

    [SerializeField]
    [Min(1f)]
    float autoPlayDelaySeconds = 5f;

    [SerializeField]
    bool wakeUpOnAnyScreenTouch = true;

    [SerializeField]
    bool wakeUpOnAnyKey = true;

    [Header("Keyboard/Gamepad Activity")]
    [SerializeField]
    bool wakeUpOnMovementAxisHold = true;

    [SerializeField]
    [Range(0.01f, 0.8f)]
    float movementAxisWakeThreshold = 0.15f;

    [SerializeField]
    bool wakeUpOnSprintKeyHold = true;

    [SerializeField]
    bool showJoystickTextOnlyDuringAutoPlay = true;

    [Header("Desktop Action Hints")]
    [SerializeField]
    bool showDesktopActionHints = true;

    [SerializeField]
    GameObject desktopShootHint;

    [SerializeField]
    TMP_Text desktopShootHintText;

    [SerializeField]
    string desktopShootReadyText = "CAN SHOOT";

    [SerializeField]
    GameObject desktopDefendHint;

    [SerializeField]
    TMP_Text desktopDefendHintText;

    [SerializeField]
    string desktopDefendModeText = "(M)\nDEFEND";

    [SerializeField]
    string desktopTakeModeText = "(M)\nTAKE";

    float _nextRefreshTime;
    bool _lastShotButtonState;
    bool _lastDefendButtonState;
    bool _autoPlayActive;
    float _localLastInteractionTime;
    bool _isMatchPlayActive;
    bool _matchFlowCallbacksRegistered;

    enum DefendButtonMode
    {
        None,
        Defend,
        Take
    }

    void Awake()
    {
        NormalizeDefendLabelsToM();
        TryRegisterMatchFlowCallbacks();
    }

    void OnValidate()
    {
        NormalizeDefendLabelsToM();
    }

    void OnEnable()
    {
        NormalizeDefendLabelsToM();
        TryRegisterMatchFlowCallbacks();
    }

    void OnDisable()
    {
        SetAutoPlayActive(false, force: true);
    }

    void OnDestroy()
    {
        UnregisterMatchFlowCallbacks();
    }

    void Start()
    {
        TryRegisterMatchFlowCallbacks();

        _nextRefreshTime = 0f;
        _lastShotButtonState = shotButton != null && shotButton.activeSelf;
        _lastDefendButtonState = defendButton != null && defendButton.activeSelf;
        _localLastInteractionTime = Time.time;

        RefreshShotButton(force: true);
        RefreshDefendButton(force: true);
        SetAutoPlayActive(false, force: true);

        if (desktopShootHint != null)
            desktopShootHint.SetActive(false);

        if (desktopDefendHint != null)
            desktopDefendHint.SetActive(false);
    }

    void Update()
    {
        if (!_matchFlowCallbacksRegistered)
            TryRegisterMatchFlowCallbacks();

        HandleAutoPlayState();

        if (!autoToggleShotButton && !autoToggleDefendButton)
            return;

        if (Time.time < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.time + Mathf.Max(0.05f, refreshInterval);
        if (autoToggleShotButton)
            RefreshShotButton(force: false);

        if (autoToggleDefendButton)
            RefreshDefendButton(force: false);
    }

    void HandleAutoPlayState()
    {
        if (!_isMatchPlayActive)
        {
            if (_autoPlayActive)
                SetAutoPlayActive(false, force: true);

            // Do not accumulate inactivity during cutscenes/kickoff/goal resets.
            _localLastInteractionTime = Time.time;
            return;
        }

        if (!enableAutoPlayOnInactivity)
        {
            SetAutoPlayActive(false);
            return;
        }

        if (DetectAnyManualInteractionThisFrame())
        {
            RegisterManualInteraction();

            if (_autoPlayActive)
                SetAutoPlayActive(false);

            return;
        }

        float inactivitySeconds = GetInactivitySeconds();
        bool shouldEnableAutoPlay = inactivitySeconds >= Mathf.Max(1f, autoPlayDelaySeconds);
        SetAutoPlayActive(shouldEnableAutoPlay);
    }

    void TryRegisterMatchFlowCallbacks()
    {
        if (_matchFlowCallbacksRegistered || MatchManager.Instance == null)
            return;

        MatchManager.Instance.OnMatchPlayStart += Instance_OnMatchPlayStart;
        MatchManager.Instance.OnMatchPlayStop += Instance_OnMatchPlayStop;
        _matchFlowCallbacksRegistered = true;
        _isMatchPlayActive = false;
    }

    void UnregisterMatchFlowCallbacks()
    {
        if (!_matchFlowCallbacksRegistered || MatchManager.Instance == null)
            return;

        MatchManager.Instance.OnMatchPlayStart -= Instance_OnMatchPlayStart;
        MatchManager.Instance.OnMatchPlayStop -= Instance_OnMatchPlayStop;
        _matchFlowCallbacksRegistered = false;
    }

    void Instance_OnMatchPlayStart()
    {
        _isMatchPlayActive = true;
        RegisterManualInteraction();
        SetAutoPlayActive(false, force: true);
    }

    void Instance_OnMatchPlayStop()
    {
        _isMatchPlayActive = false;
        RegisterManualInteraction();
        SetAutoPlayActive(false, force: true);
    }

    float GetInactivitySeconds()
    {
        float localInactivity = Mathf.Max(0f, Time.time - _localLastInteractionTime);
        float mobileInactivity = MobileControlsInput.SecondsSinceLastUserInteraction();

        if (float.IsInfinity(mobileInactivity) || float.IsNaN(mobileInactivity))
            return localInactivity;

        return Mathf.Min(localInactivity, Mathf.Max(0f, mobileInactivity));
    }

    bool DetectAnyManualInteractionThisFrame()
    {
        if (wakeUpOnAnyScreenTouch)
        {
            if (Input.touchCount > 0)
                return true;

            if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0))
                return true;
        }

        if (wakeUpOnAnyKey && Input.anyKeyDown)
            return true;

        if (wakeUpOnMovementAxisHold)
        {
            float threshold = Mathf.Clamp(movementAxisWakeThreshold, 0.01f, 0.8f);
            Vector2 keyboardOrPadAxes = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (keyboardOrPadAxes.sqrMagnitude >= threshold * threshold)
                return true;
        }

        if (wakeUpOnSprintKeyHold)
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                return true;
        }

        return false;
    }

    void RegisterManualInteraction()
    {
        _localLastInteractionTime = Time.time;
        MobileControlsInput.RegisterExternalInteraction();
    }

    void SetAutoPlayActive(bool active, bool force = false)
    {
        if (!force && _autoPlayActive == active)
        {
            ApplyManualControlStateToUserTeam();
            return;
        }

        _autoPlayActive = active;

        if (_autoPlayActive)
            MobileControlsInput.ClearQueuedTapActions();

        ApplyManualControlStateToUserTeam();

        if (Joystick_Text != null && showJoystickTextOnlyDuringAutoPlay)
            Joystick_Text.SetActive(_autoPlayActive);
        if (Joystick_togle != null)
        {
            Joystick_togle.SetActive(!_autoPlayActive);
        }
    }

    void ApplyManualControlStateToUserTeam()
    {
        Team userTeam = GetUserControlledTeam();
        if (userTeam == null)
            return;

        userTeam.SetManualControlEnabled(!_autoPlayActive);
    }

    Team GetUserControlledTeam()
    {
        if (MatchManager.Instance == null)
            return null;

        if (MatchManager.Instance.TeamAway != null && MatchManager.Instance.TeamAway.IsUserControlled)
            return MatchManager.Instance.TeamAway;

        if (MatchManager.Instance.TeamHome != null && MatchManager.Instance.TeamHome.IsUserControlled)
            return MatchManager.Instance.TeamHome;

        return null;
    }

    void RefreshShotButton(bool force)
    {
        if (shotButton == null)
        {
            UpdateDesktopShootHint(false);
            return;
        }

        bool canShootNow = CanCurrentUserBallOwnerShoot();
        if (!force && canShootNow == _lastShotButtonState)
        {
            UpdateDesktopShootHint(canShootNow);
            return;
        }

        shotButton.SetActive(canShootNow);
        _lastShotButtonState = canShootNow;
        UpdateDesktopShootHint(canShootNow);
    }

    void RefreshDefendButton(bool force)
    {
        if (defendButton == null)
        {
            UpdateDesktopDefendHint(false, DefendButtonMode.None);
            return;
        }

        bool canDefendNow = CanCurrentUserContestBall();
        DefendButtonMode mode = canDefendNow ? ResolveDefendButtonMode() : DefendButtonMode.None;
        UpdateDefendButtonText(canDefendNow, mode);
        UpdateDesktopDefendHint(canDefendNow, mode);

        if (!force && canDefendNow == _lastDefendButtonState)
            return;

        defendButton.SetActive(canDefendNow);
        _lastDefendButtonState = canDefendNow;
    }

    bool CanCurrentUserBallOwnerShoot()
    {
        if (!autoToggleShotButton)
            return true;

        if (Ball.Instance == null)
            return false;

        Player ballOwner = Ball.Instance.Owner;
        if (ballOwner == null)
            return false;

        if (!ballOwner.IsUserControlled)
            return false;

        if (ballOwner.OppGoal == null)
            return false;

        float distanceToGoal = Vector3.Distance(ballOwner.Position, ballOwner.OppGoal.Position);
        return distanceToGoal <= ballOwner.DistanceShotMaxValid;
    }

    bool CanCurrentUserContestBall()
    {
        if (!autoToggleDefendButton)
            return true;

        if (Ball.Instance == null || Ball.Instance.Owner == null)
            return false;

        Player ballOwner = Ball.Instance.Owner;
        if (ballOwner.PlayerType == PlayerTypes.Goalkeeper)
            return false;

        if (ballOwner.OppositionMembers == null)
            return false;

        // If user owns the ball, allow defend button only when an opponent is close enough to challenge.
        if (ballOwner.IsUserControlled)
        {
            foreach (Player opponent in ballOwner.OppositionMembers)
            {
                if (opponent == null || opponent == ballOwner)
                    continue;

                if (opponent.IsBallWithinTacklableDistance())
                    return true;
            }

            return false;
        }

        // If AI owns the ball, show defend button when user-controlled challenger is in tackle range.
        foreach (Player challenger in ballOwner.OppositionMembers)
        {
            if (challenger == null || !challenger.IsUserControlled)
                continue;

            if (challenger.IsBallWithinTacklableDistance())
                return true;
        }

        return false;
    }

    void UpdateDefendButtonText(bool buttonVisible, DefendButtonMode mode)
    {
        if (defendButton_Text == null)
            return;

        if (!buttonVisible)
        {
            defendButton_Text.text = idleModeText;
            return;
        }

        if (mode == DefendButtonMode.Take)
            defendButton_Text.text = takeModeText;
        else if (mode == DefendButtonMode.Defend)
            defendButton_Text.text = defendModeText;
        else
            defendButton_Text.text = idleModeText;
    }

    void UpdateDesktopShootHint(bool canShootNow)
    {
        if (!ShouldShowDesktopHints())
        {
            if (desktopShootHint != null)
                desktopShootHint.SetActive(false);
            return;
        }

        if (desktopShootHint != null)
            desktopShootHint.SetActive(canShootNow);

        if (desktopShootHintText != null)
            desktopShootHintText.text = canShootNow ? desktopShootReadyText : string.Empty;
    }

    void UpdateDesktopDefendHint(bool canDefendNow, DefendButtonMode mode)
    {
        if (!ShouldShowDesktopHints())
        {
            if (desktopDefendHint != null)
                desktopDefendHint.SetActive(false);
            return;
        }

        if (desktopDefendHint != null)
            desktopDefendHint.SetActive(canDefendNow);

        if (desktopDefendHintText == null)
            return;

        if (!canDefendNow)
        {
            desktopDefendHintText.text = string.Empty;
            return;
        }

        if (mode == DefendButtonMode.Take)
            desktopDefendHintText.text = desktopTakeModeText;
        else if (mode == DefendButtonMode.Defend)
            desktopDefendHintText.text = desktopDefendModeText;
        else
            desktopDefendHintText.text = string.Empty;
    }

    bool ShouldShowDesktopHints()
    {
        return showDesktopActionHints && !Application.isMobilePlatform;
    }

    void NormalizeDefendLabelsToM()
    {
        defendModeText = NormalizeActionLabelToM(defendModeText, "DEFEND");
        takeModeText = NormalizeActionLabelToM(takeModeText, "TAKE");
        desktopDefendModeText = NormalizeActionLabelToM(desktopDefendModeText, "DEFEND");
        desktopTakeModeText = NormalizeActionLabelToM(desktopTakeModeText, "TAKE");
    }

    string NormalizeActionLabelToM(string value, string fallbackAction)
    {
        string action = ExtractActionLine(value, fallbackAction);
        return $"(M)\n{action}";
    }

    string ExtractActionLine(string value, string fallbackAction)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallbackAction;

        string normalized = value.Replace("\r", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return fallbackAction;

        string[] lines = normalized.Split('\n');
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("(") && line.EndsWith(")"))
                continue;

            return line;
        }

        return fallbackAction;
    }

    DefendButtonMode ResolveDefendButtonMode()
    {
        if (Ball.Instance == null || Ball.Instance.Owner == null)
            return DefendButtonMode.None;

        Player ballOwner = Ball.Instance.Owner;
        if (ballOwner.PlayerType == PlayerTypes.Goalkeeper)
            return DefendButtonMode.None;

        if (ballOwner.IsUserControlled)
            return DefendButtonMode.Defend;

        if (ballOwner.OppositionMembers == null)
            return DefendButtonMode.None;

        foreach (Player challenger in ballOwner.OppositionMembers)
        {
            if (challenger == null || !challenger.IsUserControlled)
                continue;

            if (challenger.IsBallWithinTacklableDistance())
                return DefendButtonMode.Take;
        }

        return DefendButtonMode.None;
    }
}
