using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Automatically installs the AIBridge skill documentation to the project's .agent directory.
    /// This allows AI assistants to discover and use the skill for Unity Editor operations.
    /// </summary>
    public static class SkillInstaller
    {
        private const string SKILL_FILE_NAME = "SKILL.md";
        private const string AGENT_DIR = ".agent/skills/aibridge";

        /// <summary>
        /// Copy skill to .agent directory
        /// </summary>
        public static void CopyToAgent()
        {
            try
            {
                var sourceFile = Path.Combine(AIBridge.PackageRoot, "Skill~", SKILL_FILE_NAME);
                
                if (!File.Exists(sourceFile))
                {
                    throw new FileNotFoundException($"Source SKILL.md not found at: {sourceFile}");
                }

                var agentDir = Path.Combine(AIBridge.ProjectRoot, AGENT_DIR);
                var targetFile = Path.Combine(agentDir, SKILL_FILE_NAME);

                if (!Directory.Exists(agentDir))
                {
                    Directory.CreateDirectory(agentDir);
                }

                File.Copy(sourceFile, targetFile, true);
                Debug.Log($"[AIBridge] Skill copied to .agent directory: {targetFile}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIBridge] Failed to copy skill: {ex.Message}");
                throw;
            }
        }
        
        public static void GenerateSkillFile()
        {
            var entries = CommandRegistry.GetAll().ToList();
            if (entries.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "No commands registered. Please scan assemblies first.", "OK");
                return;
            }

            var commandsByClass = entries.GroupBy(e => e.Method.DeclaringType.Name)
                .OrderBy(g => g.Key);

            var commandSections = commandsByClass.Select(group =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"### {group.Key.Replace("Command", "")}");
                
                foreach (var entry in group.OrderBy(e => e.Name))
                {
                    var desc = entry.Description ?? "No description";
                    var example = entry.Example ?? $"AIBridgeCLI {entry.Name}";
                    
                    sb.AppendLine($"- **{entry.Name}**: {desc}");
                    sb.AppendLine($"  - Example: `{example}`");
                }
                
                return sb.ToString();
            });

            var skillContent = $@"# AI Bridge Unity Skill

Control Unity Editor through AI Bridge CLI.

## Installation

1. Ensure Unity project has AI Bridge package installed
2. CLI is located at: `{AIBridge.BridgeCLI}`

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

{string.Join("\n\n", commandSections)}

**Skill Version**: 1.0
";

            var skillPath = Path.Combine(AIBridge.PackageRoot, "Skill~", SKILL_FILE_NAME);
            var dir = Path.GetDirectoryName(skillPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(skillPath, skillContent);
            AssetDatabase.Refresh();

            Debug.Log($"[AIBridge] Generated SKILL.md with {entries.Count} commands at: {skillPath}");
            EditorUtility.DisplayDialog("Success", 
                $"Generated SKILL.md with {entries.Count} commands.", "OK");
        }

        /// <summary>
        /// Override/update existing skill installations in .agent directory
        /// </summary>
        public static void OverrideSkill()
        {
            try
            {
                var agentDir = Path.Combine(AIBridge.ProjectRoot, AGENT_DIR);
                var targetFile = Path.Combine(agentDir, "aibridge-skill.md");
                
                if (!File.Exists(targetFile))
                {
                    return; // No existing installation to override
                }

                var sourceFile = Path.Combine(AIBridge.PackageRoot, "Skill~", SKILL_FILE_NAME);
                if (!File.Exists(sourceFile))
                {
                    Debug.LogWarning("[AIBridge] Source SKILL.md not found, cannot override.");
                    return;
                }

                File.Copy(sourceFile, targetFile, true);
                Debug.Log($"[AIBridge] Skill updated in .agent directory: {targetFile}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIBridge] Failed to override skill: {ex.Message}");
            }
        }
    }
}
