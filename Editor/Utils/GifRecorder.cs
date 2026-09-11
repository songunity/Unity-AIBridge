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
        private static long _durationCentiseconds;
        private static bool _stopRequested;
        private static bool _captureStarted;
        private static bool _finishing;
        private static string _finishError;
        private const int MaxQueuedFrames = 4;
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
            _durationCentiseconds = 0;
            _stopRequested = false;
            _captureStarted = false;
            _finishing = false;
            _finishError = null;
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

            // 收尾期间继续让出主线程，等 GPU 回读及编码线程真正结束再释放资源。
            if (_finishing)
            {
                if (!_waitingForReadback && (_encodeThread == null || !_encodeThread.IsAlive))
                    CompleteRecording();
                return;
            }

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

            // 编码落后时暂停采集，避免积压大量 RGBA 数组；下一帧保留实际经过时间。
            if (_waitingForReadback || _encodeQueue.Count >= MaxQueuedFrames) return;

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

            if (!IsRecording || _finishing) return;

            if (request.hasError)
            {
                FinishRecording("AsyncGPUReadback failed.");
                return;
            }

            var data = request.GetData<byte>();
            int width = request.width;
            int height = request.height;

            if (_frameWidth == 0)
            {
                _frameWidth = width;
                _frameHeight = height;
                StartEncodeThread();
            }

            if (width != _frameWidth || height != _frameHeight) return;

            // 回读数据只在回调内有效；主线程仅复制一次，逐行翻转交给编码线程。
            var pixels = data.ToArray();
            _encodeQueue.Enqueue(new FrameData { Pixels = pixels, FrameDelay = _pendingFrameDelay });
            _capturedFrames++;
            _durationCentiseconds += _pendingFrameDelay;
            _onProgress?.Invoke(_capturedFrames, _targetFrameCount);

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
                int rowSize = _frameWidth * 4;
                var rowBuffer = new byte[rowSize];

                while (!_encodingDone || !_encodeQueue.IsEmpty)
                {
                    if (_encodeQueue.TryDequeue(out var frame))
                    {
                        for (int y = 0; y < _frameHeight / 2; y++)
                        {
                            int top = y * rowSize;
                            int bottom = (_frameHeight - 1 - y) * rowSize;
                            Buffer.BlockCopy(frame.Pixels, top, rowBuffer, 0, rowSize);
                            Buffer.BlockCopy(frame.Pixels, bottom, frame.Pixels, top, rowSize);
                            Buffer.BlockCopy(rowBuffer, 0, frame.Pixels, bottom, rowSize);
                        }
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
            if (_finishing) return;
            _finishing = true;
            _finishError = error;
            _encodingDone = true;
        }

        private static void CompleteRecording()
        {
            EditorApplication.update -= OnUpdate;
            IsRecording = false;
            ScreenshotHelper.ReleaseCachedResources();
            _encodeThread = null;
            var error = _finishError;

            // Check for encoding error
            if (_encodeError != null && string.IsNullOrEmpty(error))
            {
                error = _encodeError;
            }

            bool success = string.IsNullOrEmpty(error) && _capturedFrames > 0;

            if (!success)
            {
                try { if (File.Exists(_outputPath)) File.Delete(_outputPath); } catch { }
                var onComplete = _onComplete;
                Cleanup();
                onComplete?.Invoke(new GifRecordResult { Success = false, Error = error ?? "No frames captured." });
                return;
            }

            GifRecordResult result;
            try
            {
                var fileInfo = new FileInfo(_outputPath);
                result = new GifRecordResult
                {
                    Success = true,
                    GifPath = _outputPath,
                    Filename = _outputFilename,
                    FrameCount = _capturedFrames,
                    Width = _frameWidth,
                    Height = _frameHeight,
                    Duration = _durationCentiseconds / 100f,
                    FileSize = fileInfo.Length,
                    Timestamp = _recordingStartTime.ToString("yyyy-MM-ddTHH:mm:ss")
                };
            }
            catch (Exception ex)
            {
                result = new GifRecordResult { Success = false, Error = $"Failed to get file info: {ex.Message}" };
            }

            var callback = _onComplete;
            Cleanup();
            callback?.Invoke(result);
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