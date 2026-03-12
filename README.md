# Persistent Components - Unity 2022.3 Compatibility Notes

This document summarizes the changes made to adapt this plugin for Unity 2022.3 and to restore its main runtime persistence workflow.

## Overview

The original package had several issues when used in Unity 2022.3:

- obsolete editor play mode API usage
- broken search bar GUI styles in newer Unity versions
- unstable persistence based on instance IDs
- runtime changes were only captured once when entering Play Mode
- components with custom inspectors, such as `VRCPhysBone`, were not reliably updated after runtime edits

The plugin has been updated so that watched component changes made during Play Mode can be restored after exiting Play Mode in Unity 2022.3.

## Main Problems Found

### 1. Obsolete play mode callback

The original implementation used the obsolete `EditorApplication.playmodeStateChanged` event.

This was replaced with the Unity 2022.3 compatible `EditorApplication.playModeStateChanged` callback in [`PersistentComponents()`](PersistentComponents/Editor/PersistentComponents.cs:31) and handled through [`OnPlayModeChanged()`](PersistentComponents/Editor/PersistentComponents.cs:40).

### 2. Search bar GUI failure

The editor window search bar relied on old built-in style names that may not exist in newer Unity versions.

This caused null GUI styles, which then triggered layout errors and exceptions in the editor window.

The search bar was updated in [`SearchUtils.BeginSearchbar()`](PersistentComponents/Editor/Utils/SearchUtils.cs:35) to:

- try the newer toolbar style names first
- fall back to the legacy misspelled names if needed
- fall back again to safe default GUI styles when Unity does not provide the toolbar styles

### 3. Unstable persistence identifiers

The original plugin stored watched components using `GetInstanceID()` values.

That approach is not stable enough for Unity 2022.3 editor lifecycle behavior, especially across play mode transitions and editor reloads.

The persistence system was migrated to use `GlobalObjectId` in:

- [`GetComponentId()`](PersistentComponents/Editor/PersistentComponents.cs:257)
- [`GetComponentById()`](PersistentComponents/Editor/PersistentComponents.cs:265)
- [`WatchComponent()`](PersistentComponents/Editor/PersistentComponentsStateSaving.cs:9)
- [`RecallComponents()`](PersistentComponents/Editor/PersistentComponentsStayPersistent.cs:25)

The saved asset format in [`PersistencyData`](PersistentComponents/Editor/PersistencyData.cs:7) was also changed from `int[]` to `string[]` so stable object identifiers can be stored.

### 4. Runtime changes were not continuously captured

Originally, the plugin only created a snapshot when Play Mode started.

That meant later changes made during Play Mode were not included in the saved snapshot, so exiting Play Mode restored the old values instead of the latest runtime values.

This was the main reason runtime edits appeared to not save.

## Final Fixes Implemented

### Stable watched component storage

Watched components are now tracked with stable string IDs instead of instance IDs.

Relevant changes:

- [`WatchedComponents`](PersistentComponents/Editor/PersistentComponents.cs:21)
- [`components`](PersistentComponents/Editor/PersistentComponents.cs:23)
- [`serializedObjects`](PersistentComponents/Editor/PersistentComponents.cs:24)
- [`persistentComponents`](PersistentComponents/Editor/PersistencyData.cs:9)

### Real target object write-back

When leaving Play Mode, the plugin now writes snapshot data back onto the real target object through a fresh `SerializedObject`, instead of relying on the old cached object alone.

This logic is implemented in [`ApplyModifiedProperties()`](PersistentComponents/Editor/PersistentComponents.cs:57).

The write-back flow now:

- resolves the real target object
- iterates through the stored snapshot properties
- copies matching properties onto the target `SerializedObject`
- applies the modified properties without undo
- marks the target component dirty

### Runtime polling for custom inspector components

Some components do not use the fallback custom inspector path, so inspector change detection alone is not enough.

To support components such as `VRCPhysBone`, a runtime polling mechanism was added:

- [`EditorApplication.update`](PersistentComponents/Editor/PersistentComponents.cs:35)
- [`OnEditorUpdate()`](PersistentComponents/Editor/PersistentComponents.cs:154)
- [`PollWatchedComponents()`](PersistentComponents/Editor/PersistentComponents.cs:166)
- [`BuildSerializedHash()`](PersistentComponents/Editor/PersistentComponents.cs:194)

During Play Mode, watched components are periodically checked for serialized data changes. If a change is detected, the snapshot is refreshed automatically.

This is the key fix that restored the main functionality for components using their own custom inspectors.

### Inspector-based immediate updates for normal components

For components that do go through the fallback custom inspector, the plugin still updates snapshots immediately when inspector values change.

This behavior is handled in [`CustomInspector.OnInspectorGUI()`](PersistentComponents/Editor/CustomInspector.cs:19).

## Additional Stability Improvements

### Safer hierarchy drawing

[`HierarchyItemCallback()`](PersistentComponents/Editor/PersistentComponents.cs:276) now includes null checks so stale entries do not break hierarchy rendering.

### Safer persistency asset lookup

[`PersistencyData.GetAssetLocation()`](PersistentComponents/Editor/PersistencyData.cs:27) now handles missing asset search results more safely and falls back to a default path when needed.

## Files Modified

The following files were updated:

- [`PersistentComponents/Editor/PersistentComponents.cs`](PersistentComponents/Editor/PersistentComponents.cs)
- [`PersistentComponents/Editor/PersistentComponentsStateSaving.cs`](PersistentComponents/Editor/PersistentComponentsStateSaving.cs)
- [`PersistentComponents/Editor/PersistentComponentsStayPersistent.cs`](PersistentComponents/Editor/PersistentComponentsStayPersistent.cs)
- [`PersistentComponents/Editor/PersistentComponentsWindow.cs`](PersistentComponents/Editor/PersistentComponentsWindow.cs)
- [`PersistentComponents/Editor/PersistencyData.cs`](PersistentComponents/Editor/PersistencyData.cs)
- [`PersistentComponents/Editor/Utils/SearchUtils.cs`](PersistentComponents/Editor/Utils/SearchUtils.cs)
- [`PersistentComponents/Editor/CustomInspector.cs`](PersistentComponents/Editor/CustomInspector.cs)

## Result

After these changes:

- the editor window works correctly in Unity 2022.3
- obsolete API warnings are removed
- watched components are stored with stable identifiers
- runtime changes can be detected after Play Mode has already started
- exiting Play Mode restores the latest watched runtime values instead of only the initial snapshot
- the workflow has been verified with `VRCPhysBone`

## Notes

This adaptation keeps the original plugin behavior and structure as much as possible while making it functional in Unity 2022.3.

The most important behavioral change is that runtime persistence is no longer dependent on a single snapshot taken only at Play Mode entry. The plugin now refreshes snapshots when watched serialized data changes during Play Mode.
