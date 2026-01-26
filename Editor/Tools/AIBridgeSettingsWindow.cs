using System.IO;
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
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawBridgeSettings();
            EditorGUILayout.Space(10);

            DrawGifSettings();
            EditorGUILayout.Space(10);

            DrawDirectoryInfo();
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

        private void DrawGifSettings()
        {
            EditorGUILayout.LabelField("GIF Recording Settings (F11)", EditorStyles.boldLabel);

            _gifFrameCount = EditorGUILayout.IntSlider("Frame Count", _gifFrameCount, 10, GifRecorder.MaxFrameCount);
            EditorGUILayout.LabelField($"  Duration: {(float)_gifFrameCount / _gifFps:F1}s", EditorStyles.miniLabel);

            _gifFps = EditorGUILayout.IntSlider("FPS", _gifFps, 10, 30);

            _gifScale = EditorGUILayout.Slider("Scale", _gifScale, 0.25f, 1f);
            EditorGUILayout.LabelField($"  Output: {(int)(1920 * _gifScale)}x{(int)(1080 * _gifScale)} (at 1080p)", EditorStyles.miniLabel);

            _gifColorCount = EditorGUILayout.IntSlider("Color Count", _gifColorCount, 64, 256);

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save GIF Settings"))
            {
                GifRecorderSettings.DefaultFrameCount = _gifFrameCount;
                GifRecorderSettings.DefaultFps = _gifFps;
                GifRecorderSettings.DefaultScale = _gifScale;
                GifRecorderSettings.DefaultColorCount = _gifColorCount;
                Debug.Log("[AIBridge] GIF settings saved.");
            }

            if (GUILayout.Button("Reset to Defaults"))
            {
                GifRecorderSettings.ResetToDefaults();
                LoadSettings();
                Debug.Log("[AIBridge] GIF settings reset to defaults.");
            }
            EditorGUILayout.EndHorizontal();

            if (GifRecorder.IsRecording)
            {
                EditorGUILayout.HelpBox("GIF Recording in progress...", MessageType.Warning);
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
    }
}
