using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Hotkey handlers for screenshot and GIF recording.
    /// F12: Screenshot
    /// F11: GIF Recording
    /// </summary>
    public static class ScreenshotHotkey
    {
        /// <summary>
        /// Capture screenshot with F12 hotkey.
        /// </summary>
        [MenuItem("AIBridge/Screenshot Game View _F12")]
        private static void CaptureScreenshot()
        {
            var result = ScreenshotHelper.CaptureGameView(checkPlayMode: true);

            if (result.Success)
            {
                Debug.Log($"[AIBridge] Screenshot saved: {result.ImagePath}");
            }
            else
            {
                Debug.LogWarning($"[AIBridge] Screenshot failed: {result.Error}");
            }
        }

        [MenuItem("AIBridge/Screenshot Game View _F12", true)]
        private static bool ValidateCaptureScreenshot()
        {
            return EditorApplication.isPlaying;
        }

        /// <summary>
        /// Record GIF with F11 hotkey.
        /// Press once to start, press again to stop early.
        /// </summary>
        [MenuItem("AIBridge/Record GIF _F11")]
        private static void RecordGif()
        {
            if (GifRecorder.IsRecording)
            {
                Debug.Log("[AIBridge] Stopping GIF recording...");
                GifRecorder.StopRecording();
                return;
            }

            int frameCount = GifRecorderSettings.DefaultFrameCount;
            int fps = GifRecorderSettings.DefaultFps;
            float scale = GifRecorderSettings.DefaultScale;
            int colorCount = GifRecorderSettings.DefaultColorCount;

            Debug.Log($"[AIBridge] Starting GIF recording: {frameCount} frames @ {fps} fps...");

            GifRecorder.StartRecording(
                frameCount,
                fps,
                scale,
                colorCount,
                onComplete: result =>
                {
                    EditorUtility.ClearProgressBar();

                    if (result.Success)
                    {
                        Debug.Log($"[AIBridge] GIF saved: {result.GifPath} ({result.FileSize / 1024}KB, {result.FrameCount} frames, {result.Duration:F1}s)");
                    }
                    else
                    {
                        Debug.LogWarning($"[AIBridge] GIF recording failed: {result.Error}");
                    }
                },
                onProgress: (current, total) =>
                {
                    EditorUtility.DisplayProgressBar(
                        "Recording GIF",
                        $"Capturing frame {current}/{total}",
                        (float)current / total);
                }
            );
        }

        [MenuItem("AIBridge/Record GIF _F11", true)]
        private static bool ValidateRecordGif()
        {
            return EditorApplication.isPlaying;
        }

        /// <summary>
        /// Open GIF settings window.
        /// </summary>
        [MenuItem("AIBridge/GIF Settings")]
        private static void OpenGifSettings()
        {
            GifSettingsWindow.ShowWindow();
        }
    }

    /// <summary>
    /// Settings window for GIF recording.
    /// </summary>
    public class GifSettingsWindow : EditorWindow
    {
        private int _frameCount;
        private int _fps;
        private float _scale;
        private int _colorCount;

        public static void ShowWindow()
        {
            var window = GetWindow<GifSettingsWindow>("GIF Settings");
            window.minSize = new Vector2(300, 200);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            _frameCount = GifRecorderSettings.DefaultFrameCount;
            _fps = GifRecorderSettings.DefaultFps;
            _scale = GifRecorderSettings.DefaultScale;
            _colorCount = GifRecorderSettings.DefaultColorCount;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("GIF Recording Settings (F11 Hotkey)", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "These settings are used when recording GIF with F11 hotkey.\n" +
                "Press F11 to start recording, press again to stop early.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            _frameCount = EditorGUILayout.IntSlider("Frame Count", _frameCount, 10, GifRecorder.MaxFrameCount);
            EditorGUILayout.LabelField($"  Duration: {(float)_frameCount / _fps:F1}s", EditorStyles.miniLabel);

            _fps = EditorGUILayout.IntSlider("FPS", _fps, 10, 30);

            _scale = EditorGUILayout.Slider("Scale", _scale, 0.25f, 1f);
            EditorGUILayout.LabelField($"  Output: {(int)(1920 * _scale)}x{(int)(1080 * _scale)} (at 1080p)", EditorStyles.miniLabel);

            _colorCount = EditorGUILayout.IntSlider("Color Count", _colorCount, 64, 256);

            EditorGUILayout.Space(20);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save", GUILayout.Height(30)))
            {
                SaveSettings();
                Debug.Log("[AIBridge] GIF settings saved.");
            }

            if (GUILayout.Button("Reset to Defaults", GUILayout.Height(30)))
            {
                GifRecorderSettings.ResetToDefaults();
                LoadSettings();
                Debug.Log("[AIBridge] GIF settings reset to defaults.");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (GifRecorder.IsRecording)
            {
                EditorGUILayout.HelpBox("Recording in progress...", MessageType.Warning);
            }
        }

        private void SaveSettings()
        {
            GifRecorderSettings.DefaultFrameCount = _frameCount;
            GifRecorderSettings.DefaultFps = _fps;
            GifRecorderSettings.DefaultScale = _scale;
            GifRecorderSettings.DefaultColorCount = _colorCount;
        }
    }
}
