using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class CinematicCameraSystem : MonoBehaviour
{
    [Header("Cameras")]
    public Camera startGameCamera;
    public Camera showGoalCamera;
    public Camera mainCamera;

    [Header("Start Game Shot")]
    public Transform startPoint;
    public Transform endPoint;

    [Header("Goal Shot")]
    public Transform goalStartPoint;
    public Transform goalEndPoint;

    [Header("Timing")]
    [SerializeField]
    [Min(0.1f)]
    float firstShotDuration = 4f;

    [SerializeField]
    [Min(0.1f)]
    float secondShotDuration = 3.5f;

    [SerializeField]
    [Min(0.2f)]
    float durationMultiplier = 1f;

    [SerializeField]
    AnimationCurve moveEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField]
    [Min(0f)]
    float holdAfterFirstShot = 0.2f;

    [SerializeField]
    [Min(0f)]
    float holdAfterSecondShot = 0.15f;

    [SerializeField]
    bool playOnStart = true;

    [Header("Events")]
    public UnityEvent onCinematicStarted;
    public UnityEvent onCinematicFinished;

    Coroutine _sequenceRoutine;
    bool _isPlaying;

    void Start()
    {
        if (playOnStart)
            PlaySequence();
    }

    public void PlaySequence()
    {
        if (_sequenceRoutine != null)
            StopCoroutine(_sequenceRoutine);

        _sequenceRoutine = StartCoroutine(PlaySequenceRoutine());
    }

    public void SkipToMainCamera()
    {
        if (_sequenceRoutine != null)
        {
            StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
        }

        _isPlaying = false;
        SetActiveCamera(mainCamera);
        onCinematicFinished?.Invoke();
    }

    IEnumerator PlaySequenceRoutine()
    {
        _isPlaying = true;
        onCinematicStarted?.Invoke();

        if (startGameCamera != null && startPoint != null && endPoint != null)
        {
            SetActiveCamera(startGameCamera);
            yield return MoveCameraBetweenPoints(startGameCamera, startPoint, endPoint, firstShotDuration);

            if (holdAfterFirstShot > 0f)
                yield return new WaitForSeconds(holdAfterFirstShot);
        }

        if (showGoalCamera != null && goalStartPoint != null && goalEndPoint != null)
        {
            SetActiveCamera(showGoalCamera);
            yield return MoveCameraBetweenPoints(showGoalCamera, goalStartPoint, goalEndPoint, secondShotDuration);

            if (holdAfterSecondShot > 0f)
                yield return new WaitForSeconds(holdAfterSecondShot);
        }

        SetActiveCamera(mainCamera);

        _isPlaying = false;
        _sequenceRoutine = null;
        onCinematicFinished?.Invoke();
    }

    IEnumerator MoveCameraBetweenPoints(Camera cameraToMove, Transform fromPoint, Transform toPoint, float duration)
    {
        if (cameraToMove == null || fromPoint == null || toPoint == null)
            yield break;

        float safeDuration = Mathf.Max(0.01f, duration * Mathf.Max(0.2f, durationMultiplier));
        float elapsed = 0f;

        cameraToMove.transform.SetPositionAndRotation(fromPoint.position, fromPoint.rotation);

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float smoothT = moveEaseCurve != null ? moveEaseCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

            cameraToMove.transform.position = Vector3.LerpUnclamped(fromPoint.position, toPoint.position, smoothT);
            cameraToMove.transform.rotation = Quaternion.SlerpUnclamped(fromPoint.rotation, toPoint.rotation, smoothT);

            yield return null;
        }

        cameraToMove.transform.SetPositionAndRotation(toPoint.position, toPoint.rotation);
    }

    void SetActiveCamera(Camera active)
    {
        SetCameraEnabled(startGameCamera, active == startGameCamera);
        SetCameraEnabled(showGoalCamera, active == showGoalCamera);
        SetCameraEnabled(mainCamera, active == mainCamera);
    }

    void SetCameraEnabled(Camera cameraToToggle, bool enabled)
    {
        if (cameraToToggle == null)
            return;

        if (enabled && !cameraToToggle.gameObject.activeSelf)
            cameraToToggle.gameObject.SetActive(true);

        cameraToToggle.enabled = enabled;

        AudioListener listener = cameraToToggle.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = enabled;

        if (!enabled && cameraToToggle.gameObject.activeSelf)
            cameraToToggle.gameObject.SetActive(false);
    }

    public bool IsPlaying => _isPlaying;
}
