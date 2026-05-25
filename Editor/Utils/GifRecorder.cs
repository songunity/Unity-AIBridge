using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AIBridge.Editor
{
    public static class GifRecorder
    {
        public const int MaxFrameCount = 200;

        public static bool IsRecording { get; private set; }

        private static int _targetFrameCount;
        private static int _fps;
        private static float _scale;
        private static int _colorCount;
        private static float _startDelay;
        private static Action<GifRecordResult> _onComplete;
        private static Action<int, int> _onProgress;

        private static int _frameWidth;
        private static int _frameHeight;
        private static double _lastCaptureTime;
        private static double _frameInterval;
        private static int _capturedFrames;
        private static bool _stopRequested;
        private static bool _captureStarted;
        private static double _captureStartTime;

        private static FileStream _outputStream;
        private static GifEncoder _encoder;
        private static string _outputPath;
        private static string _outputFilename;
        private static DateTime _recordingStartTime;

        // Async readback state
        private static bool _waitingForReadback;
        private static int _pendingFrameDelay;

        public static void StartRecording(
            int frameCount,
            int fps = 20,
            float scale = 0.5f,
            int colorCount = 128,
            float startDelay = 0.1f,
            Action<GifRecordResult> onComplete = null,
            Action<int, int> onProgress = null)
        {
            if (IsRecording)
            {
                onComplete?.Invoke(new GifRecordResult { Success = false, Error = "Recording already in progress." });
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                onComplete?.Invoke(new GifRecordResult { Success = false, Error = "GIF recording requires Play mode." });
                return;
            }

            _targetFrameCount = Mathf.Clamp(frameCount, 1, MaxFrameCount);
            _fps = Mathf.Clamp(fps, 10, 30);
            _scale = Mathf.Clamp(scale, 0.25f, 1f);
            _colorCount = Mathf.Clamp(colorCount, 64, 256);
            _startDelay = Mathf.Clamp(startDelay, 0f, 5f);
            _onComplete = onComplete;
            _onProgress = onProgress;

            _frameWidth = 0;
            _frameHeight = 0;
            _frameInterval = 1.0 / _fps;
            _lastCaptureTime = 0;
            _capturedFrames = 0;
            _stopRequested = false;
            _captureStarted = false;
            _waitingForReadback = false;
            _captureStartTime = EditorApplication.timeSinceStartup + _startDelay;
            _recordingStartTime = DateTime.Now;

            ScreenshotHelper.EnsureScreenshotsDirectory();
            _outputFilename = $"gif_{_recordingStartTime:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.gif";
            _outputPath = Path.Combine(ScreenshotHelper.ScreenshotsDir, _outputFilename);

            _outputStream = null;
            _encoder = null;

            IsRecording = true;
            EditorApplication.update += OnUpdate;
        }

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

            // Still waiting for previous async readback
            if (_waitingForReadback) return;

            double currentTime = EditorApplication.timeSinceStartup;

            if (!_captureStarted)
            {
                if (currentTime < _captureStartTime) return;
                _captureStarted = true;
                _lastCaptureTime = currentTime - _frameInterval;
            }

            double actualInterval = currentTime - _lastCaptureTime;
            if (actualInterval < _frameInterval) return;

            _pendingFrameDelay = Mathf.Max(1, Mathf.RoundToInt((float)(actualInterval * 100)));
            _lastCaptureTime = currentTime;

            // Get Game View RT and request async readback
            var sourceRt = ScreenshotHelper.GetScaledRenderTexture(_scale);
            if (sourceRt == null)
            {
                FinishRecording("Cannot access Game View render texture.");
                return;
            }

            _waitingForReadback = true;
            AsyncGPUReadback.Request(sourceRt, 0, TextureFormat.RGBA32, OnReadbackComplete);
        }

        private static void OnReadbackComplete(AsyncGPUReadbackRequest request)
        {
            _waitingForReadback = false;

            if (!IsRecording) return;

            if (request.hasError)
            {
                FinishRecording("AsyncGPUReadback failed.");
                return;
            }

            var data = request.GetData<byte>();
            int width = request.width;
            int height = request.height;

            // Flip vertically
            int rowSize = width * 4;
            var pixels = new byte[data.Length];
            for (int y = 0; y < height; y++)
            {
                int srcOffset = y * rowSize;
                int dstOffset = (height - 1 - y) * rowSize;
                Unity.Collections.NativeArray<byte>.Copy(data, srcOffset, pixels, dstOffset, rowSize);
            }

            if (_encoder == null)
            {
                _frameWidth = width;
                _frameHeight = height;
                try
                {
                    _outputStream = new FileStream(_outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
                    _encoder = new GifEncoder(_outputStream, _frameWidth, _frameHeight, _fps, _colorCount);
                    _encoder.Initialize(pixels);
                }
                catch (Exception ex)
                {
                    FinishRecording($"Failed to create encoder: {ex.Message}");
                    return;
                }
            }

            if (width != _frameWidth || height != _frameHeight) return;

            try
            {
                _encoder.AddFrame(pixels, _pendingFrameDelay);
                _capturedFrames++;
                _onProgress?.Invoke(_capturedFrames, _targetFrameCount);

                if (_capturedFrames % 5 == 0)
                {
                    EditorUtility.DisplayProgressBar("Recording GIF",
                        $"Frame {_capturedFrames}/{_targetFrameCount}",
                        (float)_capturedFrames / _targetFrameCount);
                }
            }
            catch (Exception ex)
            {
                FinishRecording($"Failed to encode frame: {ex.Message}");
                return;
            }

            if (_capturedFrames >= _targetFrameCount)
            {
                FinishRecording(null);
            }
        }

        private static void FinishRecording(string error)
        {
            EditorApplication.update -= OnUpdate;
            IsRecording = false;
            _waitingForReadback = false;
            EditorUtility.ClearProgressBar();
            ScreenshotHelper.ReleaseCachedResources();

            bool success = string.IsNullOrEmpty(error) && _capturedFrames > 0;

            try
            {
                _encoder?.Dispose();
                _outputStream?.Dispose();
            }
            catch (Exception ex)
            {
                if (success) { error = $"Failed to finalize GIF: {ex.Message}"; success = false; }
            }
            finally
            {
                _encoder = null;
                _outputStream = null;
            }

            if (!success)
            {
                try { if (File.Exists(_outputPath)) File.Delete(_outputPath); } catch { }
                _onComplete?.Invoke(new GifRecordResult { Success = false, Error = error ?? "No frames captured." });
                Cleanup();
                return;
            }

            try
            {
                var fileInfo = new FileInfo(_outputPath);
                _onComplete?.Invoke(new GifRecordResult
                {
                    Success = true,
                    GifPath = _outputPath,
                    Filename = _outputFilename,
                    FrameCount = _capturedFrames,
                    Width = _frameWidth,
                    Height = _frameHeight,
                    Duration = (float)_capturedFrames / _fps,
                    FileSize = fileInfo.Length,
                    Timestamp = _recordingStartTime.ToString("yyyy-MM-ddTHH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                _onComplete?.Invoke(new GifRecordResult { Success = false, Error = $"Failed to get file info: {ex.Message}" });
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