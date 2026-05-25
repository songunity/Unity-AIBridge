using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    public static class ScreenshotHotkey
    {
        [MenuItem("AIBridge/Screenshot Game View _F12")]
        private static void CaptureScreenshot()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[AIBridge] Screenshot requires Play mode.");
                return;
            }

            var result = ScreenshotHelper.CaptureFrame(1f);
            if (!result.Success)
            {
                Debug.LogWarning($"[AIBridge] Screenshot failed: {result.Error}");
                return;
            }

            ScreenshotHelper.EnsureScreenshotsDirectory();
            var filename = $"game_{System.DateTime.Now:yyyyMMdd_HHmmss}_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}.png";
            var fullPath = System.IO.Path.Combine(ScreenshotHelper.ScreenshotsDir, filename);

            var tex = new Texture2D(result.Width, result.Height, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(result.Pixels);
            tex.Apply();
            var pngData = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            System.IO.File.WriteAllBytes(fullPath, pngData);
            Debug.Log($"[AIBridge] Screenshot saved: {fullPath}");
        }

        [MenuItem("AIBridge/Screenshot Game View _F12", true)]
        private static bool ValidateCaptureScreenshot()
        {
            return EditorApplication.isPlaying;
        }

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
            float startDelay = GifRecorderSettings.DefaultStartDelay;

            Debug.Log($"[AIBridge] Starting GIF recording: {frameCount} frames @ {fps} fps...");

            GifRecorder.StartRecording(frameCount, fps, scale, colorCount, startDelay,
                onComplete: result =>
                {
                    EditorUtility.ClearProgressBar();
                    if (result.Success)
                        Debug.Log($"[AIBridge] GIF saved: {result.GifPath} ({result.FileSize / 1024}KB, {result.FrameCount} frames)");
                    else
                        Debug.LogWarning($"[AIBridge] GIF recording failed: {result.Error}");
                },
                onProgress: (current, total) =>
                {
                    EditorUtility.DisplayProgressBar("Recording GIF", $"Frame {current}/{total}", (float)current / total);
                });
        }

        [MenuItem("AIBridge/Record GIF _F11", true)]
        private static bool ValidateRecordGif()
        {
            return EditorApplication.isPlaying;
        }
    }
}
