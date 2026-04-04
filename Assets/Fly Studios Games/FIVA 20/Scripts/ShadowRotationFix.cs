using UnityEngine;

public class ShadowRotationFix : MonoBehaviour
{
    [SerializeField] private Transform shadowTransform;

    private readonly Quaternion identityRotation = Quaternion.identity;

    void LateUpdate()
    {
        if (shadowTransform != null)
        {
            shadowTransform.rotation = identityRotation;
        }
    }
}