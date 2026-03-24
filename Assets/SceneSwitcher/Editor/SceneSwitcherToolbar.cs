using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SceneSwitcher
{
	[InitializeOnLoad]
	internal static class SceneSwitcherToolbar
	{
		private const string SourcePrefKey = "SceneSwitcher.Toolbar.SceneSource";
		private const string RootFieldName = "m_Root";
		private const string ContainerName = "SceneSwitcherToolbarContainer";

		private static readonly Type ToolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");

		private static readonly GUIContent SourceBuildContent = new GUIContent("Build", "List scenes from Build Settings");
		private static readonly GUIContent SourceAllContent = new GUIContent("All", "List all scenes in the project");
		private static readonly GUIContent NoScenesContent = new GUIContent("No Scenes", "No scenes found for selected source");
		private static readonly GUIContent LockedContent = new GUIContent("Locked", "Scene switching is disabled in Play Mode");

		private static readonly List<SceneItem> SceneCache = new List<SceneItem>();

		private static ScriptableObject _toolbar;
		private static IMGUIContainer _toolbarContainer;
		private static bool _isDirty = true;
		private static SceneSource _sceneSource;

		private enum SceneSource
		{
			BuildSettings = 0,
			AllProject = 1
		}

		private class SceneItem
		{
			public string Name;
			public string Path;
		}

		static SceneSwitcherToolbar()
		{
			LoadPreferences();

			EditorApplication.update += OnEditorUpdate;
			EditorApplication.projectChanged += MarkDirty;
			EditorBuildSettings.sceneListChanged += MarkDirty;
			EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
		}

		private static void OnEditorUpdate()
		{
			EnsureToolbarInjected();
		}

		private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
		{
			if (_toolbarContainer != null)
				_toolbarContainer.MarkDirtyRepaint();
		}

		private static void EnsureToolbarInjected()
		{
			if (ToolbarType == null)
				return;

			UnityEngine.Object[] toolbars = Resources.FindObjectsOfTypeAll(ToolbarType);
			if (toolbars == null || toolbars.Length == 0)
				return;

			ScriptableObject currentToolbar = toolbars[0] as ScriptableObject;
			if (currentToolbar == null)
				return;

			if (_toolbar == currentToolbar && _toolbarContainer != null)
				return;

			_toolbar = currentToolbar;
			InjectIntoToolbar();
		}

		private static void InjectIntoToolbar()
		{
			FieldInfo rootField = ToolbarType.GetField(RootFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
			if (rootField == null)
				return;

			VisualElement root = rootField.GetValue(_toolbar) as VisualElement;
			if (root == null)
				return;

			VisualElement hostZone = root.Q("ToolbarZonePlayModes") ?? root.Q("ToolbarZoneLeftAlign");
			if (hostZone == null)
				return;

			VisualElement existing = hostZone.Q(ContainerName);
			if (existing != null)
				existing.RemoveFromHierarchy();

			_toolbarContainer = new IMGUIContainer(DrawToolbarGUI)
			{
				name = ContainerName
			};

			_toolbarContainer.style.marginLeft = 6;
			_toolbarContainer.style.marginRight = 4;
			_toolbarContainer.style.width = 255;
			_toolbarContainer.style.flexShrink = 0;

			hostZone.Add(_toolbarContainer);
		}

		private static void DrawToolbarGUI()
		{
			RefreshSceneCacheIfNeeded();

			bool isLocked = EditorApplication.isPlayingOrWillChangePlaymode;
			using (new EditorGUI.DisabledScope(isLocked))
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					DrawSourceDropdown();
					DrawSceneDropdown();
				}
			}

			if (isLocked)
				GUILayout.Label(LockedContent, EditorStyles.miniLabel, GUILayout.Width(46));
		}

		private static void DrawSourceDropdown()
		{
			GUIContent sourceContent = _sceneSource == SceneSource.BuildSettings ? SourceBuildContent : SourceAllContent;
			if (!EditorGUILayout.DropdownButton(sourceContent, FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(52)))
				return;

			GenericMenu menu = new GenericMenu();
			menu.AddItem(new GUIContent("Build Settings"), _sceneSource == SceneSource.BuildSettings, () => SetSceneSource(SceneSource.BuildSettings));
			menu.AddItem(new GUIContent("All Project"), _sceneSource == SceneSource.AllProject, () => SetSceneSource(SceneSource.AllProject));
			menu.ShowAsContext();
		}

		private static void DrawSceneDropdown()
		{
			if (SceneCache.Count == 0)
			{
				GUILayout.Label(NoScenesContent, EditorStyles.miniLabel, GUILayout.Width(180));
				return;
			}

			int currentSceneIndex = GetActiveSceneIndex();
			string[] sceneNames = SceneCache.Select(scene => scene.Name).ToArray();

			int shownIndex = Mathf.Clamp(currentSceneIndex, 0, sceneNames.Length - 1);
			EditorGUI.BeginChangeCheck();
			int newIndex = EditorGUILayout.Popup(shownIndex, sceneNames, EditorStyles.toolbarPopup, GUILayout.Width(195));
			if (!EditorGUI.EndChangeCheck())
				return;

			if (newIndex == currentSceneIndex)
				return;

			OpenScene(SceneCache[newIndex].Path);
		}

		private static void OpenScene(string path)
		{
			if (string.IsNullOrEmpty(path))
				return;

			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
				return;

			EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
		}

		private static int GetActiveSceneIndex()
		{
			string activePath = SceneManager.GetActiveScene().path;
			for (int i = 0; i < SceneCache.Count; i++)
			{
				if (string.Equals(SceneCache[i].Path, activePath, StringComparison.OrdinalIgnoreCase))
					return i;
			}

			return -1;
		}

		private static void RefreshSceneCacheIfNeeded()
		{
			if (!_isDirty)
				return;

			SceneCache.Clear();

			IEnumerable<string> scenePaths;
			if (_sceneSource == SceneSource.BuildSettings)
				scenePaths = EditorBuildSettings.scenes.Select(scene => scene.path);
			else
				scenePaths = AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath);

			foreach (string scenePath in scenePaths.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (string.IsNullOrEmpty(scenePath))
					continue;

				SceneCache.Add(new SceneItem
				{
					Path = scenePath,
					Name = System.IO.Path.GetFileNameWithoutExtension(scenePath)
				});
			}

			SceneCache.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
			_isDirty = false;

			if (_toolbarContainer != null)
				_toolbarContainer.MarkDirtyRepaint();
		}

		private static void SetSceneSource(SceneSource source)
		{
			if (_sceneSource == source)
				return;

			_sceneSource = source;
			SavePreferences();
			MarkDirty();
		}

		private static void MarkDirty()
		{
			_isDirty = true;
			if (_toolbarContainer != null)
				_toolbarContainer.MarkDirtyRepaint();
		}

		private static void SavePreferences()
		{
			EditorPrefs.SetInt(SourcePrefKey, (int)_sceneSource);
		}

		private static void LoadPreferences()
		{
			int source = EditorPrefs.GetInt(SourcePrefKey, 0);
			if (!Enum.IsDefined(typeof(SceneSource), source))
				source = 0;

			_sceneSource = (SceneSource)source;
		}
	}
}
