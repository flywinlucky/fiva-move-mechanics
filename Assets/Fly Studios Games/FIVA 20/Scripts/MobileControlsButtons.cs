using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Managers;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using UnityEngine;

public class MobileControlsButtons : MonoBehaviour
{
    public GameObject shotButton;
    public GameObject passButton;
    public GameObject sprintButton;
    public GameObject defendButton;
   
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

    float _nextRefreshTime;
    bool _lastShotButtonState;
    bool _lastDefendButtonState;

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
}
