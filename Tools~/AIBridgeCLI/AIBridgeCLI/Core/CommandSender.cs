using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AIBridgeCLI;

/// <summary>通过原子文件交接提交命令；等待超时不取消或重放 Unity 操作。</summary>
public class CommandSender
{
    private readonly string _exchangeDir;
    private readonly string _commandsDir;
    private readonly string _resultsDir;
    private readonly int _timeout;
    private readonly int _pollInterval;

    public CommandSender(int timeout = 5000, int pollInterval = 50, string exchangeDirectory = null)
    {
        if (timeout <= 0 || pollInterval <= 0) throw new ArgumentOutOfRangeException(nameof(timeout));
        _exchangeDir = exchangeDirectory ?? PathHelper.GetExchangeDirectory();
        _commandsDir = Path.Combine(_exchangeDir, "commands");
        _resultsDir = Path.Combine(_exchangeDir, "results");
        _timeout = timeout;
        _pollInterval = pollInterval;
        Directory.CreateDirectory(_commandsDir);
        Directory.CreateDirectory(_resultsDir);
    }

    /// <summary>提交一次并等待；结果保留供同一 ID 重复查询。</summary>
    public CommandResult SendCommand(CommandRequest request)
    {
        var timer = Stopwatch.StartNew();
        var sent = Submit(request);
        if (!sent.success) return sent;
        var result = WaitForResult(request.id, _timeout);
        var timings = result.timings is JsonElement element && element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value.Clone())
            : new Dictionary<string, object>();
        timings["cliWaitMs"] = timer.ElapsedMilliseconds;
        result.timings = timings;
        return result;
    }

    /// <summary>不等待执行，但同样检查目标与协议；提交成功不代表执行完成。</summary>
    public CommandResult Submit(CommandRequest request)
    {
        request.id ??= PathHelper.GenerateCommandId();
        ValidateId(request.id);
        var (alive, error) = EditorInstanceChecker.Check(_exchangeDir);
        if (!alive) return Failure(request.id, "EDITOR_UNAVAILABLE", error);
        var metadata = JsonSerializer.Deserialize(ReadPublishedFile(Path.Combine(_exchangeDir, "editor-instance.json")), JsonContext.Default.EditorInstanceMetadata);
        if (metadata?.protocolVersion != 2)
            return Failure(request.id, "PROTOCOL_MISMATCH", "Update the Editor package and CLI together (protocol 2 required).");
        var path = Path.Combine(_commandsDir, request.id + ".json");
        // 已知 ID 只能查询，不允许再次提交业务操作。
        if (File.Exists(path) || File.Exists(Path.Combine(_resultsDir, request.id + ".json")) || File.Exists(StatusPath(request.id)))
            return Failure(request.id, "DUPLICATE_ID", "Command ID already exists; query its result instead.");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(request, JsonContext.Default.CommandRequest), new UTF8Encoding(false));
        File.Move(temporary, path);
        return new CommandResult { protocolVersion = 2, id = request.id, success = true, status = "submitted" };
    }

    /// <summary>先读终态结果，再读会话状态；失去现场时不猜测执行结果。</summary>
    public CommandResult GetStatus(string id)
    {
        ValidateId(id);
        var result = TryGetResult(id);
        if (result?.errorCode == "INVALID_RESULT" || result?.errorCode == "PROTOCOL_MISMATCH")
        {
            result.status = "unknown";
            return result;
        }
        if (result != null) return new CommandResult { protocolVersion = 2, id = id, success = true, status = result.status ?? (result.success ? "completed" : "failed") };
        var path = StatusPath(id);
        if (File.Exists(path))
        {
            using var doc = JsonDocument.Parse(ReadPublishedFile(path));
            var state = doc.RootElement.GetProperty("status").GetString();
            if (state == "completed" || state == "failed" || state == "unknown")
                return new CommandResult { protocolVersion = 2, id = id, success = true, status = "unknown" };
            var metadataPath = Path.Combine(_exchangeDir, "editor-instance.json");
            if (File.Exists(metadataPath))
            {
                var metadata = JsonSerializer.Deserialize(ReadPublishedFile(metadataPath), JsonContext.Default.EditorInstanceMetadata);
                if (metadata?.sessionId == doc.RootElement.GetProperty("sessionId").GetString() && EditorInstanceChecker.Check(_exchangeDir).alive)
                    return new CommandResult { protocolVersion = 2, id = id, success = true, status = state };
            }
            return new CommandResult { protocolVersion = 2, id = id, success = true, status = "unknown" };
        }
        return new CommandResult { protocolVersion = 2, id = id, success = true, status = File.Exists(Path.Combine(_commandsDir, id + ".json")) ? "submitted" : "unknown" };
    }

    /// <summary>只等待原命令；超时后结果仍可取回。</summary>
    public CommandResult WaitForResult(string id, int timeout)
    {
        ValidateId(id);
        var timer = Stopwatch.StartNew();
        do
        {
            var result = TryGetResult(id);
            if (result != null) return result;
            Thread.Sleep(Math.Min(_pollInterval, Math.Max(1, timeout - (int)timer.ElapsedMilliseconds)));
        } while (timer.ElapsedMilliseconds < timeout);
        var pending = Failure(id, "WAIT_TIMEOUT", $"Timeout waiting for result after {timeout}ms. Execution was not cancelled; query this command ID before any retry.");
        pending.status = GetStatus(id).status;
        return pending;
    }

    public CommandResult TryGetResult(string commandId)
    {
        ValidateId(commandId);
        var path = Path.Combine(_resultsDir, commandId + ".json");
        if (!File.Exists(path)) return null;
        if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > TimeSpan.FromMinutes(10)) return null;
        var json = ReadPublishedFile(path);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("protocolVersion", out var version) || version.GetInt32() != 2)
            return Failure(commandId, "PROTOCOL_MISMATCH", "Result requires protocol 2.");
        var result = JsonSerializer.Deserialize(json, JsonContext.Default.CommandResult);
        if (result == null || result.protocolVersion != 2 || result.id != commandId)
            return Failure(commandId, "INVALID_RESULT", "Result protocol or command ID mismatch.");
        return result;
    }

    /// <summary>允许发布方原子替换文件，避免 Windows 读取锁阻塞心跳或状态更新。</summary>
    internal static string ReadPublishedFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private string StatusPath(string id) => Path.Combine(_exchangeDir, "status", id + ".json");

    internal static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-'))
            throw new ArgumentException("Command ID must contain only ASCII letters, digits, '_' or '-'.");
    }

    internal static CommandResult Failure(string id, string code, string error) => new() { protocolVersion = 2, id = id, success = false, errorCode = code, error = error };
}
