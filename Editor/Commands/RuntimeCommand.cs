#if AIBRIDGE_RUNTIME_ENABLED
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using AIBridge.Internal.Json;

namespace AIBridge.Editor
{
    public static class RuntimeCommand
    {
        [AIBridge("列出 Runtime targets", "AIBridgeCLI RuntimeCommand_ListTargets --raw")]
        public static IEnumerator ListTargets()
        {
            var players = AIBridgeRuntimeBridgeEditorUtility.ListPlayers();
            yield return CommandResult.Success(new
            {
                runtimeDir = AIBridgeRuntimeBridgeEditorUtility.GetRuntimeDirectory(),
                count = players.Count,
                targets = players
            });
        }

        [AIBridge("扫描局域网 Runtime targets", "AIBridgeCLI RuntimeCommand_Discover --udpPort 27183 --raw")]
        public static IEnumerator Discover(
            [Description("UDP discovery 起始端口")] int udpPort = 27183,
            [Description("超时毫秒")] int timeout = 1500)
        {
            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;
            var result = AIBridgeRuntimeBridgeEditorUtility.DiscoverLanTargets(timeout, udpPort, settings.AuthToken);
            if (!result.Success)
            {
                yield return CommandResult.Failure(result.Error ?? "Runtime discovery failed.");
                yield break;
            }

            yield return CommandResult.Success(new
            {
                count = result.Count,
                reachableCount = result.ReachableCount,
                sentPackets = result.SentPackets,
                scannedInterfaces = result.ScannedInterfaces,
                targets = AIBridgeRuntimeBridgeEditorUtility.ListDiscoveredTargets()
            });
        }

        [AIBridge("获取 Runtime status", "AIBridgeCLI RuntimeCommand_Status --transport http --url http://127.0.0.1:27182 --target latest --raw")]
        public static IEnumerator Status(string transport = "file", string target = "latest", string url = null, int timeout = 5000)
        {
            return SendRuntimeAction("runtime.status", transport, target, url, timeout, null);
        }

        [AIBridge("读取 Runtime logs", "AIBridgeCLI RuntimeCommand_Logs --transport file --target latest --logType Error --count 100 --raw")]
        public static IEnumerator Logs(string transport = "file", string target = "latest", string url = null, string logType = "all", int count = 50, int timeout = 5000)
        {
            return SendRuntimeAction("runtime.logs", transport, target, url, timeout, new Dictionary<string, object>
            {
                ["logType"] = logType,
                ["count"] = count
            });
        }

        [AIBridge("截取 Runtime screenshot", "AIBridgeCLI RuntimeCommand_Screenshot --transport file --target latest --raw")]
        public static IEnumerator Screenshot(string transport = "file", string target = "latest", string url = null, int timeout = 10000)
        {
            return SendRuntimeAction("runtime.screenshot", transport, target, url, timeout, null);
        }

        [AIBridge("发送已编译 DLL 到 Runtime 执行", "AIBridgeCLI RuntimeCommand_ExecDll --dll probe.dll --transport http --url http://127.0.0.1:27182 --riskAccepted true --raw")]
        public static IEnumerator ExecDll(string dll, string transport = "http", string target = "latest", string url = null, bool riskAccepted = false, string entryType = null, string methodName = null, int timeout = 30000)
        {
            if (string.IsNullOrWhiteSpace(dll) || !File.Exists(dll))
            {
                yield return CommandResult.Failure("DLL does not exist: " + dll);
                yield break;
            }

            if (!riskAccepted)
            {
                yield return CommandResult.Failure("RuntimeCommand_ExecDll requires --riskAccepted true.");
                yield break;
            }

            var bytes = File.ReadAllBytes(dll);
            var parameters = new Dictionary<string, object>
            {
                ["assemblyBase64"] = Convert.ToBase64String(bytes),
                ["sha256"] = ComputeSha256(bytes),
                ["riskAccepted"] = true,
                ["entryType"] = entryType,
                ["methodName"] = methodName
            };
            var nested = SendRuntimeAction("runtime.code.execute", transport, target, url, timeout, parameters);
            while (nested.MoveNext())
            {
                yield return nested.Current;
            }
        }

        private static IEnumerator SendRuntimeAction(string action, string transport, string target, string url, int timeout, Dictionary<string, object> parameters)
        {
            transport = string.IsNullOrWhiteSpace(transport) ? (string.IsNullOrWhiteSpace(url) ? "file" : "http") : transport;
            if (string.Equals(transport, "http", StringComparison.OrdinalIgnoreCase))
            {
                var nested = SendHttpAction(action, url, timeout, parameters);
                while (nested.MoveNext())
                {
                    yield return nested.Current;
                }

                yield break;
            }

            if (string.Equals(transport, "file", StringComparison.OrdinalIgnoreCase))
            {
                var nested = SendFileAction(action, target, timeout, parameters);
                while (nested.MoveNext())
                {
                    yield return nested.Current;
                }

                yield break;
            }

            yield return CommandResult.Failure("Unsupported runtime transport: " + transport);
        }

        private static IEnumerator SendFileAction(string action, string target, int timeout, Dictionary<string, object> parameters)
        {
            var player = ResolvePlayer(target);
            if (player == null)
            {
                yield return CommandResult.Failure("Runtime target not found or stale.");
                yield break;
            }

            var commandId = "rte_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(player.CommandsPath);
            Directory.CreateDirectory(player.ResultsPath);
            File.WriteAllText(Path.Combine(player.CommandsPath, commandId + ".json"), BuildRuntimeCommandJson(commandId, action, parameters), new UTF8Encoding(false));

            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(100, timeout));
            var resultPath = Path.Combine(player.ResultsPath, commandId + ".json");
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(resultPath))
                {
                    yield return ConvertRuntimeResult(File.ReadAllText(resultPath, Encoding.UTF8));
                    try { File.Delete(resultPath); } catch { }
                    yield break;
                }

                yield return null;
            }

            yield return CommandResult.Failure("Timeout waiting for runtime result after " + timeout.ToString(CultureInfo.InvariantCulture) + "ms.");
        }

        private static IEnumerator SendHttpAction(string action, string url, int timeout, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                url = AIBridgeRuntimeBridgeEditorUtility.BuildLocalHttpUrl();
            }

            var commandId = "http_" + Guid.NewGuid().ToString("N");
            var commandPayload = BuildRuntimeCommandJson(commandId, action, parameters);
            var authToken = AIBridgeProjectSettings.Instance.RuntimeBridge.AuthToken;
            string responseBody = null;
            string httpError = null;

            var sendThread = new Thread(() =>
            {
                try
                {
                    responseBody = SendHttpPost(url.TrimEnd('/') + "/aibridge/commands?timeoutMs=" + Math.Max(100, timeout), commandPayload, authToken, timeout);
                }
                catch (Exception ex)
                {
                    httpError = ex.GetType().Name + ": " + ex.Message;
                }
            });
            sendThread.IsBackground = true;
            sendThread.Start();

            var startedAt = DateTime.UtcNow;
            while (sendThread.IsAlive && (DateTime.UtcNow - startedAt).TotalMilliseconds < timeout + 10000)
            {
                yield return null;
            }

            if (sendThread.IsAlive)
            {
                yield return CommandResult.Failure("HTTP request timed out.");
                yield break;
            }

            if (!string.IsNullOrEmpty(httpError))
            {
                yield return CommandResult.Failure("HTTP error: " + httpError);
                yield break;
            }

            yield return ConvertRuntimeResult(responseBody);
        }

        private static string SendHttpPost(string commandUrl, string commandPayload, string authToken, int timeout)
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
                    + (string.IsNullOrEmpty(authToken) ? "" : "Authorization: Bearer " + authToken + "\r\n")
                    + "Connection: close\r\n\r\n";
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
                    {
                        responseBytes.Add(buffer[i]);
                    }
                }

                var fullResponse = Encoding.UTF8.GetString(responseBytes.ToArray());
                var bodyStart = fullResponse.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                return bodyStart >= 0 ? fullResponse.Substring(bodyStart + 4) : fullResponse;
            }
        }

        private static AIBridgeRuntimePlayerInfo ResolvePlayer(string target)
        {
            var players = AIBridgeRuntimeBridgeEditorUtility.ListPlayers();
            if (string.IsNullOrWhiteSpace(target) || string.Equals(target, "latest", StringComparison.OrdinalIgnoreCase))
            {
                for (var i = 0; i < players.Count; i++)
                {
                    if (!players[i].Stale)
                    {
                        return players[i];
                    }
                }

                return null;
            }

            for (var i = 0; i < players.Count; i++)
            {
                if (!players[i].Stale && string.Equals(players[i].TargetId, target, StringComparison.OrdinalIgnoreCase))
                {
                    return players[i];
                }
            }

            return null;
        }

        private static object ConvertRuntimeResult(string json)
        {
            var data = AIBridgeJson.DeserializeObject(json);
            var success = GetBool(data, "Success") || GetBool(data, "success");
            var error = GetString(data, "Error") ?? GetString(data, "error");
            var resultData = GetValue(data, "Data") ?? GetValue(data, "data");
            return success ? CommandResult.Success(resultData) : CommandResult.Failure(error ?? "Runtime command failed.");
        }

        private static string BuildRuntimeCommandJson(string commandId, string action, Dictionary<string, object> parameters)
        {
            return AIBridgeJson.Serialize(new Dictionary<string, object>
            {
                ["id"] = commandId,
                ["action"] = action,
                ["token"] = AIBridgeProjectSettings.Instance.RuntimeBridge.AuthToken,
                ["params"] = parameters ?? new Dictionary<string, object>()
            }, pretty: false);
        }

        private static bool GetBool(Dictionary<string, object> data, string key)
        {
            var value = GetValue(data, key);
            if (value is bool boolValue)
            {
                return boolValue;
            }

            return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) && parsed;
        }

        private static string GetString(Dictionary<string, object> data, string key)
        {
            var value = GetValue(data, key);
            return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static object GetValue(Dictionary<string, object> data, string key)
        {
            if (data == null)
            {
                return null;
            }

            object value;
            if (data.TryGetValue(key, out value))
            {
                return value;
            }

            foreach (var item in data)
            {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Value;
                }
            }

            return null;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
#endif
