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
    string defendModeText = "DEFEND";

    [SerializeField]
    string takeModeText = "TAKE";

    [SerializeField]
    string idleModeText = "";

    float _nextRefreshTime;
    bool _lastShotButtonState;
    bool _lastDefendButtonState;

    enum DefendButtonMode
    {
        None,
        Defend,
        Take
    }

    void Start()
    {
        _nextRefreshTime = 0f;
        _lastShotButtonState = shotButton != null && shotButton.activeSelf;
        _lastDefendButtonState = defendButton != null && defendButton.activeSelf;

        RefreshShotButton(force: true);
        RefreshDefendButton(force: true);
    }

    void Update()
    {
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

    void RefreshShotButton(bool force)
    {
        if (shotButton == null)
            return;

        bool canShootNow = CanCurrentUserBallOwnerShoot();
        if (!force && canShootNow == _lastShotButtonState)
            return;

        shotButton.SetActive(canShootNow);
        _lastShotButtonState = canShootNow;
    }

    void RefreshDefendButton(bool force)
    {
        if (defendButton == null)
            return;

        bool canDefendNow = CanCurrentUserContestBall();
        UpdateDefendButtonText(canDefendNow);

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

    void UpdateDefendButtonText(bool buttonVisible)
    {
        if (defendButton_Text == null)
            return;

        if (!buttonVisible)
        {
            defendButton_Text.text = idleModeText;
            return;
        }

        DefendButtonMode mode = ResolveDefendButtonMode();
        if (mode == DefendButtonMode.Take)
            defendButton_Text.text = takeModeText;
        else if (mode == DefendButtonMode.Defend)
            defendButton_Text.text = defendModeText;
        else
            defendButton_Text.text = idleModeText;
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
