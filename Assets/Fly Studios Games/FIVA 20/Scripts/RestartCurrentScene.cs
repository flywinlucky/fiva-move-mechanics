using UnityEngine;
using UnityEngine.SceneManagement; 

public class RestartCurrentScene : MonoBehaviour
{
    public void RestartGame()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
    }
}