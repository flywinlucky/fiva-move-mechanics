using Assets.SimpleSteering.Scripts.Movement;
using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAnimationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    Animator playerAnimator;

    [SerializeField]
    Player player;

    [SerializeField]
    RPGMovement movement;

    [Header("Blend Tree Parameters")]
    [SerializeField]
    string runParameterName = "run";

    [Header("Pass Animation")]
    [SerializeField]
    string passTriggerName = "pass";

    [SerializeField]
    string passLayerName = "passlayer";

    [SerializeField]
    [Range(0f, 1f)]
    float passLayerActiveWeight = 1f;

    [Tooltip("Expected pass clip duration in seconds.")]
    [SerializeField]
    [Range(0.1f, 3f)]
    float passAnimationDuration = 1f;

    [Tooltip("Kick ball at this normalized point from pass clip length (0..1).")]
    [SerializeField]
    [Range(0f, 1f)]
    float passReleaseNormalizedTime = 0.5f;

    [SerializeField]
    string shotTriggerName = "shot";

    [Tooltip("Expected shot clip duration in seconds.")]
    [SerializeField]
    [Range(0.1f, 3f)]
    float shotAnimationDuration = 0.8f;

    [Tooltip("Release shot ball at this normalized point from shot clip length (0..1).")]
    [SerializeField]
    [Range(0f, 1f)]
    float shotReleaseNormalizedTime = 0.35f;

    [Header("Kick Timing")]
    [Tooltip("Global kick release speed scale. < 1 = faster release, > 1 = slower release.")]
    [SerializeField]
    [Range(0.25f, 2f)]
    float kickReleaseTimeScale = 1f;

    [SerializeField]
    [Range(1f, 30f)]
    float passLayerBlendSpeed = 12f;

    [Header("Smoothing")]
    [SerializeField]
    [Range(0f, 0.5f)]
    float runDampTime = 0.12f;

    [SerializeField]
    [Range(0f, 1f)]
    float stopSpeedThreshold = 0.05f;

    [Header("1D Blend Threshold Targets")]
    [SerializeField]
    [Range(0f, 1f)]
    float runBlendIdleValue = 0f;

    [SerializeField]
    [Range(0f, 1f)]
    float runBlendNormalValue = 0.30f; // 0.35 * 0.85

    [SerializeField]
    [Range(0f, 1f)]
    float runBlendSprintValue = 0.37f; // 0.44 * 0.85

    [Header("With Ball Sync")]
    [Tooltip("Reduces run blend while owning ball so locomotion looks heavier/slower.")]
    [SerializeField]
    [Range(0.6f, 1f)]
    float withBallRunBlendMultiplier = 0.85f; // sincron cu mișcarea cu mingea

    [Header("Fallback Speeds")]
    [SerializeField]
    [Range(0.1f, 30f)]
    float fallbackRunSpeed = 4.25f; // 5f * 0.85

    [SerializeField]
    [Range(1f, 4f)]
    float fallbackSprintMultiplier = 1.7f; // 2f * 0.85

    bool hasRunParameter;
    int runParameterHash;
    bool hasPassTrigger;
    int passTriggerHash;
    bool hasShotTrigger;
    int shotTriggerHash;
    int passLayerIndex = -1;
    float passLayerHoldUntil;
    bool _animatorCacheBuilt;

    void Awake()
    {
        AutoAssignReferences();
        CacheAnimatorParameterAvailability();
    }

    void OnValidate()
    {
        stopSpeedThreshold = Mathf.Clamp01(stopSpeedThreshold);
        fallbackRunSpeed = Mathf.Max(0.1f, fallbackRunSpeed);
        fallbackSprintMultiplier = Mathf.Max(1f, fallbackSprintMultiplier);
        runDampTime = Mathf.Clamp(runDampTime, 0f, 0.5f);
        runBlendIdleValue = Mathf.Clamp01(runBlendIdleValue);
        runBlendNormalValue = Mathf.Clamp(runBlendNormalValue, runBlendIdleValue, 1f);
        runBlendSprintValue = Mathf.Clamp(runBlendSprintValue, runBlendNormalValue, 1f);
        withBallRunBlendMultiplier = Mathf.Clamp(withBallRunBlendMultiplier, 0.6f, 1f);
        passLayerActiveWeight = Mathf.Clamp01(passLayerActiveWeight);
        passAnimationDuration = Mathf.Max(0.1f, passAnimationDuration);
        passReleaseNormalizedTime = Mathf.Clamp01(passReleaseNormalizedTime);
        shotAnimationDuration = Mathf.Max(0.1f, shotAnimationDuration);
        shotReleaseNormalizedTime = Mathf.Clamp01(shotReleaseNormalizedTime);
        kickReleaseTimeScale = Mathf.Clamp(kickReleaseTimeScale, 0.25f, 2f);
        passLayerBlendSpeed = Mathf.Max(1f, passLayerBlendSpeed);

        if (!Application.isPlaying)
            return;

        AutoAssignReferences();

        // During validation Animator may exist but not be fully initialized yet.
        // Runtime refresh/initialize paths rebuild the cache safely.
        _animatorCacheBuilt = false;
    }

    // Player.cs calls this each LateUpdate. Update acts as fallback if not integrated.
    void Update()
    {
        if (player != null)
            return;

        RefreshAnimation(Time.deltaTime);
    }

    public void Initialize(Player owner, RPGMovement ownerMovement = null)
    {
        player = owner;
        movement = ownerMovement;

        if (movement == null && player != null)
            movement = player.RPGMovement;

        AutoAssignReferences();
        CacheAnimatorParameterAvailability();
    }

    public void RefreshAnimation(float deltaTime = -1f)
    {
        if (deltaTime < 0f)
            deltaTime = Time.deltaTime;

        if (!HasValidAnimatorController())
            return;

        if (!_animatorCacheBuilt)
            CacheAnimatorParameterAvailability();

        float currentMoveSpeed = ResolveCurrentMoveSpeed();
        float runBlendValue = ResolveRunBlend(currentMoveSpeed);

        if (hasRunParameter)
            playerAnimator.SetFloat(runParameterHash, runBlendValue, runDampTime, deltaTime);

        UpdatePassLayerWeight(deltaTime);
    }

    void AutoAssignReferences()
    {
        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();

        if (player == null)
            player = GetComponent<Player>();

        if (movement == null)
            movement = GetComponent<RPGMovement>();

        if (movement == null && player != null)
            movement = player.RPGMovement;
    }

    void CacheAnimatorParameterAvailability()
    {
        hasRunParameter = false;
        hasPassTrigger = false;
        hasShotTrigger = false;
        _animatorCacheBuilt = false;

        runParameterHash = Animator.StringToHash(runParameterName);
        passTriggerHash = Animator.StringToHash(passTriggerName);
        shotTriggerHash = Animator.StringToHash(shotTriggerName);
        passLayerIndex = -1;

        if (!HasValidAnimatorController())
            return;

        passLayerIndex = playerAnimator.GetLayerIndex(passLayerName);

        AnimatorControllerParameter[] parameters = playerAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Float
                && parameter.name == runParameterName)
                hasRunParameter = true;

            if (parameter.type == AnimatorControllerParameterType.Trigger
                && parameter.name == passTriggerName)
                hasPassTrigger = true;

            if (parameter.type == AnimatorControllerParameterType.Trigger
                && parameter.name == shotTriggerName)
                hasShotTrigger = true;
        }

        _animatorCacheBuilt = true;
    }

    void UpdatePassLayerWeight(float deltaTime)
    {
        if (!HasValidAnimatorController() || passLayerIndex < 0)
            return;

        float targetWeight = Time.time <= passLayerHoldUntil ? passLayerActiveWeight : 0f;
        float currentWeight = playerAnimator.GetLayerWeight(passLayerIndex);
        float nextWeight = Mathf.MoveTowards(currentWeight, targetWeight, passLayerBlendSpeed * Mathf.Max(0f, deltaTime));
        playerAnimator.SetLayerWeight(passLayerIndex, nextWeight);
    }

    public float TriggerPassAnimationAndGetReleaseDelay(float fallbackDelay = 0.5f)
    {
        return TriggerKickAnimationAndGetReleaseDelay(
            passTriggerHash,
            hasPassTrigger,
            passAnimationDuration,
            passReleaseNormalizedTime,
            fallbackDelay);
    }

    public float TriggerShotAnimationAndGetReleaseDelay(float fallbackDelay = 0.35f)
    {
        return TriggerKickAnimationAndGetReleaseDelay(
            shotTriggerHash,
            hasShotTrigger,
            shotAnimationDuration,
            shotReleaseNormalizedTime,
            fallbackDelay);
    }

    float TriggerKickAnimationAndGetReleaseDelay(
        int triggerHash,
        bool hasTrigger,
        float animationDuration,
        float releaseNormalizedTime,
        float fallbackDelay)
    {
        AutoAssignReferences();
        if (!_animatorCacheBuilt)
            CacheAnimatorParameterAvailability();

        float timingScale = Mathf.Clamp(kickReleaseTimeScale, 0.25f, 2f);
        float duration = Mathf.Max(0.1f, animationDuration) * timingScale;
        float releaseDelay = duration * Mathf.Clamp01(releaseNormalizedTime);

        if (!HasValidAnimatorController())
            return Mathf.Max(0.01f, fallbackDelay);

        if (hasTrigger)
            playerAnimator.SetTrigger(triggerHash);

        passLayerHoldUntil = Time.time + duration;

        return Mathf.Max(0.01f, releaseDelay);
    }

    float ResolveCurrentMoveSpeed()
    {
        if (movement == null && player != null)
            movement = player.RPGMovement;

        if (movement == null)
            return 0f;

        if (!movement.Steer || movement.MovementDirection.sqrMagnitude <= 0.0001f)
            return 0f;

        Vector3 planarVelocity = movement.Velocity;
        planarVelocity.y = 0f;

        float physicsSpeed = planarVelocity.magnitude;
        float steeringSpeed = Mathf.Max(0f, movement.CurrentSpeed);

        return Mathf.Max(physicsSpeed, steeringSpeed);
    }

    float ResolveRunBlend(float currentMoveSpeed)
    {
        float runSpeed = ResolveRunSpeedReference();
        float sprintSpeed = ResolveSprintSpeedReference(runSpeed);

        if (IsOwningBall())
            currentMoveSpeed *= withBallRunBlendMultiplier;

        if (currentMoveSpeed <= stopSpeedThreshold)
            return runBlendIdleValue;

        if (currentMoveSpeed <= runSpeed)
        {
            float tRun = Mathf.InverseLerp(stopSpeedThreshold, runSpeed, currentMoveSpeed);
            return Mathf.Lerp(runBlendIdleValue, runBlendNormalValue, tRun);
        }

        float sprintT = Mathf.InverseLerp(runSpeed, sprintSpeed, currentMoveSpeed);
        float linearBlend = Mathf.Lerp(runBlendNormalValue, runBlendSprintValue, sprintT);

        return Mathf.Clamp(linearBlend, runBlendIdleValue, runBlendSprintValue);
    }

    bool IsOwningBall()
    {
        return player != null
            && Ball.Instance != null
            && Ball.Instance.Owner == player;
    }

    float ResolveRunSpeedReference()
    {
        if (movement == null && player != null)
            movement = player.RPGMovement;

        // Prefer runtime target speed because gameplay states can scale movement
        // (for example when controlling the ball) and animation should follow that.
        if (movement != null && movement.Speed > 0.01f)
        {
            float runSpeed = Mathf.Max(0.1f, movement.Speed);

            if (player != null && player.IsSprinting)
            {
                float sprintMultiplier = Mathf.Max(1f, player.SprintSpeedMultiplier);
                runSpeed /= sprintMultiplier;
            }

            return Mathf.Max(0.1f, runSpeed);
        }

        if (player != null && player.ActualSpeed > 0.01f)
            return Mathf.Max(0.1f, player.ActualSpeed);

        return fallbackRunSpeed;
    }

    float ResolveSprintSpeedReference(float runSpeed)
    {
        float sprintMultiplier = fallbackSprintMultiplier;
        if (player != null)
            sprintMultiplier = Mathf.Max(1f, player.SprintSpeedMultiplier);

        float sprintSpeed = runSpeed * sprintMultiplier;
        if (sprintSpeed <= runSpeed + 0.01f)
            sprintSpeed = runSpeed + Mathf.Max(0.25f, runSpeed * 0.25f);

        if (movement != null)
            sprintSpeed = Mathf.Max(sprintSpeed, movement.Speed);

        return sprintSpeed;
    }

    bool HasValidAnimatorController()
    {
        return playerAnimator != null
            && playerAnimator.runtimeAnimatorController != null
            && playerAnimator.isActiveAndEnabled
            && playerAnimator.gameObject.activeInHierarchy
            && playerAnimator.isInitialized;
    }
}
