using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace AIBridge.Editor
{
    /// <summary>
    /// Watches the commands directory and processes incoming commands
    /// </summary>
    public class CommandWatcher
    {
        /// <summary>
        /// Timeout for stale command/result files (10 minutes)
        /// </summary>
        private static readonly TimeSpan StaleFileTimeout = TimeSpan.FromMinutes(10);

        private readonly string _commandsDir;
        private readonly string _resultsDir;
        private readonly string _codeDir;
        private readonly string _screenshotsDir;
        private readonly CommandQueue _queue;
        private readonly string _statusDir;
        private readonly System.Collections.Generic.Dictionary<string, System.Diagnostics.Stopwatch> _queueTimers = new System.Collections.Generic.Dictionary<string, System.Diagnostics.Stopwatch>();
        private readonly System.Collections.Generic.Dictionary<string, long> _queueTimes = new System.Collections.Generic.Dictionary<string, long>();

        public CommandWatcher(string baseDir)
        {
            _commandsDir = Path.Combine(baseDir, "commands");
            _resultsDir = Path.Combine(baseDir, "results");
            _codeDir = Path.Combine(baseDir, "code");
            _screenshotsDir = Path.Combine(baseDir, "screenshots");
            _queue = new CommandQueue();
            _statusDir = Path.Combine(baseDir, "status");

            EnsureDirectoriesExist();
        }

        /// <summary>
        /// Scan for new command files and enqueue them
        /// </summary>
        public void ScanForCommands()
        {
            if (!Directory.Exists(_commandsDir))
            {
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(_commandsDir, "*.json");
            }
            catch (Exception ex)
            {
                AIBridgeLogger.LogError($"Failed to scan commands directory: {ex.Message}");
                return;
            }

            foreach (var file in files)
            {
                try
                {
                    // Check if file is stale (older than timeout)
                    var fileInfo = new FileInfo(file);
                    var fileAge = DateTime.UtcNow - fileInfo.LastWriteTimeUtc;
                    if (fileAge > StaleFileTimeout)
                    {
                        AIBridgeLogger.LogWarning($"Cleaning up stale command file: {Path.GetFileName(file)} (age: {fileAge.TotalMinutes:F1} minutes)");
                        File.Delete(file);
                        continue;
                    }

                    var json = File.ReadAllText(file, System.Text.Encoding.UTF8);

                    // Use Newtonsoft.Json for proper Dictionary support
                    var jObject = JObject.Parse(json);
                    var request = new CommandRequest
                    {
                        id = jObject["id"]?.ToString(),
                        type = jObject["type"]?.ToString(),
                        @params = new System.Collections.Generic.Dictionary<string, object>()
                    };

                    // Parse params
                    var paramsObj = jObject["params"] as JObject;
                    if (paramsObj != null)
                    {
                        foreach (var prop in paramsObj.Properties())
                        {
                            request.@params[prop.Name] = ConvertJTokenToObject(prop.Value);
                        }
                    }

                    if (request != null && !string.IsNullOrEmpty(request.id)
                        && System.Text.RegularExpressions.Regex.IsMatch(request.id, "^[a-zA-Z0-9_-]+$"))
                    {
                        // 磁盘状态同时作为当前保留窗口内的去重依据，域重载后也不重放。
                        if (File.Exists(Path.Combine(_resultsDir, request.id + ".json")) || File.Exists(Path.Combine(_statusDir, request.id + ".json")))
                        {
                            File.Delete(file);
                            continue;
                        }
                        if ((int?)jObject["protocolVersion"] != 2)
                        {
                            var mismatch = CommandResult.FailureWithId(request.id, "Update CLI and Editor package together (protocol 2 required).");
                            mismatch.errorCode = "PROTOCOL_MISMATCH";
                            WriteResult(mismatch);
                            File.Delete(file);
                            continue;
                        }
                        if (_queue.Enqueue(request))
                        {
                            _queueTimers[request.id] = System.Diagnostics.Stopwatch.StartNew();
                            WriteStatus(request.id, "queued");
                            AIBridgeLogger.LogDebug($"Enqueued command: {request.id} ({request.type})");
                            // Delete the command file after reading
                            File.Delete(file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AIBridgeLogger.LogError($"Failed to parse command file {file}: {ex.Message}");
                    // Move failed file to prevent repeated errors
                    try
                    {
                        File.Move(file, file + ".error");
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }

            // Periodically trim processed IDs
            _queue.TrimProcessedIds();

            // Cleanup stale result files and error files
            CleanupStaleFiles();

            // Cleanup old screenshots (1 day retention)
            ScreenshotCacheManager.CleanupOldScreenshots();
        }

        /// <summary>
        /// Clean up stale result files and error command files
        /// </summary>
        private void CleanupStaleFiles()
        {
            // Cleanup stale result files
            if (Directory.Exists(_resultsDir))
            {
                try
                {
                    var resultFiles = Directory.GetFiles(_resultsDir, "*.json");
                    foreach (var file in resultFiles)
                    {
                        var fileInfo = new FileInfo(file);
                        var fileAge = DateTime.UtcNow - fileInfo.LastWriteTimeUtc;
                        if (fileAge > StaleFileTimeout)
                        {
                            File.Delete(file);
                            AIBridgeLogger.LogDebug($"Cleaned up stale result file: {Path.GetFileName(file)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AIBridgeLogger.LogError($"Failed to cleanup stale result files: {ex.Message}");
                }
            }

            foreach (var file in Directory.GetFiles(_statusDir, "*.json"))
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) <= StaleFileTimeout) continue;
                var state = JObject.Parse(File.ReadAllText(file));
                var active = (string)state["sessionId"] == EditorInstanceTracker.SessionId
                    && ((string)state["status"] == "queued" || (string)state["status"] == "running");
                if (!active) File.Delete(file);
            }

            // Cleanup stale error files in commands directory
            if (Directory.Exists(_commandsDir))
            {
                try
                {
                    var errorFiles = Directory.GetFiles(_commandsDir, "*.error");
                    foreach (var file in errorFiles)
                    {
                        var fileInfo = new FileInfo(file);
                        var fileAge = DateTime.UtcNow - fileInfo.LastWriteTimeUtc;
                        if (fileAge > StaleFileTimeout)
                        {
                            File.Delete(file);
                            AIBridgeLogger.LogDebug($"Cleaned up stale error file: {Path.GetFileName(file)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AIBridgeLogger.LogError($"Failed to cleanup stale error files: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Process one pending command
        /// </summary>
        /// <returns>True if a command was processed</returns>
        public bool ProcessOneCommand()
        {
            if (!_queue.TryDequeue(out var request))
            {
                return false;
            }

            if (_queueTimers.TryGetValue(request.id, out var timer))
            {
                _queueTimes[request.id] = timer.ElapsedMilliseconds;
                _queueTimers.Remove(request.id);
            }
            WriteStatus(request.id, "running");
            if (!CommandRegistry.TryGetCommand(request.type, out var entry))
            {
                WriteResult(CommandResult.FailureWithId(request.id, $"Unknown command: {request.type}"));
                return true;
            }

            if (!CommandParamBinder.TryBind(entry, request, out var args, out var bindError))
            {
                WriteResult(CommandResult.FailureWithId(request.id, bindError));
                return true;
            }

            try
            {
                var coroutine = (System.Collections.IEnumerator)entry.Method.Invoke(null, args);
                EditorCoroutineRunner.Start(coroutine, WriteResult, request.id);
            }
            catch (Exception ex)
            {
                WriteResult(CommandResult.FromException(request.id, ex.InnerException ?? ex));
            }
            AIBridgeLogger.LogDebug($"Command {request.id} ({request.type}) started async processing");

            return true;
        }

        /// <summary>
        /// Write command result to file
        /// </summary>
        private void WriteResult(CommandResult result)
        {
            EnsureDirectoriesExist();

            result.status = result.errorCode == "EXECUTION_WAIT_TIMEOUT" ? "unknown" : result.success ? "completed" : "failed";
            _queueTimes.TryGetValue(result.id, out var queuedMs);
            _queueTimes.Remove(result.id);
            result.timings = new { queuedMs, executionMs = result.executionTime };
            var filePath = Path.Combine(_resultsDir, $"{result.id}.json");

            try
            {
                // Use Newtonsoft.Json for proper object serialization (supports anonymous types)
                var json = JsonConvert.SerializeObject(result, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
                var tmpPath = filePath + ".tmp";
                File.WriteAllText(tmpPath, json, System.Text.Encoding.UTF8);
                File.Move(tmpPath, filePath);
                WriteStatus(result.id, result.status);
            }
            catch (Exception ex)
            {
                WriteStatus(result.id, "unknown");
                AIBridgeLogger.LogError($"Failed to write result for {result.id}: {ex.Message}");
            }
        }

        /// <summary>状态在主线程原子发布；会话标识用于识别重载后无法确认的操作。</summary>
        private void WriteStatus(string id, string status)
        {
            var path = Path.Combine(_statusDir, id + ".json");
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(new { id, protocolVersion = 2, status, sessionId = EditorInstanceTracker.SessionId, updatedAtUtc = DateTime.UtcNow.ToString("O") }));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        /// <summary>确保命令、状态及结果目录存在。</summary>
        private void EnsureDirectoriesExist()
        {
            try
            {
                Directory.CreateDirectory(_statusDir);
                if (!Directory.Exists(_commandsDir))
                {
                    Directory.CreateDirectory(_commandsDir);
                }

                if (!Directory.Exists(_resultsDir))
                {
                    Directory.CreateDirectory(_resultsDir);
                }

                if (!Directory.Exists(_codeDir))
                {
                    Directory.CreateDirectory(_codeDir);
                }

                if (!Directory.Exists(_screenshotsDir))
                {
                    Directory.CreateDirectory(_screenshotsDir);
                }
            }
            catch (Exception ex)
            {
                AIBridgeLogger.LogError($"Failed to create directories: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert JToken to appropriate .NET object
        /// </summary>
        private object ConvertJTokenToObject(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var dict = new System.Collections.Generic.Dictionary<string, object>();
                    foreach (var prop in ((JObject)token).Properties())
                    {
                        dict[prop.Name] = ConvertJTokenToObject(prop.Value);
                    }
                    return dict;

                case JTokenType.Array:
                    var list = new System.Collections.Generic.List<object>();
                    foreach (var item in (JArray)token)
                    {
                        list.Add(ConvertJTokenToObject(item));
                    }
                    return list;

                case JTokenType.Integer:
                    return token.Value<long>();

                case JTokenType.Float:
                    return token.Value<double>();

                case JTokenType.String:
                    return token.Value<string>();

                case JTokenType.Boolean:
                    return token.Value<bool>();

                case JTokenType.Null:
                    return null;

                default:
                    return token.ToString();
            }
        }
    }
}
