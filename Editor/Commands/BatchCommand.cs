using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace AIBridge.Editor
{
    public static class BatchCommand
    {
        [AIBridge("顺序执行命令，默认遇错停止；任一失败则批次失败，每项返回执行状态。", "AIBridgeCLI Batch --stdin --raw", "Batch")]
        public static IEnumerator Execute(
            [Description("命令数组，每项含 type、params")] object commands = null,
            [Description("首个错误后跳过余项，独立查询可设为 false")] bool stopOnError = true)
        {
            if (!(commands is List<object> items) || items.Count == 0)
            {
                yield return CommandResult.Failure("commands must be a non-empty array.");
                yield break;
            }
            var results = new List<object>();
            int successCount = 0, failureCount = 0, skippedCount = 0;
            for (int index = 0; index < items.Count; index++)
            {
                var item = items[index] as Dictionary<string, object>;
                var type = item != null && item.TryGetValue("type", out var name) ? name?.ToString() : null;
                if (stopOnError && failureCount > 0)
                {
                    results.Add(new { index, type, status = "skipped", success = false });
                    skippedCount++;
                    continue;
                }
                CommandResult subResult = null;
                IEnumerator coroutine = null;
                var timer = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    if (string.IsNullOrEmpty(type) || !CommandRegistry.TryGetCommand(type, out var entry))
                        throw new ArgumentException("Missing or unknown command type: " + type);
                    var parameters = item.TryGetValue("params", out var raw) ? raw as Dictionary<string, object> : new Dictionary<string, object>();
                    if (parameters == null) throw new ArgumentException("params must be an object.");
                    var request = new CommandRequest { id = Guid.NewGuid().ToString("N"), type = type, @params = parameters };
                    if (!CommandParamBinder.TryBind(entry, request, out var args, out var error)) throw new ArgumentException(error);
                    coroutine = (IEnumerator)entry.Method.Invoke(null, args);
                    EditorCoroutineRunner.Start(coroutine, result => subResult = result, request.id);
                }
                catch (Exception ex)
                {
                    subResult = CommandResult.FromException(null, ex.InnerException ?? ex);
                }
                while (subResult == null) yield return null;
                var status = subResult.success ? "completed" : subResult.errorCode == "EXECUTION_WAIT_TIMEOUT" ? "unknown" : "failed";
                results.Add(new { index, type, status, subResult.success, subResult.data, subResult.error, subResult.errorCode, executionTime = timer.ElapsedMilliseconds });
                if (subResult.success) successCount++; else failureCount++;
            }
            yield return new CommandResult
            {
                success = failureCount == 0,
                errorCode = failureCount == 0 ? null : "BATCH_FAILED",
                error = failureCount == 0 ? null : "One or more batch commands failed; inspect individual results.",
                data = new { totalCommands = items.Count, successCount, failureCount, skippedCount, results }
            };
        }
    }
}
