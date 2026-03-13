# AI Bridge Unity Skill

Control Unity Editor through AI Bridge CLI.

## Installation

1. Ensure Unity project has AI Bridge package installed
2. CLI is located at: `D:\UnityProject\Test\AIBridgeCache\CLI\AIBridgeCLI.exe`

## Usage

```bash
AIBridgeCLI <CommandName> [--param value] [--raw]
```

### Common Flags
| Flag | Description |
|------|-------------|
| `--raw` | Output raw JSON (recommended for AI) |
| `--stdin` | Read parameters from stdin (JSON format) |
| `--help` | Show help |

**AI Usage:** Always add `--raw` for JSON output.

---

## Command Reference

### AssetDatabase
- **AssetDatabaseCommand_Find**: Find assets by AssetDatabase filter
  - Example: `AIBridgeCLI AssetDatabaseCommand_Find --filter "t:Prefab"`
- **AssetDatabaseCommand_GetPath**: Get asset path from GUID
  - Example: `AIBridgeCLI AssetDatabaseCommand_GetPath --guid "abc123..."`
- **AssetDatabaseCommand_Import**: Import a specific asset
  - Example: `AIBridgeCLI AssetDatabaseCommand_Import --assetPath "Assets/Textures/icon.png"`
- **AssetDatabaseCommand_Load**: Load and get info about an asset
  - Example: `AIBridgeCLI AssetDatabaseCommand_Load --assetPath "Assets/Prefabs/Player.prefab"`
- **AssetDatabaseCommand_Refresh**: Refresh the AssetDatabase
  - Example: `AIBridgeCLI AssetDatabaseCommand_Refresh`
- **AssetDatabaseCommand_Search**: Search assets with preset modes (all/prefab/scene/script/texture/material/audio/animation/shader/font/model/so)
  - Example: `AIBridgeCLI AssetDatabaseCommand_Search --mode prefab --keyword "Player"`


### Batch
- **Batch**: Execute multiple commands in sequence and return all results
  - Example: `AIBridgeCLI Batch --commands "[{\"type\":\"GameObjectCommand_Find\",\"params\":{\"name\":\"Player\"}}]"`


### CodeExecute
- **CodeExecuteCommand_Execute**: this command is use to run code, both editor or runtime. pass code or file. 如果脚本内容过多更建议写入文件来运行，脚本文件放到AIBridgeCache/code中
  - Example: `
AIBridge.exe CodeExecuteCommand_Execute --code 'using UnityEngine;Debug.Log("Log");Debug.LogWarning("Warning");'

// 上边代码是你需要提供的逻辑，不需要写方法，只需要写using和逻辑
// 以上的代码会被编译成下边的
using UnityEngine;

public static class CodeExecutor
{{
    public static object Execute()
    {{
        Debug.Log("Log");
        Debug.LogWarning("Warning");
        return null;
    }}
}}
`


### Compile
- **CompileCommand_Start**: 内部调用的
  - Example: ``
- **CompileCommand_Status**: 内部调用的
  - Example: ``


### Editor
- **EditorCommand_GetState**: Get current Editor state (play/pause/compile status)
  - Example: `AIBridgeCLI EditorCommand_GetState`
- **EditorCommand_Log**: Log a message to the Unity console
  - Example: `AIBridgeCLI EditorCommand_Log --message "Hello World"`
- **EditorCommand_Pause**: Toggle or set pause state
  - Example: `AIBridgeCLI EditorCommand_Pause`
- **EditorCommand_Play**: Enter Play mode
  - Example: `AIBridgeCLI EditorCommand_Play`
- **EditorCommand_Redo**: Perform redo operations
  - Example: `AIBridgeCLI EditorCommand_Redo --count 1`
- **EditorCommand_Refresh**: Refresh the AssetDatabase
  - Example: `AIBridgeCLI EditorCommand_Refresh`
- **EditorCommand_Stop**: Exit Play mode
  - Example: `AIBridgeCLI EditorCommand_Stop`
- **EditorCommand_Undo**: Perform undo operations
  - Example: `AIBridgeCLI EditorCommand_Undo --count 3`


### GameObject
- **GameObjectCommand_Create**: Create a new GameObject in the scene
  - Example: `AIBridgeCLI GameObjectCommand_Create --name "MyCube" --primitiveType Cube`
- **GameObjectCommand_Destroy**: Destroy a GameObject
  - Example: `AIBridgeCLI GameObjectCommand_Destroy --path "Player"`
- **GameObjectCommand_Duplicate**: Duplicate a GameObject
  - Example: `AIBridgeCLI GameObjectCommand_Duplicate --path "Original"`
- **GameObjectCommand_Find**: Find GameObjects by name, tag, or component
  - Example: `AIBridgeCLI GameObjectCommand_Find --name "Player"`
- **GameObjectCommand_GetInfo**: Get detailed info about a GameObject
  - Example: `AIBridgeCLI GameObjectCommand_GetInfo --path "Player"`
- **GameObjectCommand_Rename**: Rename a GameObject
  - Example: `AIBridgeCLI GameObjectCommand_Rename --path "OldName" --newName "NewName"`
- **GameObjectCommand_SetActive**: Set a GameObject active or inactive
  - Example: `AIBridgeCLI GameObjectCommand_SetActive --path "Player" --active false`


### GetLogs
- **GetLogsCommand_StartCapture**: Start capturing Unity console logs into buffer
  - Example: `AIBridgeCLI GetLogsCommand_StartCapture`
- **GetLogsCommand_StopCapture**: Stop capturing Unity console logs
  - Example: `AIBridgeCLI GetLogsCommand_StopCapture`
- **Log**: Get console logs from Unity Editor
  - Example: `AIBridgeCLI Log --count 50`


### Help
- **Help**: Get help for all registered commands, or detailed info for a specific one
  - Example: `AIBridgeCLI Help`


### InputSimulation
- **InputSimulationCommand_Click**: Simulate click on a GameObject by path
  - Example: `AIBridgeCLI InputSimulationCommand_Click --path "Canvas/Button"`
- **InputSimulationCommand_ClickAt**: Simulate click at screen coordinates
  - Example: `AIBridgeCLI InputSimulationCommand_ClickAt --x 100 --y 200`
- **InputSimulationCommand_ClickByInstanceId**: Simulate click on a GameObject by instance ID
  - Example: `AIBridgeCLI InputSimulationCommand_ClickByInstanceId --instanceId 12345`
- **InputSimulationCommand_Drag**: Simulate drag from one object to another by path
  - Example: `AIBridgeCLI InputSimulationCommand_Drag --path "Canvas/Item" --toPath "Canvas/Slot" --frames 10`
- **InputSimulationCommand_DragByInstanceId**: Simulate drag from one object to another by instance ID
  - Example: `AIBridgeCLI InputSimulationCommand_DragByInstanceId --instanceId 12345 --toInstanceId 67890 --frames 10`
- **InputSimulationCommand_LongPress**: Simulate long press on a GameObject by path
  - Example: `AIBridgeCLI InputSimulationCommand_LongPress --path "Canvas/Button" --duration 1000`
- **InputSimulationCommand_LongPressByInstanceId**: Simulate long press on a GameObject by instance ID
  - Example: `AIBridgeCLI InputSimulationCommand_LongPressByInstanceId --instanceId 12345 --duration 1000`


### Inspector
- **InspectorCommand_AddComponent**: Add a component to a GameObject
  - Example: `AIBridgeCLI InspectorCommand_AddComponent --path "Player" --typeName "Rigidbody"`
- **InspectorCommand_GetComponents**: Get all components on a GameObject
  - Example: `AIBridgeCLI InspectorCommand_GetComponents --path "Player"`
- **InspectorCommand_GetProperties**: Get serialized properties of a component
  - Example: `AIBridgeCLI InspectorCommand_GetProperties --path "Player" --componentName "Transform"`
- **InspectorCommand_RemoveComponent**: Remove a component from a GameObject
  - Example: `AIBridgeCLI InspectorCommand_RemoveComponent --path "Player" --componentName "Rigidbody"`
- **InspectorCommand_SetProperty**: Set a serialized property on a component
  - Example: `AIBridgeCLI InspectorCommand_SetProperty --path "Player" --componentName "Rigidbody" --propertyName "mass" --value 10`


### MenuItem
- **MenuItemCommand_Execute**: Execute a Unity Editor menu item by its path
  - Example: `AIBridgeCLI MenuItemCommand_Execute --menuPath "GameObject/Create Empty"`


### Prefab
- **PrefabCommand_Apply**: Apply prefab instance overrides back to the prefab asset
  - Example: `AIBridgeCLI PrefabCommand_Apply --gameObjectPath "Player(Clone)"`
- **PrefabCommand_GetInfo**: Get prefab info for an asset or instance
  - Example: `AIBridgeCLI PrefabCommand_GetInfo --prefabPath "Assets/Prefabs/Player.prefab"`
- **PrefabCommand_Instantiate**: Instantiate a prefab in the scene
  - Example: `AIBridgeCLI PrefabCommand_Instantiate --prefabPath "Assets/Prefabs/Player.prefab"`
- **PrefabCommand_Save**: Save a GameObject as a prefab asset
  - Example: `AIBridgeCLI PrefabCommand_Save --gameObjectPath "Player" --savePath "Assets/Prefabs/Player.prefab"`
- **PrefabCommand_Unpack**: Unpack a prefab instance
  - Example: `AIBridgeCLI PrefabCommand_Unpack --gameObjectPath "Player(Clone)"`


### Scene
- **SceneCommand_GetActive**: Get info about the active scene
  - Example: `AIBridgeCLI SceneCommand_GetActive`
- **SceneCommand_GetHierarchy**: Get the scene hierarchy as a tree
  - Example: `AIBridgeCLI SceneCommand_GetHierarchy --depth 3`
- **SceneCommand_Load**: Load a scene in the Editor
  - Example: `AIBridgeCLI SceneCommand_Load --scenePath "Assets/Scenes/Main.unity"`
- **SceneCommand_New**: Create a new empty scene
  - Example: `AIBridgeCLI SceneCommand_New --setup empty`
- **SceneCommand_Save**: Save the current open scene(s)
  - Example: `AIBridgeCLI SceneCommand_Save`


### Screenshot
- **ScreenshotCommand_Gif**: Capture multiple screenshots and combine into GIF, need least 15s timeout
  - Example: `AIBridgeCLI ScreenshotCommand_Gif --frameCount 30 --fps 15`
- **ScreenshotCommand_Image**: Capture a screenshot of the Game view
  - Example: `AIBridgeCLI ScreenshotCommand_Image`


### Selection
- **SelectionCommand_Add**: Add an object to the current selection
  - Example: `AIBridgeCLI SelectionCommand_Add --path "Enemy1"`
- **SelectionCommand_Clear**: Clear the current selection
  - Example: `AIBridgeCLI SelectionCommand_Clear`
- **SelectionCommand_Get**: Get the current selection
  - Example: `AIBridgeCLI SelectionCommand_Get`
- **SelectionCommand_Remove**: Remove an object from the current selection
  - Example: `AIBridgeCLI SelectionCommand_Remove --path "Enemy1"`
- **SelectionCommand_Set**: Set the current selection
  - Example: `AIBridgeCLI SelectionCommand_Set --path "Player"`


### Transform
- **TransformCommand_Get**: Get Transform data of a GameObject
  - Example: `AIBridgeCLI TransformCommand_Get --path "Player"`
- **TransformCommand_LookAt**: Make a GameObject look at a target position
  - Example: `AIBridgeCLI TransformCommand_LookAt --path "Player" --targetX 0 --targetY 0 --targetZ 10`
- **TransformCommand_Reset**: Reset Transform to default values
  - Example: `AIBridgeCLI TransformCommand_Reset --path "Player"`
- **TransformCommand_SetParent**: Set parent of a GameObject
  - Example: `AIBridgeCLI TransformCommand_SetParent --path "Child" --parentPath "Parent"`
- **TransformCommand_SetPosition**: Set position of a GameObject
  - Example: `AIBridgeCLI TransformCommand_SetPosition --path "Player" --x 0 --y 1 --z 0`
- **TransformCommand_SetRotation**: Set rotation of a GameObject (Euler angles)
  - Example: `AIBridgeCLI TransformCommand_SetRotation --path "Player" --y 90`
- **TransformCommand_SetScale**: Set scale of a GameObject
  - Example: `AIBridgeCLI TransformCommand_SetScale --path "Player" --uniform 2`


**Skill Version**: 1.0
