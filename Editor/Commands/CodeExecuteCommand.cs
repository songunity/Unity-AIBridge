using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using AIBridge.Editor;
using UnityEngine;

public static class CodeExecuteCommand
{
    private static CSharpCodeRunner runner;

    static CodeExecuteCommand()
    {
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () => runner = null;
    }

    /// <summary>
    /// 查询当前域内的编译缓存统计，不触发探针编译或执行。
    /// </summary>
    [AIBridge("查询 Editor C# 编译缓存统计")]
    public static IEnumerator CacheStatus()
    {
        yield return CommandResult.Success(new
        {
            capacity = CSharpCodeRunner.CacheCapacity,
            entries = runner?.CachedEntryCount ?? 0,
            hits = runner?.CacheHits ?? 0,
            compilations = runner?.CompilationCount ?? 0,
            referenceBuilds = runner?.ReferenceBuildCount ?? 0
        });
    }

    [AIBridge("在 Unity Editor 执行 C# 方法体或文件，返回结构化 returnValue 和 logs。长代码使用 --file 完整路径。",
        example: "AIBridgeCLI CodeExecuteCommand_Execute --code 'return 42;' --raw")]
    public static IEnumerator Execute(
        [Description("using 语句与方法体逻辑")] string code = null,
        [Description("代码文件完整路径")] string file = null,
        [Description("异步结果等待上限（毫秒），超时不取消底层任务")] int executionTimeoutMs = 120000)
    {
        if (executionTimeoutMs <= 0)
        {
            yield return CommandResult.Failure("executionTimeoutMs must be positive.");
            yield break;
        }
        if (!string.IsNullOrEmpty(file))
        {
            if (!File.Exists(file))
            {
                yield return CommandResult.Failure("File does not exist.");
                yield break;
            }
            code = File.ReadAllText(file);
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            yield return CommandResult.Failure("Code is empty.");
            yield break;
        }

        var logs = new List<object>();
        var logHandler = new Application.LogCallback((message, stackTrace, type) =>
            logs.Add(new { level = type.ToString(), message, stackTrace }));
        var codeRunner = runner ??= new CSharpCodeRunner();
        EvaluationResult result = null;
        var invocationTimer = System.Diagnostics.Stopwatch.StartNew();
        var waitTimer = new System.Diagnostics.Stopwatch();
        long preparationMs = 0;
        bool cacheHit = false;
        bool timedOut = false;
        Application.logMessageReceived += logHandler;
        try
        {
            try
            {
                result = codeRunner.CompileAndExecute(code);
                preparationMs = codeRunner.LastPreparationMs;
                cacheHit = codeRunner.LastCacheHit;
            }
            catch (Exception ex)
            {
                result = new EvaluationResult { Success = false, ErrorMessage = ex.ToString() };
            }
            waitTimer.Start();
            while (result != null && result.IsPending)
            {
                if (waitTimer.ElapsedMilliseconds >= executionTimeoutMs)
                {
                    timedOut = true;
                    break;
                }
                try { result = codeRunner.ContinuePendingTask(result); }
                catch (Exception ex) { result = new EvaluationResult { Success = false, ErrorMessage = ex.ToString() }; }
                if (result != null && result.IsPending) yield return null;
            }
        }
        finally
        {
            waitTimer.Stop();
            invocationTimer.Stop();
            Application.logMessageReceived -= logHandler;
        }

        var response = new CommandResult();
        Newtonsoft.Json.Linq.JToken value = Newtonsoft.Json.Linq.JValue.CreateNull();
        if (timedOut)
        {
            response.errorCode = "EXECUTION_WAIT_TIMEOUT";
            response.error = "Async result wait expired. The underlying task was not cancelled; its eventual business outcome is unknown. Do not replay the operation.";
        }
        else if (result == null || !result.Success)
        {
            response.errorCode = "EXECUTION_FAILED";
            response.error = result?.ErrorMessage ?? "Execution returned no result.";
        }
        else
        {
            try
            {
                // 先转换为 JSON token，序列化失败在此命令内报告，避免结果文件无法发布。
                var serializer = Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings
                {
                    ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Error,
                    Converters = new List<Newtonsoft.Json.JsonConverter> { new UnsupportedReturnConverter() }
                });
                if (result.ReturnValue != null) value = Newtonsoft.Json.Linq.JToken.FromObject(result.ReturnValue, serializer);
                response.success = true;
            }
            catch (Exception ex)
            {
                response.errorCode = "UNSUPPORTED_RETURN_VALUE";
                response.error = "Return only selected data fields, not Unity objects or cyclic/unsupported objects. " + ex.Message;
            }
        }
        response.data = new
        {
            returnValue = value,
            logs,
            logScope = "executionWindow",
            timings = new { preparationMs, invocationMs = invocationTimer.ElapsedMilliseconds - preparationMs - waitTimer.ElapsedMilliseconds, asyncWaitMs = waitTimer.ElapsedMilliseconds, cacheHit }
        };
        yield return response;
    }

    /// <summary>禁止 JSON 序列化遍历 Unity 对象与可执行/反射对象，包括嵌套字段。</summary>
    private sealed class UnsupportedReturnConverter : Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type type) => typeof(UnityEngine.Object).IsAssignableFrom(type)
            || typeof(Delegate).IsAssignableFrom(type) || typeof(Type).IsAssignableFrom(type);
        public override bool CanRead => false;
        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
            => throw new Newtonsoft.Json.JsonSerializationException("Unsupported return type: " + value.GetType().FullName);
        public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
            => throw new NotSupportedException();
    }
}
