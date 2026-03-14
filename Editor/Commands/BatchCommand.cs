using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace AIBridge.Editor
{
    public static class BatchCommand
    {
        [AIBridge("批量执行多个命令，按顺序执行并返回每个命令的结果。用于需要执行多个相关操作的场景",
            @"Windows CMD 必须使用单引号包裹 JSON：
AIBridgeCLI Batch --commands '[{""type"":""GameObjectCommand_Find"",""params"":{""name"":""Player""}},{""type"":""TransformCommand_Get"",""params"":{""path"":""Player""}}]' --raw

注意：如果使用双引号会导致参数解析错误（Unexpected positional argument），必须使用单引号。
PowerShell 或 Bash 环境下可以使用双引号但需要转义内部引号。",
            "Batch")]
        public static IEnumerator Execute(
            [Description("命令对象数组，每个对象包含 'type' 和 'params' 字段")] object commands = null)
        {
            if (commands == null)
            {
                yield return CommandResult.Failure("Missing 'commands' parameter");
                yield break;
            }

            var cmdList = commands as List<object>;
            if (cmdList == null || cmdList.Count == 0)
            {
                yield return CommandResult.Failure("'commands' must be a non-empty array");
                yield break;
            }

            var results = new List<object>();
            var successCount = 0;
            var failureCount = 0;

            foreach (var cmdObj in cmdList)
            {
                var cmdDict = cmdObj as Dictionary<string, object>;
                if (cmdDict == null)
                {
                    results.Add(new { success = false, error = "Invalid command format" });
                    failureCount++;
                    continue;
                }

                var type = cmdDict.ContainsKey("type") ? cmdDict["type"]?.ToString() : null;
                if (string.IsNullOrEmpty(type))
                {
                    results.Add(new { success = false, error = "Missing 'type' in command" });
                    failureCount++;
                    continue;
                }

                var @params = cmdDict.ContainsKey("params")
                    ? cmdDict["params"] as Dictionary<string, object>
                    : new Dictionary<string, object>();

                if (!CommandRegistry.TryGetCommand(type, out var entry))
                {
                    results.Add(new { type, success = false, error = $"Unknown command: {type}" });
                    failureCount++;
                    continue;
                }

                var subRequest = new CommandRequest { id = $"batch_sub_{results.Count}", type = type, @params = @params };
                if (!CommandParamBinder.TryBind(entry, subRequest, out var args, out var bindError))
                {
                    results.Add(new { type, success = false, error = bindError });
                    failureCount++;
                    continue;
                }

                // Run sub-command coroutine and wait for it to complete
                bool done = false;
                CommandResult subResult = null;
                var subCoroutine = (System.Collections.IEnumerator)entry.Method.Invoke(null, args);
                EditorCoroutineRunner.Start(subCoroutine, r => { subResult = r; done = true; }, subRequest.id);
                while (!done) yield return null;

                results.Add(new { type, success = subResult.success, data = subResult.data, error = subResult.error });
                if (subResult.success) successCount++;
                else failureCount++;
            }

            yield return CommandResult.Success(new
            {
                totalCommands = cmdList.Count,
                successCount,
                failureCount,
                results
            });
        }
    }
}
