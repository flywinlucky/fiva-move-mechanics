using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using UnityEngine;

public class MobileControlsButtons : MonoBehaviour
{
    public GameObject shotButton;
    public GameObject passButton;
    public GameObject sprintButton;

    [Space]
    public GameObject JoystickArea;

    [Header("Runtime")]
    [SerializeField]
    bool autoToggleShotButton = true;

    [SerializeField]
    [Range(0.05f, 1f)]
    float refreshInterval = 0.12f;

    float _nextRefreshTime;
    bool _lastShotButtonState;

    void Start()
    {
        _nextRefreshTime = 0f;
        _lastShotButtonState = shotButton != null && shotButton.activeSelf;

        RefreshShotButton(force: true);
    }

    void Update()
    {
        if (!autoToggleShotButton)
            return;

        if (Time.time < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.time + Mathf.Max(0.05f, refreshInterval);
        RefreshShotButton(force: false);
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
