using UnityEngine;
using UnityEditor;

public class TimeControlWindow : EditorWindow
{
    private float timeScale = 1.0f;

    [MenuItem("Tools/Time Control Debugger")]
    public static void ShowWindow()
    {
        GetWindow<TimeControlWindow>("Time Control");
    }

    private void OnGUI()
    {
        GUILayout.Label("Control Timp (Debug)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Afișăm valoarea curentă a timeScale
        EditorGUILayout.LabelField("Time Scale curent:", Time.timeScale.ToString("F2"));

        // Slider pentru control manual (între 0 și 10x viteză)
        timeScale = EditorGUILayout.Slider("Viteză:", timeScale, 0.0f, 10.0f);

        if (GUILayout.Button("Aplică Viteza Selectată"))
        {
            Time.timeScale = timeScale;
        }

        EditorGUILayout.Space();
        GUILayout.BeginHorizontal();

        // Butoane rapide (Preset-uri)
        if (GUILayout.Button("Pauză (0x)")) { ApplyTimeScale(0f); }
        if (GUILayout.Button("Normal (1x)")) { ApplyTimeScale(1f); }
        if (GUILayout.Button("Fast (2x)")) { ApplyTimeScale(2f); }
        if (GUILayout.Button("Ultra (5x)")) { ApplyTimeScale(5f); }

        GUILayout.EndHorizontal();

        EditorGUILayout.Space();
        if (GUILayout.Button("RESET (1x)", GUILayout.Height(30)))
        {
            ApplyTimeScale(1f);
        }

        EditorGUILayout.HelpBox("Atenție: Time Scale afectează FixedUpdate (fizica) și animațiile care folosesc timpul normal.", MessageType.Info);
    }

    private void ApplyTimeScale(float value)
    {
        timeScale = value;
        Time.timeScale = value;
    }

    // Actualizăm fereastra constant pentru a vedea modificările dacă sunt făcute din alte scripturi
    private void OnInspectorUpdate()
    {
        Repaint();
    }
}