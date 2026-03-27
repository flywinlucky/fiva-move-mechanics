using UnityEngine;

public class TestCallTriger : MonoBehaviour
{
    [Header("Animator Test")]
    [SerializeField]
    Animator targetAnimator;

    [SerializeField]
    string passLayerName = "passlayer";

    [SerializeField]
    string passTriggerName = "pass";

    [SerializeField]
    KeyCode triggerKey = KeyCode.P;

    [SerializeField]
    [Range(0f, 1f)]
    float passLayerWeight = 1f;

    int _passTriggerHash;
    int _passLayerIndex = -1;

    void Awake()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>();

        _passTriggerHash = Animator.StringToHash(passTriggerName);

        if (targetAnimator != null)
            _passLayerIndex = targetAnimator.GetLayerIndex(passLayerName);
    }

    void Update()
    {
        if (targetAnimator == null)
            return;

        if (!Input.GetKeyDown(triggerKey))
            return;

        if (_passLayerIndex >= 0)
            targetAnimator.SetLayerWeight(_passLayerIndex, passLayerWeight);

        targetAnimator.SetTrigger(_passTriggerHash);
    }
}
