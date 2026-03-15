using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public class EditorRestartCurrentScene : EditorWindow
{
    // Adaugă o opțiune în meniul de sus al Unity
    [MenuItem("Tools/Restart Current Scene %r")] // %r înseamnă scurtătură Ctrl+R
    public static void RestartScene()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Restartul funcționează doar în modul Play!");
            return;
        }

        // Reîncarcă scena care este activă în acest moment
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }
}