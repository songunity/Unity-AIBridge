using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
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
    /// Optimized GIF recorder for capturing animated screenshots.
    /// Uses streaming encoding to minimize memory usage.
    /// Features:
    /// - Stream encoding (encode frames immediately, don't store in memory)
    /// - Reuses ScreenshotHelper's cached resources
    /// - Minimal memory footprint (~5MB vs ~100MB for 50 frames)
    /// </summary>
    public static class GifRecorder
    {
        public const int MaxFrameCount = 200;

        public static bool IsRecording { get; private set; }

        // Recording parameters
        private static int _targetFrameCount;
        private static int _fps;
        private static float _scale;
        private static int _colorCount;
        private static Action<GifRecordResult> _onComplete;
        private static Action<int, int> _onProgress;

        // Recording state
        private static int _frameWidth;
        private static int _frameHeight;
        private static double _lastCaptureTime;
        private static double _frameInterval;
        private static int _capturedFrames;
        private static bool _stopRequested;

        // Streaming encoder
        private static FileStream _outputStream;
        private static GifEncoder _encoder;
        private static string _outputPath;
        private static string _outputFilename;
        private static DateTime _recordingStartTime;

        /// <summary>
        /// Start GIF recording with streaming encoding.
        /// </summary>
        public static void StartRecording(
            int frameCount,
            int fps = 20,
            float scale = 0.5f,
            int colorCount = 128,
            Action<GifRecordResult> onComplete = null,
            Action<int, int> onProgress = null)
        {
            if (IsRecording)
            {
                onComplete?.Invoke(new GifRecordResult
                {
                    Success = false,
                    Error = "Recording already in progress."
                });
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                onComplete?.Invoke(new GifRecordResult
                {
                    Success = false,
                    Error = "GIF recording requires Play mode. Please start the game first."
                });
                return;
            }

            // Validate parameters
            _targetFrameCount = Mathf.Clamp(frameCount, 1, MaxFrameCount);
            _fps = Mathf.Clamp(fps, 10, 30);
            _scale = Mathf.Clamp(scale, 0.25f, 1f);
            _colorCount = Mathf.Clamp(colorCount, 64, 256);
            _onComplete = onComplete;
            _onProgress = onProgress;

            // Initialize state
            _frameWidth = 0;
            _frameHeight = 0;
            _frameInterval = 1.0 / _fps;
            _lastCaptureTime = EditorApplication.timeSinceStartup;
            _capturedFrames = 0;
            _stopRequested = false;
            _recordingStartTime = DateTime.Now;

            // Prepare output file
            ScreenshotHelper.EnsureScreenshotsDirectory();
            _outputFilename = $"gif_{_recordingStartTime:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.gif";
            _outputPath = Path.Combine(ScreenshotHelper.ScreenshotsDir, _outputFilename);

            // Encoder will be created on first frame (need dimensions first)
            _outputStream = null;
            _encoder = null;

            IsRecording = true;
            EditorApplication.update += OnUpdate;

            AIBridgeLogger.LogInfo($"GIF recording started: {_targetFrameCount} frames @ {_fps} fps, scale {_scale}");
        }

        /// <summary>
        /// Stop recording early.
        /// </summary>
        public static void StopRecording()
        {
            if (!IsRecording) return;
            _stopRequested = true;
        }

        private static void OnUpdate()
        {
            if (!IsRecording) return;

            if (_stopRequested || !EditorApplication.isPlaying)
            {
                FinishRecording(_stopRequested ? "Recording stopped by user." : "Play mode ended.");
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            double actualInterval = currentTime - _lastCaptureTime;

            if (actualInterval < _frameInterval)
            {
                return;
            }

            // Calculate actual frame delay in 1/100 seconds for GIF encoding
            // This ensures GIF playback speed matches actual recording speed
            int actualFrameDelay = Mathf.Max(1, Mathf.RoundToInt((float)(actualInterval * 100)));

            _lastCaptureTime = currentTime;

            // Capture frame
            var result = ScreenshotHelper.CaptureFrame(_scale);
            if (!result.Success)
            {
                Debug.LogError($"[AIBridge] Frame capture failed: {result.Error}");
                FinishRecording(result.Error);
                return;
            }

            // Initialize encoder on first frame
            if (_encoder == null)
            {
                _frameWidth = result.Width;
                _frameHeight = result.Height;

                Debug.Log($"[AIBridge] GIF frame size: {_frameWidth}x{_frameHeight} (scale: {_scale})");

                try
                {
                    _outputStream = new FileStream(_outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
                    _encoder = new GifEncoder(_outputStream, _frameWidth, _frameHeight, _fps, _colorCount);

                    // Initialize with first frame's colors
                    _encoder.Initialize(result.Pixels);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AIBridge] Failed to create encoder: {ex.Message}\n{ex.StackTrace}");
                    FinishRecording($"Failed to create encoder: {ex.Message}");
                    return;
                }
            }

            // Skip frame if size changed (Game View resized during recording)
            if (result.Width != _frameWidth || result.Height != _frameHeight)
            {
                Debug.LogWarning($"[AIBridge] Frame size changed ({result.Width}x{result.Height} vs {_frameWidth}x{_frameHeight}), skipping frame");
                return;
            }

            // Stream encode frame immediately with actual frame delay
            try
            {
                _encoder.AddFrame(result.Pixels, actualFrameDelay);
                _capturedFrames++;

                // Report progress
                _onProgress?.Invoke(_capturedFrames, _targetFrameCount);

                // Show progress bar occasionally
                if (_capturedFrames % 5 == 0)
                {
                    EditorUtility.DisplayProgressBar(
                        "Recording GIF",
                        $"Frame {_capturedFrames}/{_targetFrameCount}",
                        (float)_capturedFrames / _targetFrameCount);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIBridge] Failed to encode frame: {ex.Message}\n{ex.StackTrace}");
                FinishRecording($"Failed to encode frame: {ex.Message}");
                return;
            }

            // Check if done
            if (_capturedFrames >= _targetFrameCount)
            {
                FinishRecording(null);
            }
        }

        private static void FinishRecording(string error)
        {
            EditorApplication.update -= OnUpdate;
            IsRecording = false;
            EditorUtility.ClearProgressBar();

            // Release ScreenshotHelper's cached resources
            ScreenshotHelper.ReleaseCachedResources();

            bool success = string.IsNullOrEmpty(error) && _capturedFrames > 0;

            // Close encoder and stream
            try
            {
                _encoder?.Dispose();
                _outputStream?.Dispose();
            }
            catch (Exception ex)
            {
                if (success)
                {
                    error = $"Failed to finalize GIF: {ex.Message}";
                    success = false;
                }
            }
            finally
            {
                _encoder = null;
                _outputStream = null;
            }

            if (!success)
            {
                // Delete incomplete file
                try
                {
                    if (File.Exists(_outputPath))
                        File.Delete(_outputPath);
                }
                catch { }

                _onComplete?.Invoke(new GifRecordResult
                {
                    Success = false,
                    Error = error ?? "No frames captured."
                });

                Cleanup();
                return;
            }

            // Get file info
            try
            {
                var fileInfo = new FileInfo(_outputPath);
                float duration = (float)_capturedFrames / _fps;

                AIBridgeLogger.LogInfo($"GIF saved: {_outputPath} ({fileInfo.Length / 1024}KB, {_capturedFrames} frames)");

                _onComplete?.Invoke(new GifRecordResult
                {
                    Success = true,
                    GifPath = _outputPath,
                    Filename = _outputFilename,
                    FrameCount = _capturedFrames,
                    Width = _frameWidth,
                    Height = _frameHeight,
                    Duration = duration,
                    FileSize = fileInfo.Length,
                    Timestamp = _recordingStartTime.ToString("yyyy-MM-ddTHH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                _onComplete?.Invoke(new GifRecordResult
                {
                    Success = false,
                    Error = $"Failed to get file info: {ex.Message}"
                });
            }

            Cleanup();
        }

        private static void Cleanup()
        {
            _onComplete = null;
            _onProgress = null;
            _outputPath = null;
            _outputFilename = null;
        }
    }
}
