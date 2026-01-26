using System;
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
        public byte[] Pixels;  // RGBA32 pixel data
        public int Width;
        public int Height;
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
                    _screenshotsDir = Path.Combine(projectRoot, "AIBridgeCache", "screenshots");
                }
                return _screenshotsDir;
            }
        }

        /// <summary>
        /// Capture Game view screenshot.
        /// </summary>
        public static ScreenshotResult CaptureGameView(bool checkPlayMode = true)
        {
            if (checkPlayMode && !EditorApplication.isPlaying)
            {
                return new ScreenshotResult
                {
                    Success = false,
                    Error = "Screenshot requires Play mode. Please start the game first."
                };
            }

            EnsureScreenshotsDirectory();

            var timestamp = DateTime.Now;
            var filename = $"game_{timestamp:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.jpg";
            var fullPath = Path.Combine(ScreenshotsDir, filename);

            try
            {
                var allCameras = Camera.allCameras;
                if (allCameras.Length == 0)
                {
                    return new ScreenshotResult
                    {
                        Success = false,
                        Error = "No active camera found in scene. Cannot capture screenshot."
                    };
                }

                Array.Sort(allCameras, (a, b) => a.depth.CompareTo(b.depth));

                var gameViewSize = GetGameViewSizeCached();
                int width = (int)gameViewSize.x;
                int height = (int)gameViewSize.y;

                if (width <= 0 || height <= 0)
                {
                    width = Screen.width;
                    height = Screen.height;
                }

                var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                var texture2D = new Texture2D(width, height, TextureFormat.ARGB32, false);

                foreach (var camera in allCameras)
                {
                    if (!camera.enabled || !camera.gameObject.activeInHierarchy)
                        continue;

                    var originalTarget = camera.targetTexture;
                    camera.targetTexture = renderTexture;
                    camera.Render();
                    camera.targetTexture = originalTarget;
                }

                RenderTexture.active = renderTexture;
                texture2D.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture2D.Apply();
                RenderTexture.active = null;

                var jpgData = texture2D.EncodeToJPG(85);
                File.WriteAllBytes(fullPath, jpgData);

                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(texture2D);

                AIBridgeLogger.LogInfo($"Screenshot saved: {fullPath}");

                return new ScreenshotResult
                {
                    Success = true,
                    ImagePath = fullPath,
                    Filename = filename,
                    Width = width,
                    Height = height,
                    Timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss")
                };
            }
            catch (Exception ex)
            {
                return new ScreenshotResult
                {
                    Success = false,
                    Error = $"Failed to capture screenshot: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Capture a single frame for GIF recording.
        /// Optimized with cached RenderTexture and Texture2D.
        /// </summary>
        public static FrameCaptureResult CaptureFrame(float scale = 1f)
        {
            if (!EditorApplication.isPlaying)
            {
                return new FrameCaptureResult
                {
                    Success = false,
                    Error = "Frame capture requires Play mode."
                };
            }

            try
            {
                var allCameras = Camera.allCameras;
                if (allCameras.Length == 0)
                {
                    return new FrameCaptureResult
                    {
                        Success = false,
                        Error = "No active camera found."
                    };
                }

                Array.Sort(allCameras, (a, b) => a.depth.CompareTo(b.depth));

                int width, height;

                // If we have cached resources, use those dimensions for consistency
                // This ensures all frames in a GIF recording have the same size
                if (_cachedRenderTexture != null && _cachedWidth > 0 && _cachedHeight > 0)
                {
                    width = _cachedWidth;
                    height = _cachedHeight;
                }
                else
                {
                    // First frame: get Game View size (not cached version to ensure fresh value)
                    var gameViewSize = GetGameViewSize();
                    int baseWidth = (int)gameViewSize.x;
                    int baseHeight = (int)gameViewSize.y;

                    if (baseWidth <= 0 || baseHeight <= 0)
                    {
                        baseWidth = Screen.width;
                        baseHeight = Screen.height;
                    }

                    scale = Mathf.Clamp(scale, 0.25f, 1f);
                    width = Mathf.Max(1, (int)(baseWidth * scale));
                    height = Mathf.Max(1, (int)(baseHeight * scale));
                }

                // Reuse or create cached resources
                EnsureCachedResources(width, height);

                // Render each camera
                foreach (var camera in allCameras)
                {
                    if (!camera.enabled || !camera.gameObject.activeInHierarchy)
                        continue;

                    var originalTarget = camera.targetTexture;
                    camera.targetTexture = _cachedRenderTexture;
                    camera.Render();
                    camera.targetTexture = originalTarget;
                }

                // Read pixels
                RenderTexture.active = _cachedRenderTexture;
                _cachedTexture2D.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                _cachedTexture2D.Apply();
                RenderTexture.active = null;

                // Get raw pixel data and flip
                var pixels = _cachedTexture2D.GetRawTextureData();
                FlipVerticallyInPlace(pixels, _cachedFlipBuffer, width, height);

                // Return a copy of the flipped buffer
                var result = new byte[_cachedFlipBuffer.Length];
                Buffer.BlockCopy(_cachedFlipBuffer, 0, result, 0, result.Length);

                return new FrameCaptureResult
                {
                    Success = true,
                    Pixels = result,
                    Width = width,
                    Height = height
                };
            }
            catch (Exception ex)
            {
                return new FrameCaptureResult
                {
                    Success = false,
                    Error = $"Failed to capture frame: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Ensure cached resources exist and have correct size.
        /// </summary>
        private static void EnsureCachedResources(int width, int height)
        {
            if (_cachedRenderTexture != null && _cachedWidth == width && _cachedHeight == height)
                return;

            // Cleanup old resources
            ReleaseCachedResources();

            // Create new resources
            _cachedRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            _cachedTexture2D = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _cachedFlipBuffer = new byte[width * height * 4];
            _cachedWidth = width;
            _cachedHeight = height;
        }

        /// <summary>
        /// Release cached resources. Call when recording ends.
        /// </summary>
        public static void ReleaseCachedResources()
        {
            if (_cachedRenderTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_cachedRenderTexture);
                _cachedRenderTexture = null;
            }

            if (_cachedTexture2D != null)
            {
                UnityEngine.Object.DestroyImmediate(_cachedTexture2D);
                _cachedTexture2D = null;
            }

            _cachedFlipBuffer = null;
            _cachedWidth = 0;
            _cachedHeight = 0;
        }

        /// <summary>
        /// Get GameView size with caching to avoid reflection every frame.
        /// </summary>
        private static Vector2 GetGameViewSizeCached()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - _lastGameViewSizeCheck < GameViewSizeCacheInterval && _cachedGameViewSize != Vector2.zero)
            {
                return _cachedGameViewSize;
            }

            _lastGameViewSizeCheck = currentTime;
            _cachedGameViewSize = GetGameViewSize();
            return _cachedGameViewSize;
        }

        /// <summary>
        /// Ensure screenshots directory exists.
        /// </summary>
        public static void EnsureScreenshotsDirectory()
        {
            if (!Directory.Exists(ScreenshotsDir))
            {
                Directory.CreateDirectory(ScreenshotsDir);
                var gitignorePath = Path.Combine(ScreenshotsDir, ".gitignore");
                File.WriteAllText(gitignorePath, "*\n!.gitignore\n");
            }
        }


        /// <summary>
        /// Flip pixel data vertically using pre-allocated buffer.
        /// </summary>
        private static void FlipVerticallyInPlace(byte[] src, byte[] dst, int width, int height)
        {
            int rowSize = width * 4;

            for (int y = 0; y < height; y++)
            {
                int srcRow = y * rowSize;
                int dstRow = (height - 1 - y) * rowSize;
                Buffer.BlockCopy(src, srcRow, dst, dstRow, rowSize);
            }
        }

        /// <summary>
        /// Get Game View render target size.
        /// Uses multiple methods to ensure correct resolution is obtained.
        /// </summary>
        private static Vector2 GetGameViewSize()
        {
            try
            {
                // Method 1: Use Screen.width/height in Play mode (most reliable)
                // This returns the actual game resolution, not the window size
                if (EditorApplication.isPlaying)
                {
                    // Get the current render resolution from the main camera
                    var mainCamera = Camera.main;
                    if (mainCamera != null)
                    {
                        return new Vector2(mainCamera.pixelWidth, mainCamera.pixelHeight);
                    }

                    // Fallback to any active camera
                    var allCameras = Camera.allCameras;
                    if (allCameras.Length > 0)
                    {
                        // Find the camera with highest depth (usually the main render camera)
                        Camera bestCamera = null;
                        float highestDepth = float.MinValue;
                        foreach (var cam in allCameras)
                        {
                            if (cam.enabled && cam.gameObject.activeInHierarchy && cam.depth > highestDepth)
                            {
                                highestDepth = cam.depth;
                                bestCamera = cam;
                            }
                        }
                        if (bestCamera != null)
                        {
                            return new Vector2(bestCamera.pixelWidth, bestCamera.pixelHeight);
                        }
                    }
                }

                // Method 2: Try reflection to get Game View size
                var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameViewType != null)
                {
                    var getMainGameView = gameViewType.GetMethod("GetMainGameView", BindingFlags.NonPublic | BindingFlags.Static);
                    if (getMainGameView != null)
                    {
                        var gameView = getMainGameView.Invoke(null, null);
                        if (gameView != null)
                        {
                            // Try targetSize property
                            var targetSizeProperty = gameViewType.GetProperty("targetSize", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (targetSizeProperty != null)
                            {
                                var targetSize = (Vector2)targetSizeProperty.GetValue(gameView);
                                if (targetSize.x > 0 && targetSize.y > 0)
                                    return targetSize;
                            }
                        }
                    }

                    // Try GetSizeOfMainGameView static method
                    var getSizeMethod = gameViewType.GetMethod("GetSizeOfMainGameView", BindingFlags.NonPublic | BindingFlags.Static);
                    if (getSizeMethod != null)
                    {
                        var size = (Vector2)getSizeMethod.Invoke(null, null);
                        if (size.x > 0 && size.y > 0)
                            return size;
                    }
                }

                // Method 3: Fallback to Screen dimensions
                if (Screen.width > 0 && Screen.height > 0)
                {
                    return new Vector2(Screen.width, Screen.height);
                }

                return Vector2.zero;
            }
            catch
            {
                return Vector2.zero;
            }
        }
    }
}
