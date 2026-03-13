# AI Bridge

English | [中文](./README_CN.md)

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
- Use `[AIBridge]` attribute with description
- Use `[Description]` for parameter documentation
- Return `CommandResult.Success()` or `CommandResult.Failure()`

### 2. Scan Commands

Open `AIBridge/Settings` window (menu: `AIBridge/Settings`) and click **"Scan Commands"** button to register your custom commands.

### 3. Generate Skill Documentation

In the same settings window, click **"Generate Skill"** to auto-generate `Skill~/SKILL.md` documentation for AI assistants.

**Important:** When generating the Skill documentation, it automatically scans and includes complete descriptions, parameter details, and usage examples for all registered commands, including both built-in and your custom commands.

### 4. Use Commands

Use the CLI tool or let AI assistants call your commands:

```bash
AIBridgeCLI.exe MyCustomCommand_CreateCustomCube --name "MyCube" --size 2.0
```

## Command Registration

### Auto-Scan Mode

Enable **"Auto Scan on Startup"** in `AIBridge/Settings` to automatically scan and register commands when Unity starts. You can specify which assemblies to scan (default: `Assembly-CSharp-Editor-firstpass;Assembly-CSharp`).

**Note:** If the package is installed in `Library/PackageCache` (immutable), auto-scan is forced on.

### Manual Registration

If auto-scan is disabled, commands are registered from the built-in `CommandRegistry.AutoRegister()` method. After adding new commands, click **"Scan Commands"** in the settings window.

## Skill Documentation

The `Skill~/SKILL.md` file is auto-generated documentation for AI assistants (like Claude, GPT, etc.). It includes:

- All registered command names and descriptions (built-in + custom)
- Parameter details for each command (types and descriptions)
- Usage examples
- CLI syntax

**To regenerate:** Click **"Generate Skill"** in `AIBridge/Settings` after adding or modifying commands. The system will automatically scan all commands and generate complete documentation.

**Usage:** Share `Skill~/SKILL.md` with your AI assistant to enable Unity Editor control. See the file for detailed command reference and examples.

## Install Skill to AI Editors

To enable Claude Code or Cursor to automatically recognize the AIBridge Skill, you can install it with one click:

### Install to Claude Code

Menu: `AIBridge/Install to Claude Code (Symlink)`

This creates a `.claude/skills/aibridge` symlink in the project root, pointing to `Skill~/SKILL.md`.

### Install to Cursor

Menu: `AIBridge/Install to Cursor (Symlink)`

This creates a `.cursor/skills/aibridge` symlink in the project root, pointing to `Skill~/SKILL.md`.

**Notes:**
- Using symlinks ensures AI editors automatically get the latest version when you regenerate the Skill documentation
- Creating symlinks on Windows may require administrator privileges
- If symlink creation fails, it will automatically fall back to file copy mode

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
