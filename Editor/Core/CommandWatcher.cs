using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AIBridge.Editor
{
    /// <summary>通过原子文件交接接收命令；持久化失败只重试交接，不重放业务。</summary>
    public class CommandWatcher
    {
        private static readonly TimeSpan StaleFileTimeout = TimeSpan.FromMinutes(10);
        private const int StatusRetryMilliseconds = 2000;
        private readonly string _commandsDir;
        private readonly string _resultsDir;
        private readonly string _codeDir;
        private readonly string _screenshotsDir;
        private readonly string _statusDir;
        private readonly CommandQueue _queue = new CommandQueue();
        private readonly Dictionary<string, Stopwatch> _queueTimers = new Dictionary<string, Stopwatch>();
        private readonly Dictionary<string, long> _queueTimes = new Dictionary<string, long>();
        private readonly Dictionary<string, Stopwatch> _statusFailures = new Dictionary<string, Stopwatch>();
        private readonly Dictionary<string, string> _pendingStatuses = new Dictionary<string, string>();
        private readonly Dictionary<string, PendingResult> _pendingResults = new Dictionary<string, PendingResult>();
        private readonly HashSet<string> _published = new HashSet<string>();
        private DateTime _nextCleanup;

        private enum StatusWrite { Written, Waiting, Failed }

        /// <summary>只保存第一次完成的结果；后续发布重试不再调用命令入口。</summary>
        private sealed class PendingResult
        {
            public CommandResult Result;
            public string Json;
            public readonly Stopwatch Age = Stopwatch.StartNew();
            public long NextAttemptMs;
            public bool Warned;
        }

        public CommandWatcher(string baseDir)
        {
            _commandsDir = Path.Combine(baseDir, "commands");
            _resultsDir = Path.Combine(baseDir, "results");
            _codeDir = Path.Combine(baseDir, "code");
            _screenshotsDir = Path.Combine(baseDir, "screenshots");
            _statusDir = Path.Combine(baseDir, "status");
            EnsureDirectoriesExist();
        }

        /// <summary>重试待发布结果，再扫描请求；临时 IO 失败不冒充 JSON 解析错误。</summary>
        public void ScanForCommands()
        {
            foreach (var id in new List<string>(_pendingResults.Keys)) TryPublishResult(id);
            foreach (var id in new List<string>(_pendingStatuses.Keys))
            {
                var state = _pendingStatuses[id];
                if (TryWriteStatus(id, state, out _) != StatusWrite.Waiting) _pendingStatuses.Remove(id);
            }
            string[] files;
            try { files = Directory.GetFiles(_commandsDir, "*.json"); }
            catch (IOException ex) { AIBridgeLogger.LogDebug("Command scan delayed: " + ex.Message); return; }
            catch (UnauthorizedAccessException ex) { AIBridgeLogger.LogDebug("Command scan denied: " + ex.Message); return; }
            foreach (var file in files)
            {
                try
                {
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > StaleFileTimeout)
                    {
                        TryDeleteInput(file);
                        continue;
                    }
                    var obj = JObject.Parse(ReadPublishedText(file));
                    var id = (string)obj["id"];
                    if (string.IsNullOrEmpty(id) || !System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-zA-Z0-9_-]+$"))
                        throw new JsonException("Invalid command ID");
                    // 内存和磁盘都参与去重；不能因输入文件删除失败再次入队。
                    if (_queue.IsProcessed(id) || _pendingResults.ContainsKey(id) || _published.Contains(id)
                        || File.Exists(Path.Combine(_resultsDir, id + ".json")) || File.Exists(Path.Combine(_statusDir, id + ".json")))
                    {
                        TryDeleteInput(file);
                        continue;
                    }
                    var request = new CommandRequest { id = id, type = (string)obj["type"], @params = new Dictionary<string, object>() };
                    if (obj["params"] is JObject parameters)
                        foreach (var property in parameters.Properties()) request.@params[property.Name] = ConvertJTokenToObject(property.Value);
                    if ((int?)obj["protocolVersion"] != 2)
                    {
                        var mismatch = CommandResult.FailureWithId(id, "Update CLI and Editor package together (protocol 2 required).");
                        mismatch.errorCode = "PROTOCOL_MISMATCH";
                        WriteResult(mismatch);
                        TryDeleteInput(file);
                        continue;
                    }
                    var status = TryWriteStatus(id, "queued", out var error);
                    if (status == StatusWrite.Waiting) continue;
                    if (status == StatusWrite.Failed)
                    {
                        WriteResult(StatusFailure(id, error));
                        TryDeleteInput(file);
                        continue;
                    }
                    if (_queue.Enqueue(request))
                    {
                        _queueTimers[id] = Stopwatch.StartNew();
                        TryDeleteInput(file);
                    }
                }
                catch (IOException ex) { AIBridgeLogger.LogDebug("Command file temporarily unavailable: " + ex.Message); }
                catch (UnauthorizedAccessException ex) { AIBridgeLogger.LogDebug("Command file access denied: " + ex.Message); }
                catch (Exception ex)
                {
                    AIBridgeLogger.LogError($"Invalid command file {Path.GetFileName(file)}: {ex.Message}");
                    try { File.Move(file, file + ".error"); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            _queue.TrimProcessedIds();
            CleanupStaleFiles();
            ScreenshotCacheManager.CleanupOldScreenshots();
        }

        /// <summary>状态写入期间仍持有队首，只有确定可执行或已明确拒绝时才出队。</summary>
        public bool ProcessOneCommand()
        {
            if (!_queue.TryPeek(out var request)) return false;
            var status = TryWriteStatus(request.id, "running", out var error);
            if (status == StatusWrite.Waiting) return false;
            _queue.TryDequeue(out _);
            if (_queueTimers.TryGetValue(request.id, out var timer))
            {
                _queueTimes[request.id] = timer.ElapsedMilliseconds;
                _queueTimers.Remove(request.id);
            }
            if (status == StatusWrite.Failed)
            {
                WriteResult(StatusFailure(request.id, error));
                return true;
            }
            if (!CommandRegistry.TryGetCommand(request.type, out var entry))
            {
                WriteResult(CommandResult.FailureWithId(request.id, "Unknown command: " + request.type));
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
            catch (Exception ex) { WriteResult(CommandResult.FromException(request.id, ex.InnerException ?? ex)); }
            return true;
        }

        private static CommandResult StatusFailure(string id, string error)
        {
            var result = CommandResult.FailureWithId(id, "Status file remained unavailable. Command was not executed. " + error);
            result.errorCode = "STATUS_WRITE_FAILED";
            return result;
        }

        /// <summary>完成回调只接收一次结果；状态更新失败不能把已成功业务改写成失败。</summary>
        private void WriteResult(CommandResult result)
        {
            if (_published.Contains(result.id) || _pendingResults.ContainsKey(result.id)) return;
            result.status = result.errorCode == "EXECUTION_WAIT_TIMEOUT" ? "unknown" : result.success ? "completed" : "failed";
            _queueTimes.TryGetValue(result.id, out var queuedMs);
            _queueTimes.Remove(result.id);
            result.timings = new { queuedMs, executionMs = result.executionTime };
            string json;
            try { json = JsonConvert.SerializeObject(result, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, ReferenceLoopHandling = ReferenceLoopHandling.Ignore }); }
            catch (Exception ex)
            {
                var failure = CommandResult.FailureWithId(result.id, "Execution finished but result serialization failed; do not replay. " + ex.Message);
                failure.errorCode = "RESULT_SERIALIZATION_FAILED";
                failure.status = "unknown";
                result = failure;
                json = JsonConvert.SerializeObject(result);
            }
            _pendingResults[result.id] = new PendingResult { Result = result, Json = json };
            TryPublishResult(result.id);
        }

        /// <summary>结果发布重试独立于执行；短时 IO 失败保留原结果至既有十分钟保留期限。</summary>
        private void TryPublishResult(string id)
        {
            var pending = _pendingResults[id];
            if (pending.Age.ElapsedMilliseconds < pending.NextAttemptMs) return;
            var path = Path.Combine(_resultsDir, id + ".json");
            try
            {
                // 已有文件只有内容相同才算本次发布成功，绝不覆盖其他终态。
                if (File.Exists(path))
                {
                    if (ReadPublishedText(path) != pending.Json) throw new IOException("A different result already exists for this ID.");
                }
                else
                {
                    var temporary = path + ".tmp";
                    File.WriteAllText(temporary, pending.Json, new UTF8Encoding(false));
                    File.Move(temporary, path);
                }
                _pendingResults.Remove(id);
                _published.Add(id);
                if (TryWriteStatus(id, pending.Result.status, out _) == StatusWrite.Waiting)
                    _pendingStatuses[id] = pending.Result.status;
            }
            catch (IOException ex) { DeferResult(id, pending, ex.Message); }
            catch (UnauthorizedAccessException ex) { DeferResult(id, pending, ex.Message); }
        }

        private void DeferResult(string id, PendingResult pending, string error)
        {
            if (!pending.Warned)
            {
                AIBridgeLogger.LogWarning($"Result publication delayed: {id}; execution will not be repeated. {error}");
                pending.Warned = true;
            }
            pending.NextAttemptMs = pending.Age.ElapsedMilliseconds + 1000;
            if (pending.Age.Elapsed < StaleFileTimeout) return;
            _pendingResults.Remove(id);
            _published.Add(id);
            if (TryWriteStatus(id, "unknown", out _) == StatusWrite.Waiting) _pendingStatuses[id] = "unknown";
            AIBridgeLogger.LogWarning("Result retention expired without publication: " + id);
        }

        /// <summary>Windows 文件不允许删除共享时只能等待占用解除，不删除旧文件或原地截断。</summary>
        private StatusWrite TryWriteStatus(string id, string status, out string error)
        {
            var key = id + "|" + status;
            var path = Path.Combine(_statusDir, id + ".json");
            error = null;
            try
            {
                var temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonConvert.SerializeObject(new { id, protocolVersion = 2, status, sessionId = EditorInstanceTracker.SessionId, updatedAtUtc = DateTime.UtcNow.ToString("O") }), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                _statusFailures.Remove(key);
                return StatusWrite.Written;
            }
            catch (IOException ex) { error = ex.Message; }
            catch (UnauthorizedAccessException ex) { error = ex.Message; }
            if (!_statusFailures.TryGetValue(key, out var timer))
            {
                _statusFailures[key] = timer = Stopwatch.StartNew();
                AIBridgeLogger.LogWarning($"Status publication delayed: {id}, {status}. {error}");
            }
            if (timer.ElapsedMilliseconds < StatusRetryMilliseconds) return StatusWrite.Waiting;
            _statusFailures.Remove(key);
            return StatusWrite.Failed;
        }

        private static string ReadPublishedText(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8)) return reader.ReadToEnd();
        }

        private static void TryDeleteInput(string file)
        {
            try { File.Delete(file); }
            catch (IOException ex) { AIBridgeLogger.LogDebug("Input cleanup delayed: " + ex.Message); }
            catch (UnauthorizedAccessException ex) { AIBridgeLogger.LogDebug("Input cleanup denied: " + ex.Message); }
        }

        /// <summary>清理异常不打断命令泵；读取状态时允许发布方替换文件。</summary>
        private void CleanupStaleFiles()
        {
            if (DateTime.UtcNow < _nextCleanup) return;
            _nextCleanup = DateTime.UtcNow.AddMinutes(1);
            foreach (var directory in new[] { _resultsDir, _statusDir, _commandsDir })
            {
                try
                {
                    foreach (var file in Directory.GetFiles(directory, directory == _commandsDir ? "*.error" : "*.json"))
                    {
                        if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) <= StaleFileTimeout) continue;
                        var id = Path.GetFileNameWithoutExtension(file);
                        if (_queueTimers.ContainsKey(id) || _queueTimes.ContainsKey(id) || _pendingResults.ContainsKey(id) || _pendingStatuses.ContainsKey(id)) continue;
                        try { File.Delete(file); _published.Remove(id); }
                        catch (IOException ex) { AIBridgeLogger.LogDebug("Stale file cleanup delayed: " + ex.Message); }
                        catch (UnauthorizedAccessException ex) { AIBridgeLogger.LogDebug("Stale file cleanup denied: " + ex.Message); }
                    }
                }
                catch (IOException ex) { AIBridgeLogger.LogDebug("Cleanup scan delayed: " + ex.Message); }
                catch (UnauthorizedAccessException ex) { AIBridgeLogger.LogDebug("Cleanup scan denied: " + ex.Message); }
            }
        }

        private void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(_commandsDir);
            Directory.CreateDirectory(_resultsDir);
            Directory.CreateDirectory(_codeDir);
            Directory.CreateDirectory(_screenshotsDir);
            Directory.CreateDirectory(_statusDir);
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
