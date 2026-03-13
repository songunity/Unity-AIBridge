using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Skill installation target platforms
    /// </summary>
    public enum SkillTarget
    {
        ClaudeCode,  // .claude/skills
        Cursor       // .cursor/skills
    }

    /// <summary>
    /// Automatically installs the AIBridge skill documentation to the project's .claude/skills or .cursor/skills directory.
    /// This allows Claude Code or Cursor to discover and use the skill for Unity Editor operations.
    /// </summary>
    public static class SkillInstaller
    {
        private const string SKILL_FOLDER_NAME = "aibridge";
        private const string SKILL_FILE_NAME = "SKILL.md";
        // Package directory name (not the package name in manifest)
        private const string PACKAGE_DIR_NAME = "AIBridge";
        // Package name in manifest (used for PackageInfo lookup)
        private const string PACKAGE_NAME = "cn.lys.aibridge";

        // Skill directory names for different targets
        private const string CLAUDE_SKILLS_DIR = ".claude/skills";
        private const string CURSOR_SKILLS_DIR = ".cursor/skills";
        
        // Fixed CLI path in AIBridgeCache directory
        private const string CLI_CACHE_FOLDER = "AIBridgeCache/CLI";
        private const string CLI_TOOLS_FOLDER = "Tools~/CLI";

        /// <summary>
        /// Get the current platform CLI folder name
        /// </summary>
        private static string GetCurrentPlatformCliFolder()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return "win-x64";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // Check for Apple Silicon (arm64) vs Intel (x64)
            if (SystemInfo.processorType.Contains("Apple") && SystemInfo.processorType.Contains("M"))
            {
                return "osx-arm64";
            }
            return "osx-x64";
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            return "linux-x64";
#else
            // Runtime detection fallback
            var os = Environment.OSVersion;
            if (os.Platform == PlatformID.Win32NT)
                return "win-x64";
            else if (os.Platform == PlatformID.Unix && os.VersionString.Contains("Darwin"))
            {
                // Try to detect ARM Mac
                var procInfo = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                return "osx-x64"; // Default, can be refined
            }
            return "linux-x64";
#endif
        }

        /// <summary>
        /// Get the source CLI path from the package
        /// </summary>
        private static string GetSourceCliPath()
        {
            var projectRoot = GetProjectRoot();
            var platformFolder = GetCurrentPlatformCliFolder();
            var cliPath = Path.Combine(projectRoot, "Packages", PACKAGE_DIR_NAME, CLI_TOOLS_FOLDER, platformFolder);

            if (Directory.Exists(cliPath))
            {
                return cliPath;
            }

            // Try using PackageInfo
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{PACKAGE_NAME}");
            if (packageInfo != null)
            {
                cliPath = Path.Combine(packageInfo.resolvedPath, CLI_TOOLS_FOLDER, platformFolder);
                if (Directory.Exists(cliPath))
                {
                    return cliPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Copy directory recursively
        /// </summary>
        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, targetFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, targetSubDir);
            }
        }

        /// <summary>
        /// Get the Unity project root directory
        /// </summary>
        private static string GetProjectRoot()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }

        /// <summary>
        /// Get the source skill file path from the package
        /// </summary>
        private static string GetSourceSkillPath()
        {
            // Try to find the package in Packages folder
            var projectRoot = GetProjectRoot();

            // Method 1: Direct package path (for local/embedded packages)
            var directPath = Path.Combine(projectRoot, "Packages", PACKAGE_DIR_NAME, "Skill~", SKILL_FILE_NAME);
            if (File.Exists(directPath))
            {
                return directPath;
            }

            // Method 2: Use PackageInfo to resolve package path (for git/registry packages)
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{PACKAGE_NAME}");
            if (packageInfo != null)
            {
                var packagePath = Path.Combine(packageInfo.resolvedPath, "Skill~", SKILL_FILE_NAME);
                if (File.Exists(packagePath))
                {
                    return packagePath;
                }
            }

            return null;
        }

        /// <summary>
        /// Manually trigger skill installation (for menu item)
        /// </summary>
        public static void ManualInstall(SkillTarget target)
        {
            try
            {
                var projectRoot = GetProjectRoot();

                // Determine target directory based on target platform
                var skillsDir = target == SkillTarget.Cursor ? CURSOR_SKILLS_DIR : CLAUDE_SKILLS_DIR;
                var targetDir = Path.Combine(projectRoot, skillsDir, SKILL_FOLDER_NAME);
                var targetFile = Path.Combine(targetDir, SKILL_FILE_NAME);

                var sourcePath = GetSourceSkillPath();
                if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                {
                    EditorUtility.DisplayDialog("AIBridge", "Source skill file not found.", "OK");
                    return;
                }

                Directory.CreateDirectory(targetDir);
                File.Copy(sourcePath, targetFile, true);

                // Also create symlink for CLI
                string cliMessage = "";
                var cliSourcePath = GetSourceCliPath();
                if (!string.IsNullOrEmpty(cliSourcePath))
                {
                    var cliTargetPath = Path.Combine(projectRoot, CLI_CACHE_FOLDER);
                    try
                    {
                        CopyDirectory(cliSourcePath, cliTargetPath);
                        cliMessage = $"\nCLI ({GetCurrentPlatformCliFolder()}): copy";
                    }
                    catch (Exception ex)
                    {
                        cliMessage = $"\nCLI: failed - {ex.Message}";
                    }
                }

                var targetType = target == SkillTarget.Cursor ? "Cursor" : "Claude Code";
                EditorUtility.DisplayDialog("AIBridge",
                    $"Skill documentation installed to {targetType}:\n{targetFile}\n\nMethod: copy{cliMessage}",
                    "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("AIBridge", $"Failed to install: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Install skill to Claude Code (for menu item)
        /// </summary>
        [MenuItem("AIBridge/Install to Claude Code (Symlink)")]
        public static void InstallToClaudeCode()
        {
            ManualInstall(SkillTarget.ClaudeCode);
        }

        /// <summary>
        /// Install skill to Cursor (for menu item)
        /// </summary>
        [MenuItem("AIBridge/Install to Cursor (Symlink)")]
        public static void InstallToCursor()
        {
            ManualInstall(SkillTarget.Cursor);
        }

        public static void OverrideSkill()
        {
            var projectRoot = GetProjectRoot();
            var skillsDir = CURSOR_SKILLS_DIR;
            var targetDir = Path.Combine(projectRoot, skillsDir, SKILL_FOLDER_NAME);
            if (Directory.Exists(targetDir))
            {
                ManualInstall(SkillTarget.Cursor);
            }
            skillsDir = CLAUDE_SKILLS_DIR;
            targetDir = Path.Combine(projectRoot, skillsDir, SKILL_FOLDER_NAME);
            if (Directory.Exists(targetDir))
            {
                ManualInstall(SkillTarget.ClaudeCode);
            }
        }
    }
}
