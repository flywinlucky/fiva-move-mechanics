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
		private const float DefaultVerticalOffset = 1f;
		private static readonly string[] PlayZoneNames =
		{
			"ToolbarZonePlayModes",
			"ToolbarZonePlayMode",
			"ToolbarZonePlay"
		};

		private static readonly Type ToolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");

		private static readonly GUIContent SourceBuildContent = new GUIContent("Build", "List scenes from Build Settings");
		private static readonly GUIContent SourceAllContent = new GUIContent("All", "List all scenes in the project");
		private static readonly GUIContent NoScenesContent = new GUIContent("No Scenes", "No scenes found for selected source");
		private static readonly GUIContent LockedContent = new GUIContent("Locked", "Scene switching is disabled in Play Mode");
		private const float SourceWidth = 52f;
		private const float MinScenePopupWidth = 90f;
		private const float MaxScenePopupWidth = 150f;
		private const float LockedLabelWidth = 46f;

		private static readonly List<SceneItem> SceneCache = new List<SceneItem>();

		private static ScriptableObject _toolbar;
		private static VisualElement _toolbarRoot;
		private static VisualElement _playHostZone;
		private static IMGUIContainer _toolbarContainer;
		private static bool _isDirty = true;
		private static SceneSource _sceneSource;
		private static float _scenePopupWidth = 110f;

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
			UpdateOverlayPosition();
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

			VisualElement hostZone = FindPlayHostZone(root);
			if (hostZone == null)
				return;

			_toolbarRoot = root;
			_playHostZone = hostZone;

			VisualElement existing = root.Q(ContainerName);
			if (existing != null)
				existing.RemoveFromHierarchy();

			_toolbarContainer = new IMGUIContainer(DrawToolbarGUI)
			{
				name = ContainerName
			};

			_toolbarContainer.style.marginLeft = 1;
			_toolbarContainer.style.marginRight = 0;
			_toolbarContainer.style.width = 190;
			_toolbarContainer.style.flexShrink = 0;
			_toolbarContainer.style.position = Position.Absolute;
			_toolbarContainer.style.top = 0;
			_toolbarContainer.style.height = 22;
			_toolbarContainer.style.unityTextAlign = TextAnchor.MiddleLeft;

			root.Add(_toolbarContainer);
			UpdateOverlayPosition();
		}

		private static void UpdateOverlayPosition()
		{
			if (_toolbarContainer == null || _toolbarRoot == null || _playHostZone == null)
				return;

			Rect playWorldRect = _playHostZone.worldBound;
			if (playWorldRect.width <= 0f)
				return;

			Vector2 playLocalTopLeft = _toolbarRoot.WorldToLocal(new Vector2(playWorldRect.xMin, playWorldRect.yMin));
			float targetLeft = playLocalTopLeft.x - _toolbarContainer.resolvedStyle.width - 4f;
			if (targetLeft < 0f)
				targetLeft = 0f;

			float containerHeight = _toolbarContainer.resolvedStyle.height;
			if (containerHeight <= 0f)
				containerHeight = 22f;

			float targetTop = playLocalTopLeft.y + ((playWorldRect.height - containerHeight) * 0.5f);

			_toolbarContainer.style.left = targetLeft;
			_toolbarContainer.style.top = targetTop + DefaultVerticalOffset;
		}

		private static VisualElement FindPlayHostZone(VisualElement root)
		{
			for (int i = 0; i < PlayZoneNames.Length; i++)
			{
				VisualElement zone = root.Q(PlayZoneNames[i]);
				if (zone != null)
					return zone;
			}

			List<VisualElement> allElements = root.Query<VisualElement>().ToList();
			for (int i = 0; i < allElements.Count; i++)
			{
				string name = allElements[i].name;
				if (!string.IsNullOrEmpty(name) && name.IndexOf("Play", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("ToolbarZone", StringComparison.OrdinalIgnoreCase) >= 0)
					return allElements[i];
			}

			return root.Q("ToolbarZoneLeftAlign");
		}

		private static void DrawToolbarGUI()
		{
			RefreshSceneCacheIfNeeded();

			bool isLocked = EditorApplication.isPlayingOrWillChangePlaymode;
			UpdateCompactWidths(isLocked);
			using (new EditorGUI.DisabledScope(isLocked))
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					DrawSourceDropdown();
					DrawSceneDropdown();
				}
			}

			if (isLocked)
				GUILayout.Label(LockedContent, EditorStyles.miniLabel, GUILayout.Width(LockedLabelWidth));
		}

		private static void UpdateCompactWidths(bool isLocked)
		{
			_scenePopupWidth = CalculateScenePopupWidth();

			float desiredWidth = SourceWidth + _scenePopupWidth + 6f;
			if (isLocked)
				desiredWidth += LockedLabelWidth;

			if (_toolbarContainer != null)
				_toolbarContainer.style.width = desiredWidth;
		}

		private static float CalculateScenePopupWidth()
		{
			if (SceneCache.Count == 0)
			{
				float noSceneWidth = EditorStyles.miniLabel.CalcSize(NoScenesContent).x + 8f;
				return Mathf.Clamp(noSceneWidth, MinScenePopupWidth, MaxScenePopupWidth);
			}

			int currentSceneIndex = GetActiveSceneIndex();
			int shownIndex = Mathf.Clamp(currentSceneIndex, 0, SceneCache.Count - 1);
			string title = SceneCache[shownIndex].Name;
			float popupWidth = EditorStyles.toolbarPopup.CalcSize(new GUIContent(title)).x + 24f;
			return Mathf.Clamp(popupWidth, MinScenePopupWidth, MaxScenePopupWidth);
		}

		private static void DrawSourceDropdown()
		{
			GUIContent sourceContent = _sceneSource == SceneSource.BuildSettings ? SourceBuildContent : SourceAllContent;
			if (!EditorGUILayout.DropdownButton(sourceContent, FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(SourceWidth)))
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
				GUILayout.Label(NoScenesContent, EditorStyles.miniLabel, GUILayout.Width(_scenePopupWidth));
				return;
			}

			int currentSceneIndex = GetActiveSceneIndex();
			string[] sceneNames = SceneCache.Select(scene => scene.Name).ToArray();

			int shownIndex = Mathf.Clamp(currentSceneIndex, 0, sceneNames.Length - 1);
			EditorGUI.BeginChangeCheck();
			int newIndex = EditorGUILayout.Popup(shownIndex, sceneNames, EditorStyles.toolbarPopup, GUILayout.Width(_scenePopupWidth));
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
