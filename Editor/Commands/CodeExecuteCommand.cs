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

    [AIBridge("在 Unity Editor（包括 Play Mode）执行 C# 代码片段或脚本文件。如果脚本内容过多更建议写入文件来运行，脚本文件放到.aibridge/code中",
        example:@"
Windows CMD 必须使用单引号包裹代码：
AIBridgeCLI CodeExecuteCommand_Execute --code 'using UnityEngine; Debug.Log(""Hello"");' --raw

PowerShell 或 Bash 可以使用双引号（需要转义）：
AIBridgeCLI CodeExecuteCommand_Execute --code ""using UnityEngine; Debug.Log(\""Hello\"");"" --raw

// 上边代码是你需要提供的逻辑，不需要写方法，只需要写using和逻辑
// 以上的代码会被编译成下边的
using UnityEngine;

public static class CodeExecutor
{{
    public static object Execute()
    {{
        Debug.Log(""Hello"");
        return null;
    }}
}}
")]
    public static IEnumerator Execute([Description("要执行的代码")]string code = null, [Description("要执行的文件，需要完整路径")]string file = null)
    {
        if (!string.IsNullOrEmpty(file))
        {
            if (File.Exists(file))
            {
                code = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(code))
                {
                    yield return CommandResult.Failure("File is empty.");
                    yield break;
                }
            }
            else
            {
                yield return CommandResult.Failure("File is not exist.");
                yield break;
            }
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            yield return CommandResult.Failure("Code is null or empty.");
            yield break;
        }

        // Capture logs during execution
        var logMessages = new List<string>();
        var logHandler = new Application.LogCallback((logString, stackTrace, type) =>
        {
            var prefix = type switch
            {
                LogType.Error or LogType.Exception => "[ERROR] ",
                LogType.Warning => "[WARNING] ",
                _ => "[INFO] "
            };
            logMessages.Add(prefix + logString);
        });

        Application.logMessageReceived += logHandler;

        EvaluationResult result;
        const float maxExecutionTime = 120f;
        try
        {
            // 编译和引用构建也放在 finally 的保护范围内，异常时必须解除日志监听。
            var codeRunner = runner ??= new CSharpCodeRunner();
            result = codeRunner.CompileAndExecute(code);
            // 编辑模式的帧间隔不代表协程实际等待时长，使用真实经过时间。
            var waitTimer = System.Diagnostics.Stopwatch.StartNew();
            while (result != null && result.IsPending)
            {
                if (waitTimer.Elapsed.TotalSeconds > maxExecutionTime)
                {
                    result = null;
                    break;
                }
                result = codeRunner.ContinuePendingTask(result);
                if (result.IsPending)
                    yield return null;
            }
        }
        finally
        {
            Application.logMessageReceived -= logHandler;
        }
        var output = string.Join("\n", logMessages);

        if (result == null)
        {
            yield return CommandResult.Failure($"Execution timed out after {maxExecutionTime}s\nOutput:\n{output}");
        }
        else if (!result.Success)
        {
            yield return CommandResult.Failure($"Execution Failed:\n{result.ErrorMessage}\nOutput:\n{output}");
        }
        else
        {
            var returnValue = result.ReturnValue == null ? "null" : result.ReturnValue.ToString();
            yield return CommandResult.Success($"ReturnValue:\n{returnValue}\nOutput:\n{output}");
        }
    }
}
