using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Automatically installs the AIBridge skill documentation to the project's .claude/skills directory.
    /// This allows Claude Code to discover and use the skill for Unity Editor operations.
    /// </summary>
    [InitializeOnLoad]
    public static class SkillInstaller
    {
        private const string SKILL_FOLDER_NAME = "aibridge";
        private const string SKILL_FILE_NAME = "SKILL.md";
        private const string PACKAGE_NAME = "cn.lys.aibridge";

        static SkillInstaller()
        {
            // Delay execution to ensure Unity is fully initialized
            EditorApplication.delayCall += InstallSkillIfNeeded;
        }

        /// <summary>
        /// Check if skill documentation needs to be installed and install it
        /// </summary>
        private static void InstallSkillIfNeeded()
        {
            try
            {
                var projectRoot = GetProjectRoot();
                var targetDir = Path.Combine(projectRoot, ".claude", "skills", SKILL_FOLDER_NAME);
                var targetFile = Path.Combine(targetDir, SKILL_FILE_NAME);

                // Check if skill file already exists
                if (File.Exists(targetFile))
                {
                    // Check if source is newer and update if needed
                    var sourceFile = GetSourceSkillPath();
                    if (!string.IsNullOrEmpty(sourceFile) && File.Exists(sourceFile))
                    {
                        var sourceTime = File.GetLastWriteTimeUtc(sourceFile);
                        var targetTime = File.GetLastWriteTimeUtc(targetFile);

                        if (sourceTime > targetTime)
                        {
                            CopySkillFile(sourceFile, targetDir, targetFile);
                            AIBridgeLogger.LogInfo($"[SkillInstaller] Updated skill documentation: {targetFile}");
                        }
                    }
                    return;
                }

                // Install skill documentation
                var sourcePath = GetSourceSkillPath();
                if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                {
                    AIBridgeLogger.LogWarning($"[SkillInstaller] Source skill file not found. Expected at: Packages/{PACKAGE_NAME}/Skill~/{SKILL_FILE_NAME}");
                    return;
                }

                CopySkillFile(sourcePath, targetDir, targetFile);
                AIBridgeLogger.LogInfo($"[SkillInstaller] Installed skill documentation to: {targetFile}");
            }
            catch (Exception ex)
            {
                AIBridgeLogger.LogError($"[SkillInstaller] Failed to install skill documentation: {ex.Message}");
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
        /// Get the actual CLI executable path relative to project root.
        /// This handles both local packages and UPM cache packages.
        /// </summary>
        private static string GetActualCliPath()
        {
            var projectRoot = GetProjectRoot();

            // Method 1: Direct package path (for local/embedded packages)
            var directPath = Path.Combine("Packages", PACKAGE_NAME, "Tools~", "CLI", "AIBridgeCLI.exe");
            if (File.Exists(Path.Combine(projectRoot, directPath)))
            {
                return directPath.Replace(Path.DirectorySeparatorChar, '/');
            }

            // Method 2: Use PackageInfo for resolved path (for git/registry packages in Library/PackageCache)
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{PACKAGE_NAME}");
            if (packageInfo != null)
            {
                var resolvedCliPath = Path.Combine(packageInfo.resolvedPath, "Tools~", "CLI", "AIBridgeCLI.exe");
                if (File.Exists(resolvedCliPath))
                {
                    // Make it relative to project root
                    return GetRelativePath(projectRoot, resolvedCliPath);
                }
            }

            return null;
        }

        /// <summary>
        /// Get relative path from base directory to target path.
        /// Returns path with forward slashes for consistency in markdown files.
        /// </summary>
        private static string GetRelativePath(string basePath, string targetPath)
        {
            var baseUri = new Uri(basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            var targetUri = new Uri(targetPath);
            var relativeUri = baseUri.MakeRelativeUri(targetUri);
            return Uri.UnescapeDataString(relativeUri.ToString());
        }

        /// <summary>
        /// Get the source skill file path from the package
        /// </summary>
        private static string GetSourceSkillPath()
        {
            // Try to find the package in Packages folder
            var projectRoot = GetProjectRoot();

            // Method 1: Direct package path (for local/embedded packages)
            var directPath = Path.Combine(projectRoot, "Packages", PACKAGE_NAME, "Skill~", SKILL_FILE_NAME);
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
        /// Copy skill file to target location with CLI path replacement.
        /// This ensures the CLI path in SKILL.md matches the actual package location.
        /// </summary>
        private static void CopySkillFile(string sourcePath, string targetDir, string targetFile)
        {
            // Create directory if not exists
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Read source content
            var content = File.ReadAllText(sourcePath, System.Text.Encoding.UTF8);

            // Get actual CLI path and replace the hardcoded path if different
            var actualCliPath = GetActualCliPath();
            if (!string.IsNullOrEmpty(actualCliPath))
            {
                var hardcodedPath = $"Packages/{PACKAGE_NAME}/Tools~/CLI/AIBridgeCLI.exe";
                if (actualCliPath != hardcodedPath)
                {
                    content = content.Replace(hardcodedPath, actualCliPath);
                    AIBridgeLogger.LogInfo($"[SkillInstaller] Replaced CLI path: {hardcodedPath} -> {actualCliPath}");
                }
            }

            // Write to target with UTF-8 encoding
            File.WriteAllText(targetFile, content, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Manually trigger skill installation (for menu item)
        /// </summary>
        [MenuItem("AIBridge/Install Skill Documentation")]
        public static void ManualInstall()
        {
            try
            {
                var projectRoot = GetProjectRoot();
                var targetDir = Path.Combine(projectRoot, ".claude", "skills", SKILL_FOLDER_NAME);
                var targetFile = Path.Combine(targetDir, SKILL_FILE_NAME);

                var sourcePath = GetSourceSkillPath();
                if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                {
                    EditorUtility.DisplayDialog("AIBridge", "Source skill file not found.", "OK");
                    return;
                }

                CopySkillFile(sourcePath, targetDir, targetFile);
                EditorUtility.DisplayDialog("AIBridge", $"Skill documentation installed to:\n{targetFile}", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("AIBridge", $"Failed to install: {ex.Message}", "OK");
            }
        }
    }
}
