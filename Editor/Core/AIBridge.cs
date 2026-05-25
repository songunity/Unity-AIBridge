using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Main entry point for AI Bridge.
    /// Manages polling loop and command processing.
    /// Auto-initialized via [InitializeOnLoad].
    /// </summary>
    [InitializeOnLoad]
    public static class AIBridge
    {
        /// <summary>
        /// Polling interval in seconds
        /// </summary>
        private const float POLL_INTERVAL = 0.1f;

        /// <summary>
        /// Maximum commands to process per frame
        /// </summary>
        private const int MAX_COMMANDS_PER_FRAME = 5;

        private static double _lastPollTime;
        private static CommandWatcher _watcher;
        private static bool _enabled = true;

        /// <summary>
        /// Communication directory path
        /// </summary>
        public static string BridgeDirectory { get; private set; }
        public static string BridgeCLI { get; private set; }
        
        /// <summary>
        /// Package root directory path
        /// </summary>
        public static string PackageRoot { get; private set; }
        
        /// <summary>
        /// Project root directory path
        /// </summary>
        public static string ProjectRoot { get; private set; }

        /// <summary>
        /// Enable or disable the bridge
        /// </summary>
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                AIBridgeLogger.LogInfo($"AI Bridge {(_enabled ? "enabled" : "disabled")}");
            }
        }

        static AIBridge()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize the bridge
        /// </summary>
        private static void Initialize()
        {
            // Get project and package paths
            ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            PackageRoot = FindPackageRoot();
            
            // Get the exchange directory
            BridgeDirectory = GetExchangeDirectory();
            var cliName = Application.platform == RuntimePlatform.WindowsEditor ? "AIBridgeCLI.exe" : "AIBridgeCLI";
            BridgeCLI = Path.Combine(BridgeDirectory, "cli", cliName);

            // Copy CLI to .aibridge if needed
            CopyCLIIfNeeded();

            // Initialize components
            _watcher = new CommandWatcher(BridgeDirectory);
            EditorInstanceTracker.Initialize(BridgeDirectory);

            // Subscribe to editor update
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            // Cleanup heartbeat on editor quit
            EditorApplication.quitting -= EditorInstanceTracker.Cleanup;
            EditorApplication.quitting += EditorInstanceTracker.Cleanup;

            AIBridgeLogger.LogInfo($"AI Bridge initialized. Directory: {BridgeDirectory}");
            AIBridgeLogger.LogInfo(
                $"Registered commands: {string.Join(", ", CommandRegistry.GetAll().Select(e => e.Name))}");
        }

        /// <summary>
        /// Find the AIBridge package root directory
        /// </summary>
        private static string FindPackageRoot()
        {
            // Try local package first: Packages/AIBridge
            var localPath = Path.Combine(ProjectRoot, "Packages", "AIBridge");
            if (Directory.Exists(localPath))
            {
                return localPath;
            }

            // Try PackageCache: Library/PackageCache/com.sh.aibridge@*
            var packageCachePath = Path.Combine(ProjectRoot, "Library", "PackageCache");
            if (Directory.Exists(packageCachePath))
            {
                var dirs = Directory.GetDirectories(packageCachePath, "com.sh.aibridge@*");
                if (dirs.Length > 0)
                {
                    return dirs[0];
                }
            }

            AIBridgeLogger.LogWarning("AIBridge package root not found, using fallback path");
            return localPath; // Fallback
        }

        /// <summary>
        /// Editor update callback - main polling loop
        /// </summary>
        private static void OnEditorUpdate()
        {
            EditorInstanceTracker.UpdateHeartbeat();

            if (!_enabled)
            {
                return;
            }

            // Check polling interval
            var currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - _lastPollTime < POLL_INTERVAL)
            {
                return;
            }
            _lastPollTime = currentTime;

            // Scan for new commands
            _watcher.ScanForCommands();

            // Process commands (limited per frame to prevent blocking)
            var processed = 0;
            while (processed < MAX_COMMANDS_PER_FRAME && _watcher.ProcessOneCommand())
            {
                processed++;
            }
        }

        /// <summary>
        /// Get the exchange directory path in the Unity project root
        /// </summary>
        private static string GetExchangeDirectory()
        {
            // Use .aibridge in Unity project root for better compatibility with git/UPM installation
            return Path.Combine(ProjectRoot, ".aibridge");
        }

        /// <summary>
        /// Get the current platform CLI folder name
        /// </summary>
        private static string GetPlatformCliFolder()
        {
#if UNITY_EDITOR_WIN
            return "win-x64";
#elif UNITY_EDITOR_OSX
            // Check for Apple Silicon (arm64) vs Intel (x64)
            if (SystemInfo.processorType.Contains("Apple") && SystemInfo.processorType.Contains("M"))
            {
                return "osx-arm64";
            }
            return "osx-x64";
#elif UNITY_EDITOR_LINUX
            return "linux-x64";
#else
            return "win-x64"; // Default fallback
#endif
        }

        /// <summary>
        /// Copy CLI executables to .aibridge if needed
        /// </summary>
        private static void CopyCLIIfNeeded()
        {
            try
            {
                // Ensure .gitignore exists in .aibridge
                if (!Directory.Exists(BridgeDirectory))
                {
                    Directory.CreateDirectory(BridgeDirectory);
                }
                var gitignorePath = Path.Combine(BridgeDirectory, ".gitignore");
                if (!File.Exists(gitignorePath))
                {
                    File.WriteAllText(gitignorePath, "*\n!.gitignore\n");
                }

                var platformFolder = GetPlatformCliFolder();

                // Source: {PackageRoot}/Tools~/CLI/{platform}/
                var sourcePath = Path.Combine(PackageRoot, "Tools~", "CLI", platformFolder);

                // Target: .aibridge/cli/
                var targetPath = Path.Combine(BridgeDirectory, "cli");

                if (!Directory.Exists(sourcePath))
                {
                    AIBridgeLogger.LogWarning($"CLI source not found: {sourcePath}");
                    return;
                }

                // Create target directory if needed
                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                // Copy all files from source to target
                foreach (var sourceFile in Directory.GetFiles(sourcePath))
                {
                    var fileName = Path.GetFileName(sourceFile);
                    var targetFile = Path.Combine(targetPath, fileName);
                    
                    // Only copy if target doesn't exist or source is newer
                    if (!File.Exists(targetFile) || File.GetLastWriteTime(sourceFile) > File.GetLastWriteTime(targetFile))
                    {
                        File.Copy(sourceFile, targetFile, true);
                        AIBridgeLogger.LogDebug($"Copied CLI file: {fileName}");
                    }
                }

                AIBridgeLogger.LogDebug($"CLI ready at: {targetPath}");
            }
            catch (System.Exception ex)
            {
                AIBridgeLogger.LogError($"Failed to copy CLI: {ex.Message}");
            }
        }
    }
}
