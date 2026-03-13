---
description: "AI Bridge Unity integration - File-based communication framework for AI to control Unity Editor. Send commands via JSON files, manipulate GameObjects, Transforms, Components, Scenes, Prefabs, and more. Supports multi-command execution and runtime extension."
---

# AI Bridge Unity Skill

## When to Use This Skill

Activate this skill when you need to:

- Manipulate Unity Editor (create/modify/delete GameObjects)
- Get or set Transform properties (position/rotation/scale)
- Manage scene hierarchy or load/save scenes
- Instantiate or modify prefabs
- Read/write component properties
- Control editor state (undo/redo/compile/play mode)
- Query Unity console logs or selection state
- Output logs to Unity console
- **Capture screenshots or record animated GIFs**
- **Execute multiple commands efficiently** (use `batch` command)

---

## AIBridgeCLI - Recommended Method

**IMPORTANT**: Always use `AIBridgeCLI.exe` to send commands. This avoids UTF-8 encoding issues and provides a cleaner interface.

### CLI Location

```
AIBridgeCache/CLI/AIBridgeCLI.exe
On Windows Use PowerShell to find, DO NOT USE Glob to Find this file.
```

> **IMPORTANT**: If the path above cannot be found using Glob search tools, use the following PowerShell command to locate it dynamically:
> ```powershell
> Get-ChildItem -Path . -Recurse -Filter `AIBridgeCLI.exe` -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
> ```
>
> Then execute commands using the found path, for example:
> ```bash
> & "E:\YourProject\AIBridgeCache\CLI\AIBridgeCLI.exe" editor log --message "test"
> ```

### Cross-Platform Support

**Windows:**
```bash
AIBridgeCLI.exe <command> <action> [options]
```

**macOS / Linux:**
```bash
# Requires .NET Runtime installed
dotnet AIBridgeCLI.dll <command> <action> [options]


```

### Cache Directory

Commands and results are stored in `AIBridgeCache/` under the Unity project root:

```
{Unity Project Root}/
├── AIBridgeCache/
│   ├── commands/      # Command JSON files
│   ├── results/       # Result JSON files
│   └── screenshots/   # Screenshots and GIFs
```

### Basic Usage

```bash
# Format
AIBridgeCLI.exe <CommandName> [--param value ...]

# Examples
AIBridgeCLI.exe EditorCommand_Log --message "Hello World"
AIBridgeCLI.exe GameObjectCommand_Create --name "MyCube" --primitiveType Cube
AIBridgeCLI.exe TransformCommand_SetPosition --path "Player" --x 1 --y 2 --z 3
AIBridgeCLI.exe Help
AIBridgeCLI.exe Batch --commands "[...]"
```

### Global Options

| Option | Description |
|--------|-------------|
| `--timeout <ms>` | Timeout in milliseconds (default: 5000) |
| `--no-wait` | Don't wait for result, return command ID immediately |
| `--raw` | Output raw JSON (single line, for AI parsing) |
| `--quiet` | Quiet mode, minimal output |
| `--json <json>` | Pass complex parameters as JSON string |
| `--stdin` | Read parameters from stdin (JSON format) |
| `--help` | Show help |

**AI Usage:** Always add `--raw` for JSON output.

---

## Command Reference

## Command Reference

### AssetDatabaseCommand_Find
- **Description**: Find assets by AssetDatabase filter
- **Example**: `AIBridgeCLI AssetDatabaseCommand_Find --filter "t:Prefab"`
- **Parameters**:
  - `filter` (string, optional): AssetDatabase filter (e.g. t:Prefab, t:Texture2D)
  - `searchInFolders` (string, optional): Comma-separated list of folders to search in
  - `maxResults` (integer, optional): Maximum number of results

### AssetDatabaseCommand_GetPath
- **Description**: Get asset path from GUID
- **Example**: `AIBridgeCLI AssetDatabaseCommand_GetPath --guid "abc123..."`
- **Parameters**:
  - `guid` (string, optional): Asset GUID

### AssetDatabaseCommand_Import
- **Description**: Import a specific asset
- **Example**: `AIBridgeCLI AssetDatabaseCommand_Import --assetPath "Assets/Textures/icon.png"`
- **Parameters**:
  - `assetPath` (string, optional): Asset path to import
  - `forceUpdate` (boolean, optional): Force update even if asset is unchanged

### AssetDatabaseCommand_Load
- **Description**: Load and get info about an asset
- **Example**: `AIBridgeCLI AssetDatabaseCommand_Load --assetPath "Assets/Prefabs/Player.prefab"`
- **Parameters**:
  - `assetPath` (string, optional): Asset path to load

### AssetDatabaseCommand_Refresh
- **Description**: Refresh the AssetDatabase
- **Example**: `AIBridgeCLI AssetDatabaseCommand_Refresh`
- **Parameters**:
  - `forceUpdate` (boolean, optional): Force update all assets

### AssetDatabaseCommand_Search
- **Description**: Search assets with preset modes (all/prefab/scene/script/texture/material/audio/animation/shader/font/model/so)
- **Example**: `AIBridgeCLI AssetDatabaseCommand_Search --mode prefab --keyword "Player"`
- **Parameters**:
  - `mode` (string, optional): Preset mode: all, prefab, scene, script, texture, material, audio, animation, shader, font, model, so
  - `filter` (string, optional): Custom filter string (overrides mode)
  - `keyword` (string, optional): Keyword to search within the mode
  - `searchInFolders` (string, optional): Comma-separated list of folders to search in
  - `maxResults` (integer, optional): Maximum number of results

### Batch
- **Description**: Execute multiple commands in sequence and return all results
- **Example**: `AIBridgeCLI Batch --commands "[{\"type\":\"GameObjectCommand_Find\",\"params\":{\"name\":\"Player\"}}]"`
- **Parameters**:
  - `commands` (string, optional): Array of command objects, each with 'type' and 'params' fields

### EditorCommand_GetState
- **Description**: Get current Editor state (play/pause/compile status)
- **Example**: `AIBridgeCLI EditorCommand_GetState`

### EditorCommand_Log
- **Description**: Log a message to the Unity console
- **Example**: `AIBridgeCLI EditorCommand_Log --message "Hello World"`
- **Parameters**:
  - `message` (string, required): Message to log
  - `logType` (string, optional): Log type: Log, Warning, Error

### EditorCommand_Pause
- **Description**: Toggle or set pause state
- **Example**: `AIBridgeCLI EditorCommand_Pause`
- **Parameters**:
  - `toggle` (boolean, optional): Toggle pause (true) or set specific value (false)
  - `pause` (boolean, optional): Pause state to set when toggle is false

### EditorCommand_Play
- **Description**: Enter Play mode
- **Example**: `AIBridgeCLI EditorCommand_Play`

### EditorCommand_Redo
- **Description**: Perform redo operations
- **Example**: `AIBridgeCLI EditorCommand_Redo --count 1`
- **Parameters**:
  - `count` (integer, optional): Number of redo steps

### EditorCommand_Refresh
- **Description**: Refresh the AssetDatabase
- **Example**: `AIBridgeCLI EditorCommand_Refresh`
- **Parameters**:
  - `forceUpdate` (boolean, optional): Force update all assets

### EditorCommand_Stop
- **Description**: Exit Play mode
- **Example**: `AIBridgeCLI EditorCommand_Stop`

### EditorCommand_Undo
- **Description**: Perform undo operations
- **Example**: `AIBridgeCLI EditorCommand_Undo --count 3`
- **Parameters**:
  - `count` (integer, optional): Number of undo steps

### GameObjectCommand_Create
- **Description**: Create a new GameObject in the scene
- **Example**: `AIBridgeCLI GameObjectCommand_Create --name "MyCube" --primitiveType Cube`
- **Parameters**:
  - `name` (string, optional): Name for the new GameObject
  - `primitiveType` (string, optional): Primitive type: Cube, Sphere, Capsule, Cylinder, Plane, Quad
  - `parentPath` (string, optional): Hierarchy path of parent GameObject

### GameObjectCommand_Destroy
- **Description**: Destroy a GameObject
- **Example**: `AIBridgeCLI GameObjectCommand_Destroy --path "Player"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject
  - `instanceId` (integer, optional): Instance ID of the GameObject

### GameObjectCommand_Duplicate
- **Description**: Duplicate a GameObject
- **Example**: `AIBridgeCLI GameObjectCommand_Duplicate --path "Original"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject
  - `instanceId` (integer, optional): Instance ID of the GameObject

### GameObjectCommand_Find
- **Description**: Find GameObjects by name, tag, or component
- **Example**: `AIBridgeCLI GameObjectCommand_Find --name "Player"`
- **Parameters**:
  - `name` (string, optional): Name or partial name to search
  - `tag` (string, optional): Tag to filter by
  - `withComponent` (string, optional): Component type name to filter by
  - `maxResults` (integer, optional): Maximum number of results

### GameObjectCommand_GetInfo
- **Description**: Get detailed info about a GameObject
- **Example**: `AIBridgeCLI GameObjectCommand_GetInfo --path "Player"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject
  - `instanceId` (integer, optional): Instance ID of the GameObject

### GameObjectCommand_Rename
- **Description**: Rename a GameObject
- **Example**: `AIBridgeCLI GameObjectCommand_Rename --path "OldName" --newName "NewName"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject
  - `instanceId` (integer, optional): Instance ID of the GameObject
  - `newName` (string, optional): New name for the GameObject

### GameObjectCommand_SetActive
- **Description**: Set a GameObject active or inactive
- **Example**: `AIBridgeCLI GameObjectCommand_SetActive --path "Player" --active false`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject
  - `instanceId` (integer, optional): Instance ID of the GameObject
  - `active` (boolean, optional): Whether to activate the GameObject
  - `toggle` (boolean, optional): Toggle the current active state

### GetLogsCommand_StartCapture
- **Description**: Start capturing Unity console logs into buffer
- **Example**: `AIBridgeCLI GetLogsCommand_StartCapture`

### GetLogsCommand_StopCapture
- **Description**: Stop capturing Unity console logs
- **Example**: `AIBridgeCLI GetLogsCommand_StopCapture`

### Help
- **Description**: Get help for all registered commands, or detailed info for a specific one
- **Example**: `AIBridgeCLI Help`
- **Parameters**:
  - `command` (string, optional): Command name to get detailed help for (leave empty for all commands)

### InspectorCommand_AddComponent
- **Description**: Add a component to a GameObject
- **Example**: `AIBridgeCLI InspectorCommand_AddComponent --path "Player" --typeName "Rigidbody"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject
  - `instanceId` (integer, optional): Instance ID of the GameObject
  - `typeName` (string, optional): Component type name (e.g. Rigidbody, BoxCollider)

### InspectorCommand_GetComponents
- **Description**: Get all components on a GameObject
- **Example**: `AIBridgeCLI InspectorCommand_GetComponents --path "Player"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject
  - `instanceId` (integer, optional): Instance ID of the GameObject

### InspectorCommand_GetProperties
- **Description**: Get serialized properties of a component
- **Example**: `AIBridgeCLI InspectorCommand_GetProperties --path "Player" --componentName "Transform"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject
  - `instanceId` (integer, optional): Instance ID of the GameObject
  - `componentName` (string, optional): Component type name
  - `componentIndex` (integer, optional): Component index (alternative to componentName)

### InspectorCommand_RemoveComponent
- **Description**: Remove a component from a GameObject
- **Example**: `AIBridgeCLI InspectorCommand_RemoveComponent --path "Player" --componentName "Rigidbody"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject
  - `instanceId` (integer, optional): Instance ID of the GameObject
  - `componentName` (string, optional): Component type name
  - `componentIndex` (integer, optional): Component index
  - `componentInstanceId` (integer, optional): Instance ID of the component

### InspectorCommand_SetProperty
- **Description**: Set a serialized property on a component
- **Example**: `AIBridgeCLI InspectorCommand_SetProperty --path "Player" --componentName "Rigidbody" --propertyName "mass" --value 10`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject
  - `instanceId` (integer, optional): Instance ID of the GameObject
  - `componentName` (string, optional): Component type name
  - `componentIndex` (integer, optional): Component index (alternative to componentName)
  - `propertyName` (string, optional): Serialized property name
  - `value` (string, optional): New value for the property

### Log
- **Description**: Get console logs from Unity Editor
- **Example**: `AIBridgeCLI Log --count 50`
- **Parameters**:
  - `logType` (string, optional): Log type filter: All, Error, Warning, Log
  - `filter` (string, optional): Text filter (substring match)
  - `count` (integer, optional): Maximum number of logs to return

### MenuItemCommand_Execute
- **Description**: Execute a Unity Editor menu item by its path
- **Example**: `AIBridgeCLI MenuItemCommand_Execute --menuPath "GameObject/Create Empty"`
- **Parameters**:
  - `menuPath` (string, optional): Menu item path (e.g. GameObject/Create Empty)

### PrefabCommand_Apply
- **Description**: Apply prefab instance overrides back to the prefab asset
- **Example**: `AIBridgeCLI PrefabCommand_Apply --gameObjectPath "Player(Clone)"`
- **Parameters**:
  - `gameObjectPath` (string, optional): Hierarchy path of the prefab instance (uses selection if omitted)

### PrefabCommand_GetInfo
- **Description**: Get prefab info for an asset or instance
- **Example**: `AIBridgeCLI PrefabCommand_GetInfo --prefabPath "Assets/Prefabs/Player.prefab"`
- **Parameters**:
  - `prefabPath` (string, optional): Asset path to the prefab
  - `gameObjectPath` (string, optional): Hierarchy path of a prefab instance

### PrefabCommand_Instantiate
- **Description**: Instantiate a prefab in the scene
- **Example**: `AIBridgeCLI PrefabCommand_Instantiate --prefabPath "Assets/Prefabs/Player.prefab"`
- **Parameters**:
  - `prefabPath` (string, optional): Asset path to the prefab
  - `posX` (number, optional): X position
  - `posY` (number, optional): Y position
  - `posZ` (number, optional): Z position

### PrefabCommand_Save
- **Description**: Save a GameObject as a prefab asset
- **Example**: `AIBridgeCLI PrefabCommand_Save --gameObjectPath "Player" --savePath "Assets/Prefabs/Player.prefab"`
- **Parameters**:
  - `gameObjectPath` (string, optional): Hierarchy path of the GameObject (uses selection if omitted)
  - `savePath` (string, optional): Asset path to save the prefab to

### PrefabCommand_Unpack
- **Description**: Unpack a prefab instance
- **Example**: `AIBridgeCLI PrefabCommand_Unpack --gameObjectPath "Player(Clone)"`
- **Parameters**:
  - `gameObjectPath` (string, optional): Hierarchy path of the prefab instance (uses selection if omitted)
  - `completely` (boolean, optional): Unpack completely (all nested prefabs)

### SceneCommand_GetActive
- **Description**: Get info about the active scene
- **Example**: `AIBridgeCLI SceneCommand_GetActive`

### SceneCommand_GetHierarchy
- **Description**: Get the scene hierarchy as a tree
- **Example**: `AIBridgeCLI SceneCommand_GetHierarchy --depth 3`
- **Parameters**:
  - `depth` (integer, optional): Maximum depth to traverse
  - `includeInactive` (boolean, optional): Include inactive GameObjects

### SceneCommand_Load
- **Description**: Load a scene in the Editor
- **Example**: `AIBridgeCLI SceneCommand_Load --scenePath "Assets/Scenes/Main.unity"`
- **Parameters**:
  - `scenePath` (string, optional): Asset path to the scene file
  - `mode` (string, optional): Load mode: single or additive

### SceneCommand_New
- **Description**: Create a new empty scene
- **Example**: `AIBridgeCLI SceneCommand_New --setup empty`
- **Parameters**:
  - `setup` (string, optional): Scene setup: default or empty

### SceneCommand_Save
- **Description**: Save the current open scene(s)
- **Example**: `AIBridgeCLI SceneCommand_Save`
- **Parameters**:
  - `saveAs` (string, optional): Save to a new path (save-as)

### ScreenshotCommand_Gif
- **Description**: Capture multiple screenshots and combine into GIF, need least 15s timeout
- **Example**: `AIBridgeCLI ScreenshotCommand_Gif --frameCount 30 --fps 15`
- **Parameters**:
  - `frameCount` (integer, optional): Number of frames to capture (1-200)
  - `delay` (number, optional): Delay between frames in seconds (0.1-2.0)
  - `scale` (number, optional): Scale factor (0.25-1.0)
  - `colorCount` (integer, optional): Color count (64-256)
  - `fps` (integer, optional): FPS for GIF playback (10-30)

### ScreenshotCommand_Image
- **Description**: Capture a screenshot of the Game view
- **Example**: `AIBridgeCLI ScreenshotCommand_Image`

### SelectionCommand_Add
- **Description**: Add an object to the current selection
- **Example**: `AIBridgeCLI SelectionCommand_Add --path "Enemy1"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject
  - `assetPath` (string, optional): Asset path
  - `instanceId` (integer, optional): Instance ID

### SelectionCommand_Clear
- **Description**: Clear the current selection
- **Example**: `AIBridgeCLI SelectionCommand_Clear`

### SelectionCommand_Get
- **Description**: Get the current selection
- **Example**: `AIBridgeCLI SelectionCommand_Get`
- **Parameters**:
  - `includeComponents` (boolean, optional): Include component list for each selected GameObject

### SelectionCommand_Remove
- **Description**: Remove an object from the current selection
- **Example**: `AIBridgeCLI SelectionCommand_Remove --path "Enemy1"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject
  - `assetPath` (string, optional): Asset path
  - `instanceId` (integer, optional): Instance ID

### SelectionCommand_Set
- **Description**: Set the current selection
- **Example**: `AIBridgeCLI SelectionCommand_Set --path "Player"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject to select
  - `assetPath` (string, optional): Asset path to select
  - `instanceId` (integer, optional): Instance ID to select
  - `instanceIds` (string, optional): Comma-separated list of instance IDs to select

### TransformCommand_Get
- **Description**: Get Transform data of a GameObject
- **Example**: `AIBridgeCLI TransformCommand_Get --path "Player"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the GameObject
  - `instanceId` (integer, optional): Instance ID of the GameObject

### TransformCommand_LookAt
- **Description**: Make a GameObject look at a target position
- **Example**: `AIBridgeCLI TransformCommand_LookAt --path "Player" --targetX 0 --targetY 0 --targetZ 10`
- **Parameters**:
  - `path` (string, optional): Hierarchy path
  - `instanceId` (integer, optional): Instance ID
  - `targetX` (number, optional): Target X coordinate
  - `targetY` (number, optional): Target Y coordinate
  - `targetZ` (number, optional): Target Z coordinate

### TransformCommand_Reset
- **Description**: Reset Transform to default values
- **Example**: `AIBridgeCLI TransformCommand_Reset --path "Player"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path
  - `instanceId` (integer, optional): Instance ID
  - `position` (boolean, optional): Reset position
  - `rotation` (boolean, optional): Reset rotation
  - `scale` (boolean, optional): Reset scale

### TransformCommand_SetParent
- **Description**: Set parent of a GameObject
- **Example**: `AIBridgeCLI TransformCommand_SetParent --path "Child" --parentPath "Parent"`
- **Parameters**:
  - `path` (string, optional): Hierarchy path of the child
  - `instanceId` (integer, optional): Instance ID of the child
  - `parentPath` (string, optional): Hierarchy path of the new parent (empty to unparent)
  - `parentInstanceId` (integer, optional): Instance ID of the new parent
  - `worldPositionStays` (boolean, optional): Keep world position after reparenting

### TransformCommand_SetPosition
- **Description**: Set position of a GameObject
- **Example**: `AIBridgeCLI TransformCommand_SetPosition --path "Player" --x 0 --y 1 --z 0`
- **Parameters**:
  - `path` (string, optional): Hierarchy path
  - `instanceId` (integer, optional): Instance ID
  - `x` (number, optional): X coordinate (omit to keep current)
  - `y` (number, optional): Y coordinate (omit to keep current)
  - `z` (number, optional): Z coordinate (omit to keep current)
  - `local` (boolean, optional): Use local space

### TransformCommand_SetRotation
- **Description**: Set rotation of a GameObject (Euler angles)
- **Example**: `AIBridgeCLI TransformCommand_SetRotation --path "Player" --y 90`
- **Parameters**:
  - `path` (string, optional): Hierarchy path
  - `instanceId` (integer, optional): Instance ID
  - `x` (number, optional): X euler angle (omit to keep current)
  - `y` (number, optional): Y euler angle (omit to keep current)
  - `z` (number, optional): Z euler angle (omit to keep current)
  - `local` (boolean, optional): Use local space

### TransformCommand_SetScale
- **Description**: Set scale of a GameObject
- **Example**: `AIBridgeCLI TransformCommand_SetScale --path "Player" --uniform 2`
- **Parameters**:
  - `path` (string, optional): Hierarchy path
  - `instanceId` (integer, optional): Instance ID
  - `x` (number, optional): X scale (omit to keep current)
  - `y` (number, optional): Y scale (omit to keep current)
  - `z` (number, optional): Z scale (omit to keep current)
  - `uniform` (number, optional): Uniform scale for all axes


### Compile
- **Description**: Compile unity script or refresh assets
- **Example**: `AIBridgeCLI Compile`

**Skill Version**: 1.0
