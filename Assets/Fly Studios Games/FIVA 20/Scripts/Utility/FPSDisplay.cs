using UnityEngine;
using UnityEngine.UI;
public class FPSDisplay : MonoBehaviour
{
    public Text fpsText; // Trage aici obiectul de text din Canvas
    public float updateInterval = 0.5f; // Cât de des să se actualizeze textul

    private float _accumulatedTime = 0f;
    private int _frameCount = 0;
    private float _timeLeft;

    void Start()
    {
        if (fpsText == null)
        {
            fpsText = GetComponent<Text>();
        }
        _timeLeft = updateInterval;
    }

    void Update()
    {
        _timeLeft -= Time.unscaledDeltaTime;
        _accumulatedTime += Time.unscaledDeltaTime;
        _frameCount++;

        // Actualizăm textul doar la intervalul setat pentru a fi lizibil
        if (_timeLeft <= 0.0)
        {
            float fps = _frameCount / _accumulatedTime;
            fpsText.text = string.Format("FPS: {0:F0}", fps);

            // Resetăm contoarele
            _timeLeft = updateInterval;
            _accumulatedTime = 0f;
            _frameCount = 0;

            // Opțional: Schimbăm culoarea în funcție de performanță
            if (fps < 30) fpsText.color = Color.red;
            else if (fps < 60) fpsText.color = Color.yellow;
            else fpsText.color = Color.green;
        }
    }
}