using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
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

        // Background encoding
        private static ConcurrentQueue<FrameData> _encodeQueue;
        private static Thread _encodeThread;
        private static volatile bool _encodingDone;
        private static volatile string _encodeError;

        private struct FrameData
        {
            public byte[] Pixels;
            public int FrameDelay;
        }

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
            _encodeQueue = new ConcurrentQueue<FrameData>();
            _encodingDone = false;
            _encodeError = null;
            _encodeThread = null;

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

            // Check for encoding errors from background thread
            if (_encodeError != null)
            {
                FinishRecording(_encodeError);
                return;
            }

            if (_stopRequested || !EditorApplication.isPlaying)
            {
                FinishRecording(_stopRequested ? "Recording stopped by user." : "Play mode ended.");
                return;
            }

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

            if (_frameWidth == 0)
            {
                _frameWidth = width;
                _frameHeight = height;
                StartEncodeThread();
            }

            if (width != _frameWidth || height != _frameHeight) return;

            _encodeQueue.Enqueue(new FrameData { Pixels = pixels, FrameDelay = _pendingFrameDelay });
            _capturedFrames++;
            _onProgress?.Invoke(_capturedFrames, _targetFrameCount);

            if (_capturedFrames % 5 == 0)
            {
                EditorUtility.DisplayProgressBar("Recording GIF",
                    $"Frame {_capturedFrames}/{_targetFrameCount}",
                    (float)_capturedFrames / _targetFrameCount);
            }

            if (_capturedFrames >= _targetFrameCount)
            {
                FinishRecording(null);
            }
        }

        private static void StartEncodeThread()
        {
            _encodeThread = new Thread(EncodeThreadLoop)
            {
                IsBackground = true,
                Name = "AIBridge-GifEncoder"
            };
            _encodeThread.Start();
        }

        private static void EncodeThreadLoop()
        {
            try
            {
                _outputStream = new FileStream(_outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
                _encoder = new GifEncoder(_outputStream, _frameWidth, _frameHeight, _fps, _colorCount);

                bool initialized = false;

                while (!_encodingDone || !_encodeQueue.IsEmpty)
                {
                    if (_encodeQueue.TryDequeue(out var frame))
                    {
                        if (!initialized)
                        {
                            _encoder.Initialize(frame.Pixels);
                            initialized = true;
                        }
                        _encoder.AddFrame(frame.Pixels, frame.FrameDelay);
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }

                _encoder.Dispose();
                _outputStream.Dispose();
            }
            catch (Exception ex)
            {
                _encodeError = $"Encoding failed: {ex.Message}";
                try { _encoder?.Dispose(); } catch { }
                try { _outputStream?.Dispose(); } catch { }
            }
            finally
            {
                _encoder = null;
                _outputStream = null;
            }
        }

        private static void FinishRecording(string error)
        {
            EditorApplication.update -= OnUpdate;
            IsRecording = false;
            _waitingForReadback = false;
            EditorUtility.ClearProgressBar();
            ScreenshotHelper.ReleaseCachedResources();

            // Signal encode thread to finish and wait
            _encodingDone = true;
            if (_encodeThread != null && _encodeThread.IsAlive)
            {
                _encodeThread.Join(5000);
            }
            _encodeThread = null;

            // Check for encoding error
            if (_encodeError != null && string.IsNullOrEmpty(error))
            {
                error = _encodeError;
            }

            bool success = string.IsNullOrEmpty(error) && _capturedFrames > 0;

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
            _encodeQueue = null;
        }
    }
}