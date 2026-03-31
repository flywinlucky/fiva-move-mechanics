using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RotateInspect : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = -0.5f;   // viteza de rotație
    public float smoothTime = 0.2f;    // cât de smooth să fie rotația

    private bool isDragging = false;
    private float targetRotationY;
    private float currentVelocity;

    public Camera mainCamera;

    void Start()
    {
        targetRotationY = transform.eulerAngles.y;
    }

    void Update()
    {
        // verificăm input-ul de mouse
        if (Input.GetMouseButtonDown(0))
        {
            // raycast pe obiect
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    isDragging = true;
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        if (isDragging)
        {
            float mouseX = Input.GetAxis("Mouse X");
            targetRotationY += mouseX * rotationSpeed * 10f; // adunăm rotație în funcție de mișcare
        }

        // interpolare smooth spre rotația dorită
        float newY = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotationY, ref currentVelocity, smoothTime);
        transform.rotation = Quaternion.Euler(0f, newY, 0f);
    }
}
