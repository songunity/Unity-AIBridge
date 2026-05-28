using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using AIBridge.Editor;
using UnityEngine;

public static class RuntimeExecuteCommand
{
    [AIBridge("在运行时Player中执行C#代码（需要HybridCLR）。Editor端用Roslyn编译为DLL，通过HTTP发送到真机AIBridgeRuntime执行。打包时需勾选Development Build，真机启动后自动监听HTTP。USB调试用adb forward tcp:27182 tcp:27182后url用默认值即可",
        example: @"
// USB 调试（先执行 adb forward tcp:27182 tcp:27182）
AIBridgeCLI RuntimeExecuteCommand_Execute --code 'using UnityEngine; Debug.Log(""Hello from Runtime""); return Time.frameCount;'

// 指定局域网真机 IP
AIBridgeCLI RuntimeExecuteCommand_Execute --code 'using UnityEngine; return Application.platform.ToString();' --url http://192.168.1.100:27182

// 从文件执行
AIBridgeCLI RuntimeExecuteCommand_Execute --file .aibridge/code/probe.csx --url http://192.168.1.100:27182 --timeout 15000

// 代码规则同 CodeExecuteCommand_Execute：只写 using 和逻辑，不需要写方法签名
// 支持 await，支持 return 返回值
")]
    public static IEnumerator Execute(
        [Description("要执行的C#代码，规则同CodeExecuteCommand")] string code = null,
        [Description("要执行的脚本文件路径，放在.aibridge/code/中")] string file = null,
        [Description("Runtime HTTP地址。USB调试用默认值，局域网填真机IP")] string url = "http://127.0.0.1:27182",
        [Description("超时毫秒数，复杂逻辑可适当加大")] int timeout = 10000)
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

        var commandPayload = global::AIBridge.Internal.Json.AIBridgeJson.Serialize(new Dictionary<string, object>
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
            elapsed += Time.unscaledDeltaTime;
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
