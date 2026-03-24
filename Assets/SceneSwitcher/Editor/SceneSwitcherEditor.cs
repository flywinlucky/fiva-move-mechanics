using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SceneSwitcher
{
	public class SceneSwitcherEditor : EditorWindow
	{
		private const string SourcePrefKey = "SceneSwitcher.SceneSource";
		private const string CompanyName = "Fly Studios Games";
		private const string AssetStoreUrl = "https://assetstore.unity.com/";

		private static readonly GUIContent WindowTitle = new GUIContent("Scene Switcher");
		private static readonly GUIContent[] SourceOptions =
		{
			new GUIContent("Build Settings"),
			new GUIContent("All Project")
		};

		private static List<SceneEntry> m_Scenes = new List<SceneEntry>();
		private static Vector2 ScrollPos;
		private static string SearchString = string.Empty;

		private static SceneSwitcherEditor Window;
		private static bool m_OpenInSceneView = true;
		private static bool m_CloseInSceneView;
		private static bool m_IsMinimized;
		private static bool m_IsDirty = true;

		private static SceneSource m_SceneSource;
		private enum SceneSource
		{
			BuildSettings = 0,
			AllProject = 1
		}

		private class SceneEntry
		{
			public string filePath;
			public string sceneName;
			public bool inBuildSettings;
			public bool enabledInBuild;
		}
		
		[MenuItem ("Window/SceneSwitcher/Open Dockable Window")]
		private static void Init ()
		{
			Window = GetWindow<SceneSwitcherEditor>("Scene Switcher");
			Vector2 minSize = new Vector2(320, 220);
			Window.minSize = minSize;
			Window.Show();
		}

		private void OnEnable()
		{
			LoadPreferences();
			MarkDirty();

			EditorBuildSettings.sceneListChanged += MarkDirty;
			EditorApplication.projectChanged += MarkDirty;
		}

		private void OnDisable()
		{
			EditorBuildSettings.sceneListChanged -= MarkDirty;
			EditorApplication.projectChanged -= MarkDirty;
		}

		[MenuItem("Window/SceneSwitcher/Open SceneView", true)]
		private static bool ValidateOpenSceneView() { return m_OpenInSceneView; }
		[MenuItem("Window/SceneSwitcher/Open SceneView")]
		private static void OpenSceneView()
		{
			LoadPreferences();
			m_OpenInSceneView = false;
			m_CloseInSceneView = true;
			SceneView.onSceneGUIDelegate += OnScene;
			SceneView.RepaintAll();
		}

		[MenuItem("Window/SceneSwitcher/Close SceneView", true)]
		private static bool ValidateCloseSceneView() { return m_CloseInSceneView; }
		[MenuItem("Window/SceneSwitcher/Close SceneView")]
		private static void CloseSceneView()
		{
			m_OpenInSceneView = true;
			m_CloseInSceneView = false;
			SceneView.onSceneGUIDelegate -= OnScene;
			SceneView.RepaintAll();
		}
		
		private void OnGUI ()
		{
			LoadPreferences();
			RefreshSceneCacheIfNeeded();

			DrawWindowContent();
		}

		private static void OnScene(SceneView sceneView)
		{
			Handles.BeginGUI();
			RefreshSceneCacheIfNeeded();

			if (GUI.Button(new Rect(10, 10, 260, 18), "Scene Switcher", EditorStyles.miniButton))
				m_IsMinimized = !m_IsMinimized;

			if (!m_IsMinimized)
			{
				GUILayout.BeginArea(new Rect(10, 30, 340, 220), "Scene Switcher", GUI.skin.window);
				DrawToolbar(compact: true);
				DrawSceneList(compact: true);
				GUILayout.EndArea();
			}

			sceneView.Repaint();
			Handles.EndGUI();
		}

		private static void DrawWindowContent()
		{
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				DrawHeader();
				DrawToolbar(compact: false);
				DrawSceneList(compact: false);
				GUILayout.FlexibleSpace();
				DrawFooterWatermark();
			}
		}

		private static void DrawHeader()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Label(WindowTitle, EditorStyles.boldLabel);
				GUILayout.FlexibleSpace();
				GUILayout.Label($"Scenes: {m_Scenes.Count}", EditorStyles.miniLabel);
			}

			EditorGUILayout.Space(2);
		}

		private static void DrawToolbar(bool compact)
		{
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUI.BeginChangeCheck();
					int sourceIndex = GUILayout.Toolbar((int)m_SceneSource, SourceOptions, EditorStyles.miniButton);
					if (EditorGUI.EndChangeCheck())
					{
						m_SceneSource = (SceneSource)Mathf.Clamp(sourceIndex, 0, SourceOptions.Length - 1);
						SavePreferences();
						MarkDirty();
					}

					if (GUILayout.Button("Refresh", GUILayout.Width(70)))
					{
						MarkDirty();
						RefreshSceneCacheIfNeeded(force: true);
					}
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.LabelField("Search", GUILayout.Width(50));
					SearchString = EditorGUILayout.TextField(SearchString);

					if (GUILayout.Button("X", GUILayout.Width(22)))
						SearchString = string.Empty;
				}
			}
		}

		private static void DrawSceneList(bool compact)
		{
			IEnumerable<SceneEntry> filteredScenes = m_Scenes;
			if (!string.IsNullOrEmpty(SearchString))
				filteredScenes = filteredScenes.Where(s => StringContains(s.sceneName, SearchString));

			List<SceneEntry> sceneList = filteredScenes.ToList();
			if (sceneList.Count == 0)
			{
				EditorGUILayout.HelpBox("No scenes found for the selected source.", MessageType.Info);
				return;
			}

			float rowHeight = compact ? 22.0f : 24.0f;
			float contentHeight = Mathf.Max(60.0f, (sceneList.Count * rowHeight) + 6.0f);
			float maxHeight = Window != null ? Mathf.Max(80.0f, Window.position.height - (compact ? 84.0f : 130.0f)) : 240.0f;
			bool shouldScroll = contentHeight > maxHeight;

			if (shouldScroll)
				ScrollPos = GUILayout.BeginScrollView(ScrollPos, GUILayout.Height(maxHeight));

			foreach (SceneEntry scene in sceneList)
			{
				using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
				{
					string status = GetSceneStatus(scene.filePath);
					string label = string.IsNullOrEmpty(status)
						? scene.sceneName
						: $"[{status}] {scene.sceneName}";

					GUILayout.Label(new GUIContent(label, scene.filePath), EditorStyles.label);
					GUILayout.FlexibleSpace();

					if (m_SceneSource == SceneSource.AllProject && scene.inBuildSettings)
					{
						GUIContent buildState = new GUIContent(scene.enabledInBuild ? "Build" : "Build (Off)");
						GUILayout.Label(buildState, EditorStyles.miniLabel, GUILayout.Width(62));
					}

					if (GUILayout.Button("Ping", GUILayout.Width(42)))
						PingScene(scene.filePath);

					if (GUILayout.Button("Load", GUILayout.Width(44)))
						OpenScene(scene.filePath);

					if (!compact && GUILayout.Button("Delete", GUILayout.Width(55)))
						DeleteScene(scene.filePath);
				}
			}

			if (shouldScroll)
				GUILayout.EndScrollView();
		}

		private static void DrawFooterWatermark()
		{
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				GUIStyle companyStyle = new GUIStyle(EditorStyles.boldLabel)
				{
					alignment = TextAnchor.MiddleCenter
				};

				GUIStyle hintStyle = new GUIStyle(EditorStyles.miniLabel)
				{
					alignment = TextAnchor.MiddleCenter,
					wordWrap = true
				};

				GUILayout.Label(CompanyName, companyStyle);
				GUILayout.Label("Professional tools by " + CompanyName, hintStyle);

				using (new EditorGUILayout.HorizontalScope())
				{
					GUILayout.FlexibleSpace();
					if (GUILayout.Button("Visit Our Asset Store", GUILayout.Height(22), GUILayout.MaxWidth(220)))
						Application.OpenURL(AssetStoreUrl);
					GUILayout.FlexibleSpace();
				}
			}
		}
		
		private static void RefreshSceneCacheIfNeeded(bool force = false)
		{
			if (!force && !m_IsDirty)
				return;

			m_Scenes = GetScenes();
			m_IsDirty = false;
		}

		private static List<SceneEntry> GetScenes()
		{
			Dictionary<string, EditorBuildSettingsScene> buildSceneLookup = EditorBuildSettings.scenes
				.ToDictionary(scene => scene.path, scene => scene, StringComparer.OrdinalIgnoreCase);

			IEnumerable<string> paths;
			if (m_SceneSource == SceneSource.BuildSettings)
				paths = EditorBuildSettings.scenes.Select(s => s.path);
			else
			{
				string[] guids = AssetDatabase.FindAssets("t:Scene");
				paths = guids.Select(AssetDatabase.GUIDToAssetPath);
			}

			List<SceneEntry> tempScenes = new List<SceneEntry>();
			foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (string.IsNullOrEmpty(path))
					continue;

				SceneEntry entry = new SceneEntry
				{
					filePath = path,
					sceneName = Path.GetFileNameWithoutExtension(path),
					inBuildSettings = buildSceneLookup.TryGetValue(path, out EditorBuildSettingsScene buildScene),
					enabledInBuild = buildSceneLookup.TryGetValue(path, out buildScene) && buildScene.enabled
				};

				tempScenes.Add(entry);
			}

			return tempScenes.OrderBy(list => list.sceneName).ToList();
		}
		
		private static void OpenScene(string filePath)
		{
			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
				return;

			EditorSceneManager.OpenScene(filePath, OpenSceneMode.Single);
		}

		private static void DeleteScene(string filePath)
		{
			if (!EditorUtility.DisplayDialog("Delete Scene?", "Are you sure you want to delete this scene?\n\nFile: " + filePath, "YES", "NO"))
				return;

			if (!AssetDatabase.DeleteAsset(filePath))
				EditorUtility.DisplayDialog("Delete failed", "Could not delete scene:\n" + filePath, "OK");

			MarkDirty();
		}

		private static void PingScene(string filePath)
		{
			UnityEngine.Object sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(filePath);
			if (sceneAsset != null)
				EditorGUIUtility.PingObject(sceneAsset);
		}

		private static string GetSceneStatus(string filePath)
		{
			Scene activeScene = SceneManager.GetActiveScene();
			if (string.Equals(activeScene.path, filePath, StringComparison.OrdinalIgnoreCase))
				return "ACTIVE";

			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				Scene loadedScene = SceneManager.GetSceneAt(i);
				if (string.Equals(loadedScene.path, filePath, StringComparison.OrdinalIgnoreCase))
					return "LOADED";
			}

			return string.Empty;
		}

		private static void MarkDirty()
		{
			m_IsDirty = true;
			if (Window != null)
				Window.Repaint();
		}

		private static void SavePreferences()
		{
			EditorPrefs.SetInt(SourcePrefKey, (int)m_SceneSource);
		}

		private static void LoadPreferences()
		{
			if (!Enum.IsDefined(typeof(SceneSource), EditorPrefs.GetInt(SourcePrefKey, 0)))
				EditorPrefs.SetInt(SourcePrefKey, 0);

			m_SceneSource = (SceneSource)EditorPrefs.GetInt(SourcePrefKey, 0);
		}
		
		private static bool StringContains(string _source, string _compareTo)
		{
			return _source.IndexOf(_compareTo, StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}