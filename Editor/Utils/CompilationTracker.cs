using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;

namespace AIBridge.Editor
{
    /// <summary>
    /// Tracks Unity compilation state and results.
    /// Listens to CompilationPipeline events and stores compilation errors/warnings.
    /// </summary>
    [InitializeOnLoad]
    public static class CompilationTracker
    {
        /// <summary>
        /// Compilation status
        /// </summary>
        public enum CompilationStatus
        {
            Idle,
            Compiling,
            Success,
            Failed
        }

        /// <summary>
        /// Compiler error/warning information
        /// </summary>
        [Serializable]
        public class CompilerError
        {
            public string file;
            public int line;
            public int column;
            public string message;
            public string errorCode;
            public bool isWarning;
            public string assemblyName;
        }

        /// <summary>
        /// Compilation result data
        /// </summary>
        [Serializable]
        public class CompilationResult
        {
            public CompilationStatus status;
            public DateTime startTime;
            public DateTime? endTime;
            public double durationSeconds;
            public List<CompilerError> errors = new List<CompilerError>();
            public List<CompilerError> warnings = new List<CompilerError>();
            public int errorCount;
            public int warningCount;
        }

        private static CompilationResult _currentResult;
        private static bool _isTracking;

        /// <summary>
        /// Whether currently compiling
        /// </summary>
        public static bool IsCompiling => EditorApplication.isCompiling;

        /// <summary>
        /// Current compilation result
        /// </summary>
        public static CompilationResult CurrentResult => _currentResult;

        static CompilationTracker()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize the tracker and subscribe to compilation events
        /// </summary>
        public static void Initialize()
        {
            if (_isTracking)
            {
                return;
            }

            _currentResult = new CompilationResult
            {
                status = EditorApplication.isCompiling ? CompilationStatus.Compiling : CompilationStatus.Idle
            };

            // Subscribe to compilation events
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;

            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;

            _isTracking = true;
            AIBridgeLogger.LogDebug("CompilationTracker initialized");
        }

        /// <summary>
        /// Start tracking a new compilation
        /// </summary>
        public static void StartTracking()
        {
            _currentResult = new CompilationResult
            {
                status = CompilationStatus.Compiling,
                startTime = DateTime.Now,
                errors = new List<CompilerError>(),
                warnings = new List<CompilerError>()
            };
        }

        /// <summary>
        /// Get the current compilation result
        /// </summary>
        public static CompilationResult GetResult()
        {
            if (_currentResult == null)
            {
                return new CompilationResult
                {
                    status = EditorApplication.isCompiling ? CompilationStatus.Compiling : CompilationStatus.Idle
                };
            }

            // Update status if compilation just finished
            if (_currentResult.status == CompilationStatus.Compiling && !EditorApplication.isCompiling)
            {
                _currentResult.status = _currentResult.errorCount > 0 ? CompilationStatus.Failed : CompilationStatus.Success;
                if (!_currentResult.endTime.HasValue)
                {
                    _currentResult.endTime = DateTime.Now;
                    _currentResult.durationSeconds = (_currentResult.endTime.Value - _currentResult.startTime).TotalSeconds;
                }
            }

            return _currentResult;
        }

        /// <summary>
        /// Reset the tracker to idle state
        /// </summary>
        public static void Reset()
        {
            _currentResult = new CompilationResult
            {
                status = CompilationStatus.Idle
            };
        }

        private static void OnCompilationStarted(object context)
        {
            AIBridgeLogger.LogDebug("Compilation started");
            StartTracking();
        }

        private static void OnCompilationFinished(object context)
        {
            if (_currentResult == null)
            {
                return;
            }

            _currentResult.endTime = DateTime.Now;
            _currentResult.durationSeconds = (_currentResult.endTime.Value - _currentResult.startTime).TotalSeconds;
            _currentResult.status = _currentResult.errorCount > 0 ? CompilationStatus.Failed : CompilationStatus.Success;

            AIBridgeLogger.LogDebug($"Compilation finished: {_currentResult.status}, errors={_currentResult.errorCount}, warnings={_currentResult.warningCount}, duration={_currentResult.durationSeconds:F2}s");
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (_currentResult == null)
            {
                StartTracking();
            }

            var assemblyName = System.IO.Path.GetFileNameWithoutExtension(assemblyPath);

            foreach (var msg in messages)
            {
                var error = new CompilerError
                {
                    file = msg.file,
                    line = msg.line,
                    column = msg.column,
                    message = msg.message,
                    isWarning = msg.type == CompilerMessageType.Warning,
                    assemblyName = assemblyName
                };

                // Try to extract error code from message (format: "CS0001: message")
                if (!string.IsNullOrEmpty(msg.message))
                {
                    var colonIndex = msg.message.IndexOf(':');
                    if (colonIndex > 0 && colonIndex < 10)
                    {
                        var potentialCode = msg.message.Substring(0, colonIndex).Trim();
                        if (potentialCode.StartsWith("CS") || potentialCode.StartsWith("cs"))
                        {
                            error.errorCode = potentialCode.ToUpper();
                        }
                    }
                }

                if (msg.type == CompilerMessageType.Warning)
                {
                    _currentResult.warnings.Add(error);
                    _currentResult.warningCount++;
                }
                else if (msg.type == CompilerMessageType.Error)
                {
                    _currentResult.errors.Add(error);
                    _currentResult.errorCount++;
                }
            }
        }
    }
}
