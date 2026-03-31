using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using UnityEngine;

public class MobileControlsButtons : MonoBehaviour
{
    public GameObject shotButton;
    public GameObject passButton;
    public GameObject sprintButton;
    public GameObject takeKickOffBlockButton;

    [Space]
    public GameObject JoystickArea;

    [Header("Runtime")]
    [SerializeField]
    bool autoToggleShotButton = true;

    [SerializeField]
    [Range(0.05f, 1f)]
    float refreshInterval = 0.12f;

    [SerializeField]
    bool autoToggleTakeKickOffBlockButton = true;

    [SerializeField]
    [Range(0.6f, 5f)]
    float takeKickOffBlockThreatDistance = 2.4f;

    float _nextRefreshTime;
    bool _lastShotButtonState;
    bool _lastTakeKickOffBlockButtonState;

    void Start()
    {
        _nextRefreshTime = 0f;
        _lastShotButtonState = shotButton != null && shotButton.activeSelf;
        _lastTakeKickOffBlockButtonState = takeKickOffBlockButton != null && takeKickOffBlockButton.activeSelf;

        RefreshShotButton(force: true);
        RefreshTakeKickOffBlockButton(force: true);
    }

    void Update()
    {
        if (!autoToggleShotButton)
            return;

        if (Time.time < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.time + Mathf.Max(0.05f, refreshInterval);
        RefreshShotButton(force: false);
        RefreshTakeKickOffBlockButton(force: false);
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

    void RefreshTakeKickOffBlockButton(bool force)
    {
        if (takeKickOffBlockButton == null)
            return;

        bool shouldShow = ShouldShowTakeKickOffBlockButton();
        if (!force && shouldShow == _lastTakeKickOffBlockButtonState)
            return;

        takeKickOffBlockButton.SetActive(shouldShow);
        _lastTakeKickOffBlockButtonState = shouldShow;
    }

    bool ShouldShowTakeKickOffBlockButton()
    {
        if (!autoToggleTakeKickOffBlockButton)
            return takeKickOffBlockButton != null && takeKickOffBlockButton.activeSelf;

        if (Ball.Instance == null || Ball.Instance.Owner == null)
            return false;

        Player carrier = Ball.Instance.Owner;
        if (!carrier.IsUserControlled)
            return false;

        if (carrier.PlayerType == PlayerTypes.Goalkeeper)
            return false;

        if (carrier.OppositionMembers == null || carrier.OppositionMembers.Count == 0)
            return false;

        float threatDistance = Mathf.Max(0.6f, takeKickOffBlockThreatDistance);
        float sqrThreatDistance = threatDistance * threatDistance;

        for (int i = 0; i < carrier.OppositionMembers.Count; i++)
        {
            Player opponent = carrier.OppositionMembers[i];
            if (opponent == null)
                continue;

            if ((opponent.Position - carrier.Position).sqrMagnitude <= sqrThreatDistance)
                return true;
        }

        return false;
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
}
