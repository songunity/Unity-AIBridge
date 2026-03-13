using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Main settings window for AI Bridge.
    /// </summary>
    public class AIBridgeSettingsWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private bool _bridgeEnabled;
        private bool _debugLogging;

        // GIF Settings
        private int _gifFrameCount;
        private int _gifFps;
        private float _gifScale;
        private int _gifColorCount;

        // Command Registration Settings
        private bool _autoScan;
        private string _scanAssemblies;


        [MenuItem("AIBridge/Settings")]
        private static void OpenWindow()
        {
            var window = GetWindow<AIBridgeSettingsWindow>();
            window.titleContent = new GUIContent("AI Bridge Settings");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            _bridgeEnabled = AIBridge.Enabled;
            _debugLogging = AIBridgeLogger.DebugEnabled;

            _gifFrameCount = GifRecorderSettings.DefaultFrameCount;
            _gifFps = GifRecorderSettings.DefaultFps;
            _gifScale = GifRecorderSettings.DefaultScale;
            _gifColorCount = GifRecorderSettings.DefaultColorCount;

            _scanAssemblies = EditorPrefs.GetString(CommandRegistry.PrefKeyScanAssemblies, "Assembly-CSharp-Editor-firstpass;Assembly-CSharp");

            // Package in Library/PackageCache cannot be modified – force auto-scan
            if (!CommandRegistry.IsEditablePackage)
            {
                _autoScan = true;
                EditorPrefs.SetBool(CommandRegistry.PrefKeyAutoScan, true);
            }
            else
            {
                _autoScan = EditorPrefs.GetBool(CommandRegistry.PrefKeyAutoScan, false);
            }
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawBridgeSettings();
            EditorGUILayout.Space(10);

            DrawDirectoryInfo();
            EditorGUILayout.Space(10);

            DrawCommandRegistrationSettings();
            EditorGUILayout.Space(10);

            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("AI Bridge Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "AI Bridge enables communication between AI assistants and Unity Editor.\n" +
                "Use F12 to capture screenshots and F11 to record GIFs in Play mode.",
                MessageType.Info);
        }

        private void DrawBridgeSettings()
        {
            EditorGUILayout.LabelField("Bridge Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _bridgeEnabled = EditorGUILayout.Toggle("Bridge Enabled", _bridgeEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                AIBridge.Enabled = _bridgeEnabled;
            }

            EditorGUI.BeginChangeCheck();
            _debugLogging = EditorGUILayout.Toggle("Debug Logging", _debugLogging);
            if (EditorGUI.EndChangeCheck())
            {
                AIBridgeLogger.DebugEnabled = _debugLogging;
            }
        }

        private void DrawDirectoryInfo()
        {
            EditorGUILayout.LabelField("Directory Information", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField("Bridge Directory", AIBridge.BridgeDirectory);
            if (GUILayout.Button("Open", GUILayout.Width(60)))
            {
                if (!Directory.Exists(AIBridge.BridgeDirectory))
                {
                    Directory.CreateDirectory(AIBridge.BridgeDirectory);
                }

                EditorUtility.RevealInFinder(AIBridge.BridgeDirectory);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField("Screenshots Directory", ScreenshotHelper.ScreenshotsDir);
            if (GUILayout.Button("Open", GUILayout.Width(60)))
            {
                ScreenshotHelper.EnsureScreenshotsDirectory();
                EditorUtility.RevealInFinder(ScreenshotHelper.ScreenshotsDir);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCommandRegistrationSettings()
        {
            EditorGUILayout.LabelField("Command Registration", EditorStyles.boldLabel);

            var editable = CommandRegistry.IsEditablePackage;

            // Package location info
            var locationMsg = editable
                ? "Package location: Packages/ (editable)"
                : "Package location: Library/PackageCache (read-only)";
            EditorGUILayout.LabelField(locationMsg, EditorStyles.miniLabel);

            EditorGUILayout.Space(2);

            // Auto Scan toggle
            using (new EditorGUI.DisabledScope(!editable))
            {
                EditorGUI.BeginChangeCheck();
                var newAutoScan = EditorGUILayout.Toggle("Auto Scan on Startup", _autoScan);
                if (EditorGUI.EndChangeCheck())
                {
                    if (!newAutoScan && !editable)
                    {
                        EditorUtility.DisplayDialog(
                            "Cannot Disable Auto Scan",
                            "This package is installed in Library/PackageCache and its source files cannot be modified.\n\n" +
                            "Auto Scan must remain enabled so commands are discovered at runtime.",
                            "OK");
                        newAutoScan = true;
                    }

                    _autoScan = newAutoScan;
                    EditorPrefs.SetBool(CommandRegistry.PrefKeyAutoScan, _autoScan);
                }
            }

            if (!editable)
            {
                EditorGUILayout.HelpBox(
                    "Auto Scan is forced ON because the package is in Library/PackageCache and cannot be modified.",
                    MessageType.Warning);
            }

            // Additional assemblies
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Additional Assemblies to Scan", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"Own assembly ({typeof(CommandRegistry).Assembly.GetName().Name}) is always included.",
                EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            _scanAssemblies = EditorGUILayout.TextArea(_scanAssemblies, GUILayout.MinHeight(40));
            EditorGUILayout.LabelField("Comma-separated assembly names, e.g.  Assembly-CSharp, MyGame.Editor",
                EditorStyles.miniLabel);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(CommandRegistry.PrefKeyScanAssemblies, _scanAssemblies);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                $"Currently registered: {CommandRegistry.GetAll().Count()} commands.",
                EditorStyles.miniLabel);
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Process Commands Now", GUILayout.Height(30)))
            {
                AIBridge.ProcessCommandsNow();
                Debug.Log("[AIBridge] Commands processed.");
            }

            if (GUILayout.Button("Clear Screenshot Cache", GUILayout.Height(30)))
            {
                ClearScreenshotCache();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rescan Commands", GUILayout.Height(35)))
            {
                RescanCommands();
            }

            if (GUILayout.Button("Generate SKILL.md", GUILayout.Height(35)))
            {
                GenerateSkillMarkdown();
                SkillInstaller.OverrideSkill();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Install CC Skill", GUILayout.Height(35)))
            {
                GenerateSkillMarkdown();
                SkillInstaller.InstallToClaudeCode();
            }

            if (GUILayout.Button("Install Cursor Skill", GUILayout.Height(35)))
            {
                GenerateSkillMarkdown();
                SkillInstaller.InstallToCursor();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Hotkeys", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "F12 - Capture Screenshot (Play mode only)\n" +
                "F11 - Start/Stop GIF Recording (Play mode only)",
                MessageType.None);
        }

        private void ClearScreenshotCache()
        {
            var screenshotsDir = ScreenshotHelper.ScreenshotsDir;
            if (Directory.Exists(screenshotsDir))
            {
                var files = Directory.GetFiles(screenshotsDir);
                int count = 0;
                foreach (var file in files)
                {
                    if (Path.GetFileName(file) != ".gitignore")
                    {
                        try
                        {
                            File.Delete(file);
                            count++;
                        }
                        catch
                        {
                            // Ignore deletion errors
                        }
                    }
                }

                Debug.Log($"[AIBridge] Cleared {count} files from screenshot cache.");
            }
        }

        private void RescanCommands()
        {
            CommandRegistry.Scan();
            var count = CommandRegistry.GetAll().Count();
            Debug.Log($"[AIBridge] Rescanned commands: {count} found.");
            EditorUtility.DisplayDialog("Rescan Complete",
                $"Found {count} commands:\n{string.Join(", ", CommandRegistry.GetAll().OrderBy(e => e.Name).Select(e => e.Name))}",
                "OK");
            GenerateSkillMarkdown();
            Repaint();
        }

        private void GenerateSkillMarkdown()
        {
            var entries = CommandRegistry.GetAll().Where(e=>e.Attribute.ExposeToSkill).OrderBy(e => e.Name).ToList();
            if (entries.Count == 0)
            {
                EditorUtility.DisplayDialog("No Commands Found",
                    "No [AIBridge] methods were found. Make sure commands are compiled.", "OK");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("## Command Reference");
            sb.AppendLine();
            foreach (var entry in entries)
            {
                sb.AppendLine($"### {entry.Name}");
                sb.AppendLine($"- **Description**: {entry.Description}");
                if (!string.IsNullOrEmpty(entry.Example))
                    sb.AppendLine($"- **Example**: `{entry.Example}`");
                if (entry.Parameters.Length > 0)
                {
                    sb.AppendLine("- **Parameters**:");
                    foreach (var param in entry.Parameters)
                    {
                        var required = entry.IsRequired(param) ? "required" : "optional";
                        var typeName = entry.GetTypeName(param);
                        var desc = entry.GetParamDescription(param);
                        sb.AppendLine($"  - `{param.Name}` ({typeName}, {required}): {desc}");
                    }
                }
                sb.AppendLine();
            }
            var commandSections = new string[] { sb.ToString() };

            var skillContent = $@"---
description: ""AI Bridge Unity integration - File-based communication framework for AI to control Unity Editor. Send commands via JSON files, manipulate GameObjects, Transforms, Components, Scenes, Prefabs, and more. Supports multi-command execution and runtime extension.""
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
> & ""E:\YourProject\AIBridgeCache\CLI\AIBridgeCLI.exe"" editor log --message ""test""
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
{{Unity Project Root}}/
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
AIBridgeCLI.exe EditorCommand_Log --message ""Hello World""
AIBridgeCLI.exe GameObjectCommand_Create --name ""MyCube"" --primitiveType Cube
AIBridgeCLI.exe TransformCommand_SetPosition --path ""Player"" --x 1 --y 2 --z 3
AIBridgeCLI.exe Help
AIBridgeCLI.exe Batch --commands ""[...]""
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

{string.Join("\n\n", commandSections)}
### Compile
- **Description**: Compile unity script or refresh assets
- **Example**: `AIBridgeCLI Compile`

**Skill Version**: 1.0
";

            var skillPath = Path.Combine(Application.dataPath, "Packages", "AIBridge", "Skill~", "SKILL.md");
            if (!File.Exists(skillPath))
            {
                skillPath = Path.Combine(Application.dataPath, "..", "Packages", "AIBridge", "Skill~", "SKILL.md");
            }

            if (!File.Exists(skillPath))
            {
                Debug.LogError($"[AIBridge] SKILL.md path not found: {skillPath}");
                EditorUtility.DisplayDialog("Error", $"SKILL.md path not found: {skillPath}", "OK");
                return;
            }

            File.WriteAllText(skillPath, skillContent);
            AssetDatabase.Refresh();

            Debug.Log($"[AIBridge] Generated SKILL.md with {entries.Count} commands.");
            EditorUtility.DisplayDialog("Success",
                $"Generated SKILL.md with {entries.Count} commands.",
                "OK");
        }
    }
}