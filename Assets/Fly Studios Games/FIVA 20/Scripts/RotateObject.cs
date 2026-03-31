using UnityEngine;

public class RotateObject : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 90f; // grade pe secundă

    void Update()
    {
        // rotim pe axa Z
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }
}
