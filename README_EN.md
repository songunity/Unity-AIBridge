# AI Bridge

English | [中文](./README.md)

File-based communication framework between AI Code assistants and Unity Editor.

## Features

- **GameObject** - Create, destroy, find, rename, duplicate, toggle active
- **Transform** - Position, rotation, scale, parent hierarchy, look at
- **Component/Inspector** - Get/set properties, add/remove components
- **Scene** - Load, save, get hierarchy, create new
- **Prefab** - Instantiate, save, unpack, apply overrides
- **Asset** - Search, import, refresh, find by filter
- **Editor Control** - Compile, undo/redo, play mode, focus window
- **Screenshot & GIF** - Capture game view, record animated GIFs
- **Batch Commands** - Execute multiple commands efficiently
- **Code Execution** - Execute C# code dynamically in Editor or Runtime

## Why AI Bridge? (vs Unity MCP)

| Feature               | AI Bridge          | Unity MCP                       |
| --------------------- | ------------------ | ------------------------------- |
| Communication         | File-based         | WebSocket                       |
| During Unity Compile  | **Works normally** | Connection lost                 |
| Port Conflicts        | None               | May cause reconnection failure  |
| Multi-Project Support | **Yes**            | No                              |
| Stability             | **High**           | Affected by compile/restart     |
| Context Usage         | **Low**            | Higher                          |
| Extensibility         | Simple interface   | Requires MCP protocol knowledge |

**The Problem with MCP**: Unity MCP uses persistent WebSocket connections. When Unity recompiles (which happens frequently during development), the connection breaks. Port conflicts can also prevent reconnection, leading to a frustrating experience.

**AI Bridge Solution**: By using file-based communication, AI Bridge completely avoids these issues. Commands are written as JSON files and results are read back - simple, stable, and reliable regardless of Unity's state.

## Installation

### Via Unity Package Manager

1. Open Unity Package Manager (Window > Package Manager)
2. Click "+" > "Add package from git URL"
3. Enter: `https://github.com/wang-er-s/AIBridge.git`

### Manual Installation

1. Download or clone this repository
2. Copy the entire folder to your Unity project's `Packages` folder

## Requirements

- Unity 2021.3 or later
- .NET 6.0 Runtime (for CLI tool)
- Newtonsoft.Json (com.unity.nuget.newtonsoft-json)

## Quick Start

### 0. Initial Setup

After installing AI Bridge, you need to complete the following initialization steps:

1. **Open Settings Window**: `Window > AIBridge`
2. **Install Skill to Agent**: Switch to the `Tools` tab and click the **"Copy To Agent"** button to install the Skill documentation to the agent's skills directory
3. **Configure Auto-Scan** (Optional):
   - Switch to the `Commands` tab
   - Enable the **"Auto Scan on Startup"** option
   - Configure the assemblies to scan in the **"Scan Assemblies"** text field (default: `Assembly-CSharp-Editor-firstpass;Assembly-CSharp`)
   - If your custom commands are in other assemblies, add them to this list, separated by semicolons

**Note:** If the package is installed in `Library/PackageCache` (immutable), auto-scan is forced on.

### 1. Add Custom Commands

Create a static class with methods marked with `[AIBridge]` attribute:

```csharp
using AIBridge.Editor;
using System.Collections;
using System.ComponentModel;

public static class MyCustomCommand
{
    [AIBridge("Create a custom cube with specific settings")]
    public static IEnumerator CreateCustomCube(
        [Description("Cube name")] string name = "CustomCube",
        [Description("Cube size")] float size = 1.0f)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.localScale = Vector3.one * size;

        yield return CommandResult.Success($"Created {name} with size {size}");
    }
}
```

**Key Points:**

- Method must be `static` and return `IEnumerator`
- You can use `yield return new WaitForSeconds` or `yield return new WaitUntil`
- Use `[AIBridge]` attribute with description
- Use `[Description]` for parameter documentation (optional, defaults to field name if not provided)
- Return `CommandResult.Success()` or `CommandResult.Failure()`

### 2. Refresh Command List

After adding custom commands, you need to rescan and regenerate the Skill documentation:

1. Open the `Window > AIBridge` window
2. Switch to the `Commands` tab
3. Click the **"Refresh Command List"** button (if Auto Scan is enabled, this button will be hidden and commands will be scanned automatically)

**This operation will:**
- Scan all commands in the specified assemblies
- Update the command registry
- Automatically regenerate the `Skill~/SKILL.md` documentation
- Automatically update the installed Agent Skill documentation

**Important:** Every time you add or modify custom commands, you need to click this button to update the Skill documentation so that AI assistants can recognize your new commands.

### 3. Use Commands

Use the CLI tool or let AI assistants call your commands:

```bash
AIBridgeCLI.exe MyCustomCommand_CreateCustomCube --name "MyCube" --size 2.0
```

## Command Registration

### Auto-Scan Mode

Enable **"Auto Scan on Startup"** in the `Commands` tab of `Window > AIBridge`:

- Unity will automatically scan and register commands on startup
- Specify the assemblies to scan in the **"Scan Assemblies"** text field (default: `Assembly-CSharp-Editor-firstpass;Assembly-CSharp`)
- Multiple assemblies are separated by semicolons (`;`)
- If your custom commands are in other assemblies (e.g., `MyCustomCommands`), add them to the list: `Assembly-CSharp-Editor-firstpass;Assembly-CSharp;MyCustomCommands`

**Note:** If the package is installed in `Library/PackageCache` (immutable), auto-scan is forced on.

### Manual Refresh Mode

If auto-scan is disabled:

1. Commands are registered from the built-in `CommandRegistry.AutoRegister()` method
2. After adding new commands, manually click the **"Refresh Command List"** button in the `Commands` tab of the settings window
3. This will rescan all assemblies and update the Skill documentation

## Skill Documentation

The `Skill~/SKILL.md` file is auto-generated documentation for AI assistants (like Droid, Claude, GPT, etc.). It includes:

- All registered command names and descriptions (built-in + custom)
- Parameter details for each command (types and descriptions)
- Usage examples
- CLI syntax

You can add your own content, but do not add anything between `<!-- AUTO-GENERATED-COMMANDS-START -->` and `<!-- AUTO-GENERATED-COMMANDS-END -->`

### Install Skill to Agent Directory

**Initial Installation (Required):**

1. Open the `Window > AIBridge` window
2. Switch to the `Tools` tab
3. Click the **"Copy To Agent"** button

**Copy Logic:**
- The system will first scan for existing AI editor directories in the project root (`.cursor`, `.agent`, `.factory`, `.claude`, `.codex`, etc.)
- If any existing directories are found, the Skill documentation will be copied to the `skills/aibridge/` subdirectory of these directories
- If no AI editor directories are found, it will automatically create a `.agent` directory and copy the Skill documentation

**Examples:**
- If the project already has a `.factory` directory, the Skill will be copied to `.factory/skills/aibridge/SKILL.md`
- If the project has both `.factory` and `.cursor` directories, both will be updated
- If the project has no AI editor directories, it will create `.agent/skills/aibridge/SKILL.md`

### Update Skill Documentation

When you add or modify custom commands:

**Method 1: Auto Update (Recommended)**
- Click the **"Refresh Command List"** button in the `Commands` tab
- This will automatically regenerate the Skill documentation and update all installed Agent directories

**Method 2: Manual Update**
- Click the **"Generate Skill"** button in the `Tools` tab to regenerate the documentation
- Then click the **"Copy To Agent"** button to update the Agent directories

**Usage:** AI assistants will automatically read the `.factory/skills/aibridge/SKILL.md` file to recognize all available Unity Editor control commands.

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
