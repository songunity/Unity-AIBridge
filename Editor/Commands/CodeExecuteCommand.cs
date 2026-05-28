using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using AIBridge.Editor;
using UnityEngine;

public static class CodeExecuteCommand
{
    [AIBridge("执行C#代码片段或脚本文件，支持编辑器或运行时。如果脚本内容过多更建议写入文件来运行，脚本文件放到.aibridge/code中",
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
        CSharpCodeRunner codeRunner = new CSharpCodeRunner();
        if (!string.IsNullOrEmpty(file))
        {
            if (File.Exists(file))
            {
                code = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(code))
                {
                    yield return CommandResult.Failure("File is empty.");
                }
            }
            else
            {
                yield return CommandResult.Failure("File is not exist.");
            }
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            yield return CommandResult.Failure("Code is null or empty.");
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

        var result = codeRunner.CompileAndExecute(code);
        var elapsed = 0f;
        const float maxExecutionTime = 120f;
        try
        {
            while (result != null && result.IsPending)
            {
                elapsed += UnityEngine.Time.unscaledDeltaTime;
                if (elapsed > maxExecutionTime)
                {
                    result = null;
                    break;
                }
                result = codeRunner.ContinuePendingTask(result);
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

    [AIBridge("在运行时Player中执行C#代码（需要HybridCLR）。Editor端编译为DLL后通过HTTP发送到AIBridgeRuntime执行",
        example:@"
AIBridgeCLI CodeExecuteCommand_RuntimeExecute --code 'using UnityEngine; Debug.Log(""Hello from Runtime""); return Time.frameCount;' --url http://127.0.0.1:27182 --timeout 10000
AIBridgeCLI CodeExecuteCommand_RuntimeExecute --file .aibridge/code/probe.csx --url http://127.0.0.1:27182
")]
    public static IEnumerator RuntimeExecute(
        [Description("要执行的代码")] string code = null,
        [Description("要执行的文件路径")] string file = null,
        [Description("Runtime HTTP地址，如 http://127.0.0.1:27182")] string url = "http://127.0.0.1:27182",
        [Description("超时毫秒数")] int timeout = 10000)
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
                yield return CommandResult.Failure("File does not exist: " + file);
                yield break;
            }
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            yield return CommandResult.Failure("Code is null or empty.");
            yield break;
        }

        var runner = new CSharpCodeRunner();
        var compileResult = runner.CompileToBytes(code);
        if (!compileResult.Success)
        {
            yield return CommandResult.Failure("Compilation failed:\n" + compileResult.ErrorMessage);
            yield break;
        }

        var assemblyBytes = compileResult.AssemblyBytes;
        var assemblyBase64 = Convert.ToBase64String(assemblyBytes);
        string sha256;
        using (var hasher = SHA256.Create())
        {
            var hash = hasher.ComputeHash(assemblyBytes);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            sha256 = sb.ToString();
        }

        var commandPayload = AIBridge.Internal.Json.AIBridgeJson.Serialize(new Dictionary<string, object>
        {
            ["Id"] = "rte_" + Guid.NewGuid().ToString("N"),
            ["Action"] = "runtime.code.execute",
            ["Params"] = new Dictionary<string, object>
            {
                ["assemblyBase64"] = assemblyBase64,
                ["sha256"] = sha256,
                ["riskAccepted"] = true
            }
        }, pretty: false);

        var commandUrl = url.TrimEnd('/') + "/aibridge/commands?timeoutMs=" + timeout;
        string responseBody = null;
        string httpError = null;

        var sendThread = new System.Threading.Thread(() =>
        {
            try
            {
                var bodyBytes = Encoding.UTF8.GetBytes(commandPayload);
                var uri = new Uri(commandUrl);
                using (var client = new TcpClient())
                {
                    client.Connect(uri.Host, uri.Port);
                    client.SendTimeout = timeout;
                    client.ReceiveTimeout = timeout + 5000;
                    var stream = client.GetStream();

                    var requestHeader = "POST " + uri.PathAndQuery + " HTTP/1.1\r\n"
                        + "Host: " + uri.Host + ":" + uri.Port + "\r\n"
                        + "Content-Type: application/json\r\n"
                        + "Content-Length: " + bodyBytes.Length + "\r\n"
                        + "Connection: close\r\n"
                        + "\r\n";
                    var headerBytes = Encoding.ASCII.GetBytes(requestHeader);
                    stream.Write(headerBytes, 0, headerBytes.Length);
                    stream.Write(bodyBytes, 0, bodyBytes.Length);
                    stream.Flush();

                    var responseBytes = new List<byte>(4096);
                    var buffer = new byte[4096];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        for (var i = 0; i < read; i++)
                            responseBytes.Add(buffer[i]);
                    }

                    var fullResponse = Encoding.UTF8.GetString(responseBytes.ToArray());
                    var bodyStart = fullResponse.IndexOf("\r\n\r\n");
                    responseBody = bodyStart >= 0 ? fullResponse.Substring(bodyStart + 4) : fullResponse;
                }
            }
            catch (Exception ex)
            {
                httpError = ex.GetType().Name + ": " + ex.Message;
            }
        });

        sendThread.IsBackground = true;
        sendThread.Start();

        var elapsed = 0f;
        var maxWait = (timeout + 10000) / 1000f;
        while (sendThread.IsAlive && elapsed < maxWait)
        {
            elapsed += UnityEngine.Time.unscaledDeltaTime;
            yield return null;
        }

        if (sendThread.IsAlive)
        {
            yield return CommandResult.Failure("HTTP request timed out after " + maxWait + "s");
            yield break;
        }

        if (!string.IsNullOrEmpty(httpError))
        {
            yield return CommandResult.Failure("HTTP error: " + httpError);
            yield break;
        }

        yield return CommandResult.Success(responseBody ?? "No response");
    }
}