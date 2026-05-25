using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Screenshot result data.
    /// </summary>
    public class ScreenshotResult
    {
        public bool Success;
        public string ImagePath;
        public string Filename;
        public int Width;
        public int Height;
        public string Timestamp;
        public string Error;
    }

    /// <summary>
    /// Frame capture result for GIF recording.
    /// </summary>
    public class FrameCaptureResult
    {
        public bool Success;
        public byte[] Pixels;
        public int Width;
        public int Height;
        public string Error;
    }

    /// <summary>
    /// GIF recording result data.
    /// </summary>
    public class GifRecordResult
    {
        public bool Success;
        public string GifPath;
        public string Filename;
        public int FrameCount;
        public int Width;
        public int Height;
        public float Duration;
        public long FileSize;
        public string Timestamp;
        public string Error;
    }

    /// <summary>
    /// Shared screenshot capture logic for CLI and hotkey.
    /// Optimized with cached resources for GIF recording.
    /// </summary>
    public static class ScreenshotHelper
    {
        private static string _screenshotsDir;

        // Cached resources for frame capture (reused across frames)
        private static RenderTexture _cachedRenderTexture;
        private static Texture2D _cachedTexture2D;
        private static int _cachedWidth;
        private static int _cachedHeight;
        private static byte[] _cachedFlipBuffer;

        // Cached reflection handles for Game View render texture
        private static bool _reflectionInitialized;
        private static System.Type _gameViewType;
        private static FieldInfo _renderTextureField;

        // Cached GameView size (avoid reflection every frame)
        private static Vector2 _cachedGameViewSize;
        private static double _lastGameViewSizeCheck;
        private const double GameViewSizeCacheInterval = 0.5;

        /// <summary>
        /// Get the screenshots directory path.
        /// </summary>
        public static string ScreenshotsDir
        {
            get
            {
                if (string.IsNullOrEmpty(_screenshotsDir))
                {
                    var projectRoot = Path.GetDirectoryName(Application.dataPath);
                    _screenshotsDir = Path.Combine(projectRoot, ".aibridge", "screenshots");
                }
                return _screenshotsDir;
            }
        }

        /// <summary>
        /// Capture Game view screenshot.
        /// </summary>
        public static IEnumerator CaptureGameView(Action<ScreenshotResult> onFinish)
        {
            EnsureScreenshotsDirectory();

            var timestamp = DateTime.Now;
            var filename = $"game_{timestamp:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.png";
            var fullPath = Path.Combine(ScreenshotsDir, filename);

            // Get Game View window
            var gameView = GetGameView();
            if (gameView == null)
            {
                onFinish?.Invoke(new ScreenshotResult
                {
                    Success = false,
                    Error = "Cannot find Game View window. Make sure Game View is open.",
                });
                yield break;
            }

            // Get Game View size
            var size = GetGameViewSize(gameView);
            int width = (int)size.x;
            int height = (int)size.y;

            // Capture screenshot using ScreenCapture
            ScreenCapture.CaptureScreenshot(fullPath);

            // Wait briefly for file to be written
            int retryCount = 0;
            while (!File.Exists(fullPath) && retryCount < 100)
            {
                yield return new WaitForSeconds(0.1f);
                retryCount++;
            }

            if (!File.Exists(fullPath))
            {
                onFinish?.Invoke(new ScreenshotResult
                {
                    Success = false,
                    Error = "Failed to capture screenshot - file was not created."
                });
                yield break;
            }

            onFinish?.Invoke(new ScreenshotResult
            {
                Success = true,
                ImagePath = fullPath,
                Filename = filename,
                Width = width,
                Height = height,
                Timestamp = timestamp.ToString("yyyyMMdd_HHmmss"),
            });
        }

        /// <summary>
        /// Get the Unity Game View window using reflection.
        /// </summary>
        private static EditorWindow GetGameView()
        {
            // UnityEditor.GameView is in the UnityEditor assembly
            var gameViewType = Type.GetType("UnityEditor.GameView, UnityEditor");
            if (gameViewType == null)
            {
                Debug.LogError("[AIBridge] Cannot find GameView type in UnityEditor assembly");
                return null;
            }

            // Get the main Game View window
            var getMainGameView = gameViewType.GetMethod("GetMainGameView", BindingFlags.Static | BindingFlags.NonPublic);
            if (getMainGameView != null)
            {
                return getMainGameView.Invoke(null, null) as EditorWindow;
            }

            // Fallback: GetWindow with specific type
            return EditorWindow.GetWindow(gameViewType);
        }

        /// <summary>
        /// Get Game View resolution size.
        /// </summary>
        private static Vector2 GetGameViewSize(EditorWindow gameView)
        {
            // Try to get resolution from gameViewRenderResolution property
            var prop = gameView.GetType().GetProperty("gameViewRenderResolution", BindingFlags.Instance | BindingFlags.Public);
            if (prop != null)
            {
                var value = prop.GetValue(gameView);
                if (value is Vector2 size && size.x > 0 && size.y > 0)
                {
                    return size;
                }
            }

            // Try to get size from GetSize method (instance method)
            var getSizeMethod = gameView.GetType().GetMethod("GetSize", BindingFlags.Instance | BindingFlags.Public);
            if (getSizeMethod != null)
            {
                var result = getSizeMethod.Invoke(gameView, null);
                if (result is Vector2 size && size.x > 0 && size.y > 0)
                {
                    return size;
                }
            }

            // Try to use GameViewSize property
            var sizeProp = gameView.GetType().GetProperty("GameViewSize", BindingFlags.Instance | BindingFlags.Public);
            if (sizeProp != null)
            {
                var gameViewSize = sizeProp.GetValue(gameView);
                if (gameViewSize != null)
                {
                    // GameViewSize has Width and Height properties
                    var widthProp = gameViewSize.GetType().GetProperty("Width", BindingFlags.Instance | BindingFlags.Public);
                    var heightProp = gameViewSize.GetType().GetProperty("Height", BindingFlags.Instance | BindingFlags.Public);
                    if (widthProp != null && heightProp != null)
                    {
                        int width = (int)widthProp.GetValue(gameViewSize);
                        int height = (int)heightProp.GetValue(gameViewSize);
                        if (width > 0 && height > 0)
                        {
                            return new Vector2(width, height);
                        }
                    }
                }
            }

            // Try to get target display resolution from Unity
            // For mobile games in portrait mode, check the default screen width/height
            #if UNITY_ANDROID || UNITY_IPHONE
            if (Screen.width > 0 && Screen.height > 0)
            {
                return new Vector2(Screen.width, Screen.height);
            }
            #endif

            // Default fallback - check if portrait mode (height > width)
            int defaultWidth = 1080;
            int defaultHeight = 1920;
            return new Vector2(defaultWidth, defaultHeight);
        }

        /// <summary>
        /// Ensure screenshots directory exists.
        /// </summary>
        public static void EnsureScreenshotsDirectory()
        {
            if (!Directory.Exists(ScreenshotsDir))
            {
                Directory.CreateDirectory(ScreenshotsDir);
            }
        }

        /// <summary>
        /// Capture a single frame for streaming GIF recording.
        /// Reads Game View's internal RenderTexture to capture full composited output
        /// including Screen Space - Overlay Canvas.
        /// </summary>
        public static FrameCaptureResult CaptureFrame(float scale = 1f)
        {
            if (!EditorApplication.isPlaying)
            {
                return new FrameCaptureResult { Success = false, Error = "Frame capture requires Play mode." };
            }

            try
            {
                var texture2D = CaptureGameViewTexture();
                if (texture2D == null)
                {
                    return new FrameCaptureResult { Success = false, Error = "Failed to capture Game view." };
                }

                int width = texture2D.width;
                int height = texture2D.height;

                if (scale < 1f)
                {
                    scale = Mathf.Clamp(scale, 0.25f, 1f);
                    int scaledWidth = Mathf.Max(1, (int)(width * scale));
                    int scaledHeight = Mathf.Max(1, (int)(height * scale));

                    var rt = RenderTexture.GetTemporary(scaledWidth, scaledHeight);
                    Graphics.Blit(texture2D, rt);

                    var scaledTexture = new Texture2D(scaledWidth, scaledHeight, TextureFormat.RGBA32, false);
                    RenderTexture.active = rt;
                    scaledTexture.ReadPixels(new Rect(0, 0, scaledWidth, scaledHeight), 0, 0);
                    scaledTexture.Apply();
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(rt);

                    UnityEngine.Object.DestroyImmediate(texture2D);
                    texture2D = scaledTexture;
                    width = scaledWidth;
                    height = scaledHeight;
                }

                var pixels = texture2D.GetRawTextureData();
                EnsureFlipBuffer(width, height);
                int rowSize = width * 4;
                for (int y = 0; y < height; y++)
                {
                    Buffer.BlockCopy(pixels, y * rowSize, _cachedFlipBuffer, (height - 1 - y) * rowSize, rowSize);
                }

                UnityEngine.Object.DestroyImmediate(texture2D);

                var result = new byte[_cachedFlipBuffer.Length];
                Buffer.BlockCopy(_cachedFlipBuffer, 0, result, 0, result.Length);

                return new FrameCaptureResult { Success = true, Pixels = result, Width = width, Height = height };
            }
            catch (Exception ex)
            {
                return new FrameCaptureResult { Success = false, Error = $"Failed to capture frame: {ex.Message}" };
            }
        }

        /// <summary>
        /// Capture the Game View content by reading its internal RenderTexture directly.
        /// Captures everything including Screen Space - Overlay Canvas.
        /// Returns a new Texture2D that the caller must destroy.
        /// </summary>
        private static Texture2D CaptureGameViewTexture()
        {
            var rt = GetGameViewRenderTexture();
            if (rt != null)
            {
                var flipped = RenderTexture.GetTemporary(rt.width, rt.height, 0, rt.graphicsFormat);
                if (SystemInfo.graphicsUVStartsAtTop)
                    Graphics.Blit(rt, flipped, new Vector2(1, -1), new Vector2(0, 1));
                else
                    Graphics.Blit(rt, flipped);

                var texture2D = new Texture2D(flipped.width, flipped.height, TextureFormat.RGBA32, false);
                var prev = RenderTexture.active;
                RenderTexture.active = flipped;
                texture2D.ReadPixels(new Rect(0, 0, flipped.width, flipped.height), 0, 0);
                texture2D.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(flipped);

                return texture2D;
            }

            // Fallback: ScreenCapture (requires Game view to be focused)
            return ScreenCapture.CaptureScreenshotAsTexture();
        }

        /// <summary>
        /// Get the Game View's internal RenderTexture via reflection.
        /// Searches GameView and its base class PlayModeView for the m_RenderTexture field.
        /// </summary>
        private static RenderTexture GetGameViewRenderTexture()
        {
            InitReflection();
            if (_gameViewType == null) return null;

            var gameView = GetGameViewWindow();
            if (gameView == null) return null;

            if (_renderTextureField != null)
            {
                return _renderTextureField.GetValue(gameView) as RenderTexture;
            }

            return null;
        }

        private static void InitReflection()
        {
            if (_reflectionInitialized) return;
            _reflectionInitialized = true;

            var editorAssembly = typeof(EditorWindow).Assembly;
            _gameViewType = editorAssembly.GetType("UnityEditor.GameView");
            if (_gameViewType == null) return;

            const BindingFlags kInstance = BindingFlags.NonPublic | BindingFlags.Instance;

            var type = _gameViewType;
            while (type != null && type != typeof(object))
            {
                _renderTextureField = type.GetField("m_RenderTexture", kInstance);
                if (_renderTextureField != null) break;
                type = type.BaseType;
            }
        }

        private static EditorWindow GetGameViewWindow()
        {
            if (_gameViewType == null) return null;

            var allWindows = Resources.FindObjectsOfTypeAll(_gameViewType);
            if (allWindows.Length > 0)
            {
                return allWindows[0] as EditorWindow;
            }

            return null;
        }

        /// <summary>
        /// Release cached resources after recording ends.
        /// </summary>
        public static void ReleaseCachedResources()
        {
            _cachedFlipBuffer = null;
            _cachedWidth = 0;
            _cachedHeight = 0;
        }

        private static void EnsureFlipBuffer(int width, int height)
        {
            if (_cachedWidth == width && _cachedHeight == height && _cachedFlipBuffer != null)
                return;
            _cachedFlipBuffer = new byte[width * height * 4];
            _cachedWidth = width;
            _cachedHeight = height;
        }
        
        public static GifRecordResult ConvertFramesToGif(List<string> framePaths, float scale, int fps, int colorCount)
        {
            var result = new GifRecordResult();

            if (framePaths.Count == 0)
            {
                result.Success = false;
                result.Error = "No frames to convert";
                return result;
            }

            try
            {
                // Load first frame to get dimensions
                var firstTex = LoadTextureFromFile(framePaths[0]);
                if (firstTex == null)
                {
                    result.Success = false;
                    result.Error = "Failed to load first frame";
                    return result;
                }

                int scaledWidth = Mathf.RoundToInt(firstTex.width * scale);
                int scaledHeight = Mathf.RoundToInt(firstTex.height * scale);

                // Create scaled textures
                var frames = new List<Texture2D>();
                foreach (var path in framePaths)
                {
                    var tex = LoadTextureFromFile(path);
                    if (tex == null) continue;

                    var scaled = ScaleTexture(tex, scaledWidth, scaledHeight);
                    UnityEngine.Object.DestroyImmediate(tex);
                    frames.Add(scaled);
                }

                if (frames.Count == 0)
                {
                    result.Success = false;
                    result.Error = "Failed to load any frames";
                    return result;
                }

                // Create GIF
                ScreenshotHelper.EnsureScreenshotsDirectory();
                string filename = $"gif_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.gif";
                string gifPath = Path.Combine(ScreenshotHelper.ScreenshotsDir, filename);

                using (var fs = new FileStream(gifPath, FileMode.Create, FileAccess.Write))
                using (var encoder = new GifEncoder(fs, scaledWidth, scaledHeight, fps, colorCount))
                {
                    int frameDelay = Mathf.Max(1, 100 / fps);

                    for (int i = 0; i < frames.Count; i++)
                    {
                        var pixels = TextureToRgba(frames[i]);
                        // AddFrame will auto-initialize on first frame
                        encoder.AddFrame(pixels, frameDelay);
                    }

                    encoder.Finish();
                }

                // Cleanup textures
                foreach (var tex in frames)
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }

                var fileInfo = new FileInfo(gifPath);
                result.Success = true;
                result.GifPath = gifPath;
                result.Filename = filename;
                result.FrameCount = frames.Count;
                result.Width = scaledWidth;
                result.Height = scaledHeight;
                result.Duration = (float)frames.Count / fps;
                result.FileSize = fileInfo.Length;
                result.Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        private static Texture2D LoadTextureFromFile(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(bytes);
                tex.Apply();
                return tex;
            }
            catch
            {
                return null;
            }
        }

        private static Texture2D ScaleTexture(Texture2D source, int width, int height)
        {
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float v = (float)y / height;
                    result.SetPixel(x, y, source.GetPixelBilinear(u, v));
                }
            }
            result.Apply();
            return result;
        }

        private static byte[] TextureToRgba(Texture2D tex)
        {
            var pixels = tex.GetPixels32();
            int width = tex.width;
            int height = tex.height;
            var result = new byte[width * height * 4];

            // Flip vertically: GIF expects bottom-to-top order
            for (int y = 0; y < height; y++)
            {
                int srcRow = height - 1 - y; // Flip vertically
                for (int x = 0; x < width; x++)
                {
                    int srcIndex = srcRow * width + x;
                    int dstIndex = (y * width + x) * 4;
                    result[dstIndex] = pixels[srcIndex].r;
                    result[dstIndex + 1] = pixels[srcIndex].g;
                    result[dstIndex + 2] = pixels[srcIndex].b;
                    result[dstIndex + 3] = pixels[srcIndex].a;
                }
            }
            return result;
        }

    }
}
