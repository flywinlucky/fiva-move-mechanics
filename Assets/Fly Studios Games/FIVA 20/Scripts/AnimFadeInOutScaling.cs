using System.Collections;
using UnityEngine;

public class AnimFadeInOutScaling : MonoBehaviour
{
    [Header("Animation Settings")]
    public float duration = 0.25f;      // Durata animației
    public float bounceForce = 1.15f;   // Intensitatea efectului de bounce

    private Vector3 initialScale;       // Scara finală dorită
    private Coroutine activeRoutine;

    private void Awake()
    {
        // Salvăm scara setată în Unity ca punct de destinație
        initialScale = transform.localScale;
    }

    private void OnEnable()
    {
        // Resetăm scara la zero imediat
        transform.localScale = Vector3.zero;
        
        // Oprim orice animație anterioară și pornim una nouă
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(ScaleInRoutine());
    }

    private IEnumerator ScaleInRoutine()
    {
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float percent = elapsed / duration;

            /* Formula de Bounce (Overshoot):
               Lerp merge de la 0 la 1, iar Sinusul adaugă acea "săritură" 
               peste valoarea finală care revine apoi la punct fix.
            */
            float bounce = Mathf.Sin(percent * Mathf.PI);
            float scaleAmount = percent + (bounce * (bounceForce - 1f));

            transform.localScale = initialScale * scaleAmount;
            yield return null;
        }

        // Ne asigurăm că la final scara este exact cea inițială
        transform.localScale = initialScale;
        activeRoutine = null;
    }
}