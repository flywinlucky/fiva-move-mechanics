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
    float runBlendNormalValue = 0.35f;

    [SerializeField]
    [Range(0f, 1f)]
    float runBlendSprintValue = 0.44f;

    [Header("With Ball Sync")]
    [Tooltip("Reduces run blend while owning ball so locomotion looks heavier/slower.")]
    [SerializeField]
    [Range(0.6f, 1f)]
    float withBallRunBlendMultiplier = 0.88f;

    [Header("Fallback Speeds")]
    [SerializeField]
    [Range(0.1f, 30f)]
    float fallbackRunSpeed = 5f;

    [SerializeField]
    [Range(1f, 4f)]
    float fallbackSprintMultiplier = 2f;

    bool hasRunParameter;
    int runParameterHash;

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

        if (!Application.isPlaying)
            return;

        AutoAssignReferences();
        CacheAnimatorParameterAvailability();
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

        if (playerAnimator == null)
            return;

        float currentMoveSpeed = ResolveCurrentMoveSpeed();
        float runBlendValue = ResolveRunBlend(currentMoveSpeed);

        if (hasRunParameter)
            playerAnimator.SetFloat(runParameterHash, runBlendValue, runDampTime, deltaTime);
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

        runParameterHash = Animator.StringToHash(runParameterName);

        if (playerAnimator == null)
            return;

        AnimatorControllerParameter[] parameters = playerAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type != AnimatorControllerParameterType.Float)
                continue;

            if (parameter.name == runParameterName)
                hasRunParameter = true;
        }
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
}
