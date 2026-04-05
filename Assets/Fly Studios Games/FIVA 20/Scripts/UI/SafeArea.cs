using UnityEngine;

/// <summary>
/// Un script profesional și optimizat pentru a ajusta un RectTransform la zona sigură a ecranului.
/// Ideal pentru dispozitivele cu notch-uri, insule dinamice sau bare de sistem.
/// Atașează acest script la elementul UI principal (de obicei, un panou care conține restul UI-ului).
/// </summary>
[RequireComponent(typeof(RectTransform))] // Asigură că acest script este atașat doar la obiecte cu RectTransform
public class SafeArea : MonoBehaviour
{
    private RectTransform _panel;
    private Rect _lastSafeArea = new Rect(0, 0, 0, 0); // Stocăm ultima zonă sigură cunoscută pentru a evita actualizări inutile
    private int _lastScreenWidth;
    private int _lastScreenHeight;
    private ScreenOrientation _lastOrientation;
    private float _nextCheckTime;

    [SerializeField]
    [Min(0.05f)]
    private float _checkInterval = 0.2f;

    void Awake()
    {
        _panel = GetComponent<RectTransform>();
        if (_panel == null)
        {
            Debug.LogError("Safe Area script requires a RectTransform component.", this);
            this.enabled = false;
            return;
        }

        // Aplicăm zona sigură de la început
        ApplySafeArea();
        _nextCheckTime = Time.unscaledTime;
    }

    void Update()
    {
        if (Time.unscaledTime < _nextCheckTime)
            return;

        _nextCheckTime = Time.unscaledTime + Mathf.Max(0.05f, _checkInterval);

        // Verificăm dacă zona sigură s-a schimbat (de ex., la rotirea ecranului)
        // Această verificare este foarte rapidă și previne rularea logicii în fiecare frame
        bool screenChanged = Screen.width != _lastScreenWidth
            || Screen.height != _lastScreenHeight
            || Screen.orientation != _lastOrientation;

        if (screenChanged || Screen.safeArea != _lastSafeArea)
        {
            ApplySafeArea();
        }
    }

    /// <summary>
    /// Calculează și aplică ajustările necesare pentru a se potrivi în zona sigură.
    /// </summary>
    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;

        // Convertim pixelii din zona sigură în valori normalizate (0-1) pentru ancore
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        // Setăm ancorele RectTransform-ului pentru a se potrivi exact cu zona sigură
        _panel.anchorMin = anchorMin;
        _panel.anchorMax = anchorMax;

        // Actualizăm ultima zonă sigură cunoscută
        _lastSafeArea = safeArea;
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        _lastOrientation = Screen.orientation;
    }
}