using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBridgeCLI.Commands;

internal static class RuntimeCliCommand
{
    private const string RuntimeDirectoryName = "runtime";
    private const string TargetsDirectoryName = "targets";
    private const string HeartbeatFileName = "heartbeat.json";
    private const string CommandsDirectoryName = "commands";
    private const string ResultsDirectoryName = "results";
    private const string DiscoveryProtocol = "aibridge-runtime-discovery";
    private const int DefaultDiscoveryUdpPort = 27183;
    private const int DiscoveryPortScanCount = 50;
    private static readonly TimeSpan StaleHeartbeatTimeout = TimeSpan.FromSeconds(15);

    public static int Execute(string[] args)
    {
        var parsed = RuntimeParsedArgs.Parse(args);
        if (parsed.Help || string.IsNullOrEmpty(parsed.SubCommand))
        {
            Console.WriteLine(GetHelp(parsed.SubCommand));
            return 0;
        }

        if (string.Equals(parsed.SubCommand, "exec", StringComparison.OrdinalIgnoreCase)
            && parsed.Options.ContainsKey("code"))
        {
            OutputFormatter.PrintResult(Failure("runtime_code_requires_editor", "Runtime source execution requires Editor Roslyn. Open Unity Editor and use RuntimeExecuteCommand_Execute, or pass --dll to runtime exec."), parsed.OutputMode);
            return 1;
        }

        CommandResult result;
        try
        {
            result = ExecuteParsed(parsed);
        }
        catch (Exception ex)
        {
            result = Failure("runtime_cli_error", ex.GetType().Name + ": " + ex.Message);
        }

        OutputFormatter.PrintResult(result, parsed.OutputMode);
        return result.success ? 0 : 1;
    }

    private static CommandResult ExecuteParsed(RuntimeParsedArgs parsed)
    {
        switch (parsed.SubCommand.ToLowerInvariant())
        {
            case "list_targets":
                return Success("runtime_list_targets", BuildListTargetsData(parsed));
            case "discover":
                return Discover(parsed);
            case "status":
                return SendRuntimeAction(parsed, "runtime.status");
            case "logs":
                return SendRuntimeAction(parsed, "runtime.logs");
            case "screenshot":
                return SendRuntimeAction(parsed, "runtime.screenshot");
            case "ui_snapshot":
                return SendRuntimeAction(parsed, "runtime.ui.snapshot");
            case "ui_find":
                return SendRuntimeAction(parsed, "runtime.ui.find");
            case "ui_raycast":
                return SendRuntimeAction(parsed, "runtime.ui.raycast");
            case "ui_click":
                return SendRuntimeAction(parsed, "runtime.ui.click");
            case "exec":
                return SendRuntimeAction(parsed, "runtime.code.execute", BuildExecDllParams(parsed));
            default:
                return Failure("unknown_runtime_command", "Unknown runtime command: " + parsed.SubCommand);
        }
    }

    private static CommandResult SendRuntimeAction(RuntimeParsedArgs parsed, string action, Dictionary<string, object> extraParams = null)
    {
        var transport = parsed.GetString("transport", string.IsNullOrWhiteSpace(parsed.GetString("url", null)) ? "file" : "http");
        if (string.Equals(transport, "http", StringComparison.OrdinalIgnoreCase))
        {
            return SendHttpRuntimeAction(parsed, action, extraParams);
        }

        if (string.Equals(transport, "file", StringComparison.OrdinalIgnoreCase))
        {
            return SendFileRuntimeAction(parsed, action, extraParams);
        }

        return Failure("invalid_transport", "Unsupported runtime transport: " + transport);
    }

    private static CommandResult SendFileRuntimeAction(RuntimeParsedArgs parsed, string action, Dictionary<string, object> extraParams)
    {
        var players = ListPlayers(parsed.GetRuntimeDirectory());
        var target = ResolveTarget(players, parsed.GetString("target", "latest"));
        if (target == null)
        {
            return Failure("target_not_found", "Runtime target not found or stale.");
        }

        var commandId = PathHelper.GenerateCommandId();
        var commandPath = Path.Combine(target.CommandsPath, commandId + ".json");
        var resultPath = Path.Combine(target.ResultsPath, commandId + ".json");
        Directory.CreateDirectory(target.CommandsPath);
        Directory.CreateDirectory(target.ResultsPath);
        File.WriteAllText(commandPath, BuildRuntimeCommandJson(commandId, action, parsed, extraParams), new UTF8Encoding(false));

        var deadline = DateTime.UtcNow.AddMilliseconds(parsed.Timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(resultPath))
            {
                Thread.Sleep(10);
                var result = ReadRuntimeResultFile(resultPath, commandId);
                try { File.Delete(resultPath); } catch { }
                return result;
            }

            Thread.Sleep(50);
        }

        try { File.Delete(commandPath); } catch { }
        return Failure(commandId, "Timeout waiting for runtime result after " + parsed.Timeout.ToString(CultureInfo.InvariantCulture) + "ms.");
    }

    private static CommandResult SendHttpRuntimeAction(RuntimeParsedArgs parsed, string action, Dictionary<string, object> extraParams)
    {
        var url = parsed.GetString("url", null);
        if (string.IsNullOrWhiteSpace(url))
        {
            return Failure("missing_url", "HTTP runtime command requires --url.");
        }

        var commandId = PathHelper.GenerateCommandId();
        var commandJson = BuildRuntimeCommandJson(commandId, action, parsed, extraParams);
        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(Math.Max(100, parsed.Timeout + 5000)) };
        var token = parsed.GetString("token", null);
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var commandUrl = url.TrimEnd('/') + "/aibridge/commands?timeoutMs=" + Math.Max(100, parsed.Timeout).ToString(CultureInfo.InvariantCulture);
        using var content = new StringContent(commandJson, Encoding.UTF8, "application/json");
        var response = client.PostAsync(commandUrl, content).GetAwaiter().GetResult();
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        var result = ParseRuntimeResultJson(body, commandId);
        if (!response.IsSuccessStatusCode && result.success)
        {
            return Failure(commandId, "HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase);
        }

        return result;
    }

    private static CommandResult Discover(RuntimeParsedArgs parsed)
    {
        var targets = new List<Dictionary<string, object>>();
        var requestId = "disc_" + Guid.NewGuid().ToString("N");
        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["protocol"] = DiscoveryProtocol,
            ["version"] = 1,
            ["requestId"] = requestId
        }, JsonContext.Default.DictionaryStringObject);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var sockets = new List<UdpClient>();
        var startPort = Math.Max(1, parsed.GetInt("udpPort", DefaultDiscoveryUdpPort));
        var endPort = Math.Min(65535, startPort + DiscoveryPortScanCount - 1);
        var sent = 0;

        try
        {
            foreach (var address in GetLocalLanAddresses())
            {
                try
                {
                    var client = new UdpClient(new IPEndPoint(address, 0)) { EnableBroadcast = true };
                    sockets.Add(client);
                    for (var port = startPort; port <= endPort; port++)
                    {
                        client.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, port));
                        sent++;
                    }
                }
                catch
                {
                }
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(100, parsed.Timeout));
            while (DateTime.UtcNow < deadline)
            {
                var sawPacket = false;
                foreach (var socket in sockets)
                {
                    while (socket.Available > 0)
                    {
                        sawPacket = true;
                        var remote = new IPEndPoint(IPAddress.Any, 0);
                        var responseBytes = socket.Receive(ref remote);
                        var target = ParseDiscoveryResponse(responseBytes, remote, requestId);
                        if (target != null && !ContainsDiscoveredTarget(targets, target))
                        {
                            targets.Add(target);
                        }
                    }
                }

                if (!sawPacket)
                {
                    Thread.Sleep(10);
                }
            }
        }
        finally
        {
            foreach (var socket in sockets)
            {
                socket.Dispose();
            }
        }

        return Success("runtime_discover", new Dictionary<string, object>
        {
            ["count"] = targets.Count,
            ["sentPackets"] = sent,
            ["targets"] = targets.ToArray()
        });
    }

    private static Dictionary<string, object> BuildListTargetsData(RuntimeParsedArgs parsed)
    {
        var players = ListPlayers(parsed.GetRuntimeDirectory());
        return new Dictionary<string, object>
        {
            ["runtimeDir"] = parsed.GetRuntimeDirectory(),
            ["count"] = players.Count,
            ["targets"] = players.Select(player => player.ToDictionary()).ToArray()
        };
    }

    private static List<RuntimeTarget> ListPlayers(string runtimeDirectory)
    {
        var players = new List<RuntimeTarget>();
        var targetsRoot = Path.Combine(runtimeDirectory, TargetsDirectoryName);
        if (!Directory.Exists(targetsRoot))
        {
            return players;
        }

        foreach (var targetPath in Directory.GetDirectories(targetsRoot))
        {
            var heartbeatPath = Path.Combine(targetPath, HeartbeatFileName);
            using var heartbeat = TryReadJsonDocument(heartbeatPath);
            var root = heartbeat?.RootElement;
            var target = new RuntimeTarget
            {
                TargetId = ReadString(root, "targetId") ?? Path.GetFileName(targetPath),
                TargetPath = targetPath,
                CommandsPath = ReadString(root, "commandsPath") ?? Path.Combine(targetPath, CommandsDirectoryName),
                ResultsPath = ReadString(root, "resultsPath") ?? Path.Combine(targetPath, ResultsDirectoryName),
                HttpUrl = ReadString(root, "httpUrl"),
                ProductName = ReadString(root, "productName"),
                Platform = ReadString(root, "platform"),
                ActiveScene = ReadString(root, "activeScene"),
                LastHeartbeatUtc = ReadString(root, "lastHeartbeatUtc")
            };
            target.LastHeartbeat = ParseTime(target.LastHeartbeatUtc);
            target.AgeSeconds = target.LastHeartbeat.HasValue ? (DateTime.UtcNow - target.LastHeartbeat.Value).TotalSeconds : null;
            target.Stale = !target.LastHeartbeat.HasValue || DateTime.UtcNow - target.LastHeartbeat.Value > StaleHeartbeatTimeout;
            players.Add(target);
        }

        players.Sort((left, right) =>
        {
            if (left.Stale != right.Stale)
            {
                return left.Stale ? 1 : -1;
            }

            return Nullable.Compare(right.LastHeartbeat, left.LastHeartbeat);
        });
        return players;
    }

    private static RuntimeTarget ResolveTarget(List<RuntimeTarget> players, string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId) || string.Equals(targetId, "latest", StringComparison.OrdinalIgnoreCase))
        {
            return players.FirstOrDefault(player => !player.Stale);
        }

        return players.FirstOrDefault(player => !player.Stale && string.Equals(player.TargetId, targetId, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, object> BuildExecDllParams(RuntimeParsedArgs parsed)
    {
        var dll = parsed.GetString("dll", null);
        if (string.IsNullOrWhiteSpace(dll))
        {
            throw new ArgumentException("runtime exec requires --dll.");
        }

        if (!File.Exists(dll))
        {
            throw new FileNotFoundException("DLL does not exist: " + dll);
        }

        if (!parsed.GetBool("riskAccepted", false))
        {
            throw new ArgumentException("runtime exec requires --riskAccepted true.");
        }

        var bytes = File.ReadAllBytes(dll);
        return new Dictionary<string, object>
        {
            ["assemblyBase64"] = Convert.ToBase64String(bytes),
            ["sha256"] = ComputeSha256(bytes),
            ["riskAccepted"] = true,
            ["entryType"] = parsed.GetString("entryType", null),
            ["methodName"] = parsed.GetString("methodName", null)
        };
    }

    private static string BuildRuntimeCommandJson(string commandId, string action, RuntimeParsedArgs parsed, Dictionary<string, object> extraParams)
    {
        var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in parsed.Options)
        {
            if (RuntimeParsedArgs.RuntimeOptions.Contains(option.Key))
            {
                continue;
            }

            parameters[option.Key] = RequestBuilder.ParseValue(option.Value);
        }

        if (extraParams != null)
        {
            foreach (var item in extraParams)
            {
                if (item.Value != null)
                {
                    parameters[item.Key] = item.Value;
                }
            }
        }

        var command = new Dictionary<string, object>
        {
            ["id"] = commandId,
            ["action"] = action,
            ["token"] = parsed.GetString("token", null),
            ["params"] = parameters
        };
        return JsonSerializer.Serialize(command, JsonContext.Default.DictionaryStringObject);
    }

    private static CommandResult ReadRuntimeResultFile(string path, string fallbackId)
    {
        try
        {
            return ParseRuntimeResultJson(File.ReadAllText(path, Encoding.UTF8), fallbackId);
        }
        catch (Exception ex)
        {
            return Failure(fallbackId, "Failed to read runtime result: " + ex.Message);
        }
    }

    private static CommandResult ParseRuntimeResultJson(string json, string fallbackId)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var id = ReadString(root, "CommandId") ?? ReadString(root, "commandId") ?? ReadString(root, "id") ?? fallbackId;
            var success = ReadBool(root, "Success") ?? ReadBool(root, "success") ?? false;
            var error = ReadString(root, "Error") ?? ReadString(root, "error");
            object data = null;
            if (TryGetProperty(root, "Data", out var dataElement) || TryGetProperty(root, "data", out dataElement))
            {
                data = dataElement.Clone();
            }

            return new CommandResult
            {
                id = id,
                success = success,
                error = error,
                data = data
            };
        }
        catch (Exception ex)
        {
            return Failure(fallbackId, "Invalid runtime result JSON: " + ex.Message);
        }
    }

    private static Dictionary<string, object> ParseDiscoveryResponse(byte[] bytes, IPEndPoint remote, string requestId)
    {
        try
        {
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
            var root = document.RootElement;
            if (!string.Equals(ReadString(root, "protocol"), DiscoveryProtocol, StringComparison.Ordinal)
                || !string.Equals(ReadString(root, "requestId"), requestId, StringComparison.Ordinal))
            {
                return null;
            }

            var url = ReadString(root, "reachableUrl") ?? ReadString(root, "url");
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            return new Dictionary<string, object>
            {
                ["targetId"] = ReadString(root, "targetId") ?? "http",
                ["transport"] = "http",
                ["url"] = url.TrimEnd('/'),
                ["reachableUrl"] = url.TrimEnd('/'),
                ["bindUrl"] = ReadString(root, "bindUrl"),
                ["platform"] = ReadString(root, "platform"),
                ["projectName"] = ReadString(root, "projectName"),
                ["applicationVersion"] = ReadString(root, "applicationVersion"),
                ["deviceName"] = ReadString(root, "deviceName"),
                ["requiresToken"] = ReadBool(root, "requiresToken") ?? false,
                ["remoteEndPoint"] = remote == null ? null : remote.ToString(),
                ["lastSeenUtc"] = DateTime.UtcNow.ToString("o")
            };
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<IPAddress> GetLocalLanAddresses()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            var properties = networkInterface.GetIPProperties();
            foreach (var address in properties.UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(address.Address))
                {
                    yield return address.Address;
                }
            }
        }
    }

    private static bool ContainsDiscoveredTarget(List<Dictionary<string, object>> targets, Dictionary<string, object> target)
    {
        var url = target.TryGetValue("url", out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
        return targets.Any(item => string.Equals(
            item.TryGetValue("url", out var existing) ? Convert.ToString(existing, CultureInfo.InvariantCulture) : null,
            url,
            StringComparison.OrdinalIgnoreCase));
    }

    private static JsonDocument TryReadJsonDocument(string path)
    {
        try
        {
            return File.Exists(path) ? JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8)) : null;
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? ParseTime(string value)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string ReadString(JsonElement? element, string name)
    {
        if (!element.HasValue || !TryGetProperty(element.Value, name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ToString();
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
        {
            return property.GetBoolean();
        }

        if (bool.TryParse(property.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement property)
    {
        if (element.TryGetProperty(name, out property))
        {
            return true;
        }

        foreach (var item in element.EnumerateObject())
        {
            if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = item.Value;
                return true;
            }
        }

        return false;
    }

    private static CommandResult Success(string id, object data)
    {
        return new CommandResult
        {
            id = id,
            success = true,
            data = data
        };
    }

    private static CommandResult Failure(string id, string error)
    {
        return new CommandResult
        {
            id = id,
            success = false,
            error = error
        };
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static string GetHelp(string subCommand)
    {
        return "Usage:\n"
            + "  AIBridgeCLI runtime list_targets [--runtime-dir <dir>]\n"
            + "  AIBridgeCLI runtime discover [--udpPort 27183]\n"
            + "  AIBridgeCLI runtime status --transport file|http [--target latest] [--url <url>]\n"
            + "  AIBridgeCLI runtime logs --transport file|http [--target latest] [--url <url>] [--logType Error] [--count 100]\n"
            + "  AIBridgeCLI runtime screenshot --transport file|http [--target latest] [--url <url>]\n"
            + "  AIBridgeCLI runtime ui_snapshot --transport file|http [--target latest] [--url <url>] [--maxResults 100]\n"
            + "  AIBridgeCLI runtime ui_find --transport file|http [--target latest] [--url <url>] [--keyword Start]\n"
            + "  AIBridgeCLI runtime ui_raycast --transport file|http [--target latest] [--url <url>] [--path Canvas/Button]\n"
            + "  AIBridgeCLI runtime ui_click --transport file|http [--target latest] [--url <url>] [--path Canvas/Button]\n"
            + "  AIBridgeCLI runtime exec --dll <path> --transport http --url <url> --riskAccepted true";
    }

    private sealed class RuntimeTarget
    {
        public string TargetId;
        public string TargetPath;
        public string CommandsPath;
        public string ResultsPath;
        public string HttpUrl;
        public string ProductName;
        public string Platform;
        public string ActiveScene;
        public string LastHeartbeatUtc;
        public DateTime? LastHeartbeat;
        public double? AgeSeconds;
        public bool Stale;

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                ["targetId"] = TargetId,
                ["targetPath"] = TargetPath,
                ["commandsPath"] = CommandsPath,
                ["resultsPath"] = ResultsPath,
                ["httpUrl"] = HttpUrl,
                ["productName"] = ProductName,
                ["platform"] = Platform,
                ["activeScene"] = ActiveScene,
                ["lastHeartbeatUtc"] = LastHeartbeatUtc,
                ["ageSeconds"] = AgeSeconds,
                ["stale"] = Stale
            };
        }
    }

    private sealed class RuntimeParsedArgs
    {
        public static readonly HashSet<string> RuntimeOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "transport", "target", "runtime-dir", "url", "token", "timeout", "raw", "quiet", "help", "dll", "code", "riskAccepted", "entryType", "methodName"
        };

        public string SubCommand;
        public Dictionary<string, string> Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public bool Help;
        public bool Raw;
        public bool Quiet;
        public int Timeout = CliConstants.DEFAULT_TIMEOUT;
        public OutputMode OutputMode => Raw ? OutputMode.Raw : Quiet ? OutputMode.Quiet : OutputMode.Pretty;

        public static RuntimeParsedArgs Parse(string[] args)
        {
            var result = new RuntimeParsedArgs();
            var i = 0;
            while (i < args.Length)
            {
                var arg = args[i];
                if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    var key = arg.Substring(2);
                    var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                        ? args[++i]
                        : "true";
                    result.Options[key] = value;
                    if (string.Equals(key, "help", StringComparison.OrdinalIgnoreCase)) result.Help = true;
                    if (string.Equals(key, "raw", StringComparison.OrdinalIgnoreCase)) result.Raw = true;
                    if (string.Equals(key, "quiet", StringComparison.OrdinalIgnoreCase)) result.Quiet = true;
                    if (string.Equals(key, "timeout", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var timeout)) result.Timeout = timeout;
                    i++;
                    continue;
                }

                if (result.SubCommand == null)
                {
                    result.SubCommand = arg;
                    i++;
                    continue;
                }

                throw new ArgumentException("Unexpected positional argument: " + arg + ". Use --key value format for parameters.");
            }

            return result;
        }

        public string GetRuntimeDirectory()
        {
            return GetString("runtime-dir", Path.Combine(PathHelper.GetExchangeDirectory(), RuntimeDirectoryName));
        }

        public string GetString(string key, string defaultValue)
        {
            return Options.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public int GetInt(string key, int defaultValue)
        {
            return Options.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : defaultValue;
        }

        public bool GetBool(string key, bool defaultValue)
        {
            return Options.TryGetValue(key, out var value) ? string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1" : defaultValue;
        }
    }
}
