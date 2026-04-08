using UnityEngine;
using UnityEngine.SceneManagement; 

public class RestartCurrentScene : MonoBehaviour
{
    void SaveUserDataBeforeSceneLoad()
    {
        if (UserData.Instance != null)
            UserData.Instance.Save();
    }

    public void RestartGame()
    {
        SaveUserDataBeforeSceneLoad();
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
    }

        public void SwitchScene(string sceneName)
    {
        SaveUserDataBeforeSceneLoad();
        SceneManager.LoadScene(sceneName);
    }
}