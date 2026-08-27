using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Installs the aibridge and aibridge-runtime skills to supported agent directories.
    /// </summary>
    public static class SkillInstaller
    {
        private const string SkillFileName = "SKILL.md";
        private static readonly string[] SkillNames = { "aibridge", "aibridge-runtime" };
        private static readonly string[] SkillRelativeFiles = { SkillFileName, "agents/openai.yaml" };
        private static readonly string[] AIDirectories = { ".agents", ".claude", ".cursor", ".factory", ".codex", ".kiro" };
        private static readonly string[] DefaultCreateDirs = { ".agents" };
        private static string SkillSourceDir(string skillName) => Path.Combine(AIBridge.PackageRoot, "Skill~", skillName);
        private static string SkillSourceFile(string skillName) => Path.Combine(SkillSourceDir(skillName), SkillFileName);
        private static string AgentSkillDir(string root, string agentName, string skillName) => Path.Combine(root, agentName, "skills", skillName);
        private static string AgentSkillFilePath(string root, string agentName, string skillName) => Path.Combine(AgentSkillDir(root, agentName, skillName), SkillFileName);

        private static string GetInstallRoot()
        {
            var parent = Directory.GetParent(AIBridge.ProjectRoot)?.FullName;
            if (parent != null)
            {
                foreach (var dirName in AIDirectories)
                {
                    if (Directory.Exists(Path.Combine(parent, dirName)))
                        return parent;
                }
            }
            return AIBridge.ProjectRoot;
        }

        /// <summary>
        /// Install skill to specific agent directories only.
        /// </summary>
        public static void CopyToAgent(string[] targetDirNames)
        {
            EnsureSkillSourcesExist();

            var root = GetInstallRoot();

            foreach (var dirName in targetDirNames)
            {
                CopySkillsToAgent(root, dirName);
            }
        }

        /// <summary>
        /// Install skill to AI assistant directories
        /// </summary>
        public static void CopyToAgent()
        {
            EnsureSkillSourcesExist();

            var root = GetInstallRoot();
            bool foundAnyDir = false;

            foreach (var dirName in AIDirectories)
            {
                if (!Directory.Exists(Path.Combine(root, dirName))) continue;
                foundAnyDir = true;

                CopySkillsToAgent(root, dirName);
            }

            if (!foundAnyDir)
            {
                foreach (var dirName in DefaultCreateDirs)
                {
                    CopySkillsToAgent(root, dirName);
                }
            }
        }
        
        /// <summary>
        /// Override/update existing AIBridge skill installations
        /// </summary>
        public static void OverrideSkill()
        {
            EnsureSkillSourcesExist();

            bool foundAny = false;
            var searchRoots = new[] { AIBridge.ProjectRoot };
            var parent = Directory.GetParent(AIBridge.ProjectRoot)?.FullName;
            if (parent != null)
                searchRoots = new[] { AIBridge.ProjectRoot, parent };

            foreach (var root in searchRoots)
            {
                foreach (var dirName in AIDirectories)
                {
                    if (!SkillNames.Any(skillName => File.Exists(AgentSkillFilePath(root, dirName, skillName)))) continue;

                    CopySkillsToAgent(root, dirName);
                    foundAny = true;
                }
            }

            if (!foundAny)
            {
                Debug.Log("[AIBridge] No existing AIBridge skill found, skipping override.");
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

            var skillDir = SkillSourceDir("aibridge");
            if (!Directory.Exists(skillDir))
            {
                Directory.CreateDirectory(skillDir);
            }

            var skillPath = SkillSourceFile("aibridge");
            if (!File.Exists(skillPath))
            {
                EditorUtility.DisplayDialog("Error", 
                    $"SKILL.md not found at: {skillPath}\n\nPlease create it manually first.", 
                    "OK");
                return;
            }

            var skillEntries = entries.Where(entry =>
                entry.Attribute.ExposeToSkill
                && entry.Method.DeclaringType.Name != "RuntimeCommand"
                && entry.Method.DeclaringType.Name != "RuntimeExecuteCommand").ToList();
            UpdateSkillCommandCategories(skillPath, skillEntries);

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success",
                $"Updated aibridge SKILL.md with {skillEntries.Count} commands.\n\n" +
                $"Command categories section has been regenerated.\n\n" +
                $"Location: {skillDir}",
                "OK");
        }

        private static void UpdateSkillCommandCategories(string skillPath, System.Collections.Generic.List<CommandEntry> entries)
        {
            var content = File.ReadAllText(skillPath);
            
            const string startMarker = "<!-- AUTO-GENERATED-COMMANDS-START -->";
            const string endMarker = "<!-- AUTO-GENERATED-COMMANDS-END -->";
            
            var startIndex = content.IndexOf(startMarker);
            var endIndex = content.IndexOf(endMarker);
            
            if (startIndex < 0 || endIndex < 0)
            {
                Debug.LogWarning("[AIBridge] SKILL.md missing AUTO-GENERATED markers. Command categories not updated.");
                return;
            }

            // Generate command categories
            var commandsByClass = entries.GroupBy(e => e.Method.DeclaringType.Name)
                .OrderBy(g => g.Key);

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## 命令分类");
            sb.AppendLine();

            sb.AppendLine($"-- **Compile** - 编译代码，并返回编译结果，如果有报错是会直接返回，不需要再查看Log");
            sb.AppendLine();

            foreach (var group in commandsByClass)
            {
                var categoryName = group.Key.Replace("Command", "");
                sb.AppendLine($"### {categoryName}");
                sb.AppendLine();

                foreach (var entry in group.OrderBy(e => e.Name))
                {
                    var desc = entry.Description ?? "无描述";
                    sb.AppendLine($"- **{entry.Name}** - {desc}");
                }
                sb.AppendLine();
            }

            // Replace content between markers
            var before = content.Substring(0, startIndex + startMarker.Length);
            var after = content.Substring(endIndex);
            var newContent = before + sb.ToString() + after;

            File.WriteAllText(skillPath, newContent);
            Debug.Log($"[AIBridge] Updated command categories in SKILL.md with {entries.Count} commands");
        }

        private static void EnsureSkillSourcesExist()
        {
            foreach (var skillName in SkillNames)
            {
                var sourceFile = SkillSourceFile(skillName);
                if (!File.Exists(sourceFile))
                {
                    throw new FileNotFoundException($"Source SKILL.md not found at: {sourceFile}");
                }
            }
        }

        private static void CopySkillsToAgent(string root, string agentName)
        {
            foreach (var skillName in SkillNames)
            {
                var sourceDir = SkillSourceDir(skillName);
                var targetDir = AgentSkillDir(root, agentName, skillName);
                Directory.CreateDirectory(targetDir);

                foreach (var relativePath in SkillRelativeFiles)
                {
                    var sourceFile = Path.Combine(sourceDir, relativePath);
                    if (!File.Exists(sourceFile)) continue;

                    var targetFile = Path.Combine(targetDir, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                    File.Copy(sourceFile, targetFile, true);
                }

                Debug.Log($"[AIBridge] Skill copied to {targetDir}");
            }
        }
    }
}
