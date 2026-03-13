using UnityEngine;

public class FYGDontDestroyOnLoad : MonoBehaviour
{
    private static FYGDontDestroyOnLoad instance;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // Păstrează obiectul în toate scenele
        }
        else if (instance != this)
        {
            Destroy(gameObject); // Distruge orice instanță nouă creată
        }
    }
}
