using UnityEngine;

public class IdleCameraShake : MonoBehaviour
{
    [Header("Shake Settings")]
    public float positionAmplitude = 0.05f;   // cât de mult se mișcă pe poziție
    public float rotationAmplitude = 0.5f;    // cât de mult se mișcă pe rotație (grade)
    public float frequency = 1f;              // cât de repede oscilează

    private Vector3 startPos;
    private Quaternion startRot;

    void Start()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;
    }

    void Update()
    {
        float t = Time.time * frequency;

        // offset mic pe poziție
        Vector3 posOffset = new Vector3(
            Mathf.Sin(t) * positionAmplitude,
            Mathf.Cos(t * 0.8f) * positionAmplitude,
            0f
        );

        // offset mic pe rotație
        Quaternion rotOffset = Quaternion.Euler(
            Mathf.Sin(t * 0.7f) * rotationAmplitude,
            Mathf.Cos(t) * rotationAmplitude,
            0f
        );

        // aplicăm smooth între poziția/rotația de bază și cu offset
        transform.localPosition = Vector3.Lerp(transform.localPosition, startPos + posOffset, Time.deltaTime * 2f);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, startRot * rotOffset, Time.deltaTime * 2f);
    }
}
