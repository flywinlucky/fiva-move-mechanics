# Easy Scene Switcher Pro

Easy Scene Switcher Pro is a lightweight Unity editor tool that helps you switch scenes faster during development.

It provides:
- A dockable scene browser window.
- A compact toolbar dropdown near Play controls.
- Build Settings / All Project scene source modes.
- Safe scene switching with save prompt.

## Installation

1. Copy the `Easy Scene Switcher Pro` folder into your Unity project's `Assets` folder.
2. Let Unity recompile scripts.
3. Open the tool from the menu:
   - `Tools > Easy Scene Switcher Pro > Open Window`

## Features

### 1. Dockable Scene Browser Window

Open from:
- `Tools > Easy Scene Switcher Pro > Open Window`
- `Window > Easy Scene Switcher Pro > Open Window`

Window capabilities:
- Source toggle:
  - `Build Settings` (only scenes in Build Settings)
  - `All Project` (all scenes in project)
- Search box for filtering scenes by name.
- Quick actions per scene:
  - `Ping` (highlight asset in Project window)
  - `Load` (open scene)
  - `Delete` (remove scene asset with confirmation)
- Status badges:
  - `ACTIVE` for active scene
  - `LOADED` for loaded additive scenes
- Footer branding button:
  - `Visit Our Asset Store`

### 2. Toolbar Scene Switcher (Near Play)

A compact scene switcher is injected next to the Play controls in the Unity top toolbar.

Capabilities:
- Source dropdown: `Build` / `All`
- Scene dropdown: quickly open current target scene
- Play Mode lock: interaction is disabled while entering/in Play Mode

### 3. Toolbar ON/OFF Setting

Enable or disable toolbar integration from:
- `Tools > Easy Scene Switcher Pro > Settings > Toolbar Scene Switcher > On`
- `Tools > Easy Scene Switcher Pro > Settings > Toolbar Scene Switcher > Off`

This setting is saved in `EditorPrefs` and persists between editor sessions.

## Behavior Notes

- Scene opening uses `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()`.
- If you have unsaved changes, Unity prompts before switching scenes.
- Toolbar integration is editor-only and uses Unity internal toolbar reflection.

## Folder Structure

```text
Assets/
  Easy Scene Switcher Pro/
    README.md
    Editor/
      SceneSwitcherEditor.cs
      SceneSwitcherToolbar.cs
```

## Technical Overview

- Namespace: `FlyStudiosGames.EasySceneSwitcherPro.Editor`
- Window class: `EasySceneSwitcherWindow`
- Toolbar class: `EasySceneSwitcherToolbar`
- Scene list refresh triggers:
  - `EditorBuildSettings.sceneListChanged`
  - `EditorApplication.projectChanged`
  - Active scene changes in editor

## Troubleshooting

### Toolbar not visible

1. Check setting:
   - `Tools > Easy Scene Switcher Pro > Settings > Toolbar Scene Switcher > On`
2. Trigger a layout refresh:
   - Recompile scripts or reopen Unity.
3. Reset Unity layout if needed.

### No scenes listed

- In `Build Settings` mode: ensure scenes are added to Build Settings.
- In `All Project` mode: ensure scene assets (`*.unity`) exist in project.

### Namespace/type compile conflicts

If you customize code and hit type conflicts with `Editor`, use fully qualified type names like:
- `UnityEditor.Editor`

## Changelog

### 1.0.0
- Initial release
- Dockable scene browser window
- Toolbar scene switcher near Play controls
- Source mode switching (Build / All)
- Toolbar ON/OFF settings

## Support

Publisher: Fly Studios Games

For updates and other tools, use the button in the window footer:
- `Visit Our Asset Store`
