using System.Diagnostics;
using System.Text.Json;

namespace AIBridgeCLI.Tests;

/// <summary>覆盖文件交接边界，禁止等待超时导致重复执行或丢失原结果。</summary>
public sealed class CommandTransportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "aibridge-transport-" + Guid.NewGuid().ToString("N"));
    private readonly CommandSender sender;

    public CommandTransportTests()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "editor-instance.json"), JsonSerializer.Serialize(new
        {
            protocolVersion = 2, sessionId = "session-one", processId = Environment.ProcessId,
            lastUpdatedUtc = DateTime.UtcNow.ToString("O")
        }));
        sender = new CommandSender(40, 5, root);
    }

    [Fact]
    public void TimeoutPreservesRequestAndLateResultIsRepeatable()
    {
        var request = new CommandRequest { id = "late", type = "Read", @params = new() };
        var response = sender.SendCommand(request);
        Assert.Equal("WAIT_TIMEOUT", response.errorCode);
        Assert.Equal("submitted", response.status);
        Assert.True(File.Exists(Path.Combine(root, "commands", "late.json")));
        Assert.False(File.Exists(Path.Combine(root, "commands", "late.json.tmp")));
        WriteResult("late", "{\"value\":42}");
        Assert.True(sender.TryGetResult("late").success);
        Assert.True(sender.TryGetResult("late").success);
        Assert.Equal("completed", sender.GetStatus("late").status);
        Assert.Equal("DUPLICATE_ID", sender.Submit(request).errorCode);
    }

    [Fact]
    public void StructuredResultPreservesTypesAndErrors()
    {
        WriteResult("typed", "{\"returnValue\":{\"number\":42,\"flag\":true,\"text\":\"Exception: ordinary data\",\"empty\":null},\"logs\":[]}");
        var result = sender.TryGetResult("typed");
        var data = (JsonElement)result.data;
        Assert.Equal(42, data.GetProperty("returnValue").GetProperty("number").GetInt32());
        Assert.True(data.GetProperty("returnValue").GetProperty("flag").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("returnValue").GetProperty("empty").ValueKind);
        var raw = JsonSerializer.Serialize(result, JsonContext.Default.CommandResult);
        Assert.Contains("protocolVersion", raw);
    }

    [Fact]
    public void SessionChangeCannotClaimRunningWorkOrReplayIt()
    {
        Directory.CreateDirectory(Path.Combine(root, "status"));
        File.WriteAllText(Path.Combine(root, "status", "lost.json"), "{\"status\":\"running\",\"sessionId\":\"old-domain\"}");
        Assert.Equal("unknown", sender.GetStatus("lost").status);
        Assert.Equal("DUPLICATE_ID", sender.Submit(new() { id = "lost", type = "Write", @params = new() }).errorCode);
    }

    [Fact]
    public void CurrentSessionAndUnknownIdHaveDistinctStates()
    {
        Directory.CreateDirectory(Path.Combine(root, "status"));
        File.WriteAllText(Path.Combine(root, "status", "active.json"), "{\"status\":\"running\",\"sessionId\":\"session-one\"}");
        Assert.Equal("running", sender.GetStatus("active").status);
        Assert.Equal("unknown", sender.GetStatus("absent").status);
        Assert.Null(sender.TryGetResult("absent"));
    }

    [Fact]
    public void RetentionAndProtocolAreChecked()
    {
        WriteResult("expired", "null");
        File.SetLastWriteTimeUtc(Path.Combine(root, "results", "expired.json"), DateTime.UtcNow.AddMinutes(-11));
        Assert.Null(sender.TryGetResult("expired"));
        File.WriteAllText(Path.Combine(root, "results", "legacy.json"), "{\"id\":\"legacy\",\"success\":true}");
        Assert.Equal("PROTOCOL_MISMATCH", sender.TryGetResult("legacy").errorCode);
        File.WriteAllText(Path.Combine(root, "editor-instance.json"), JsonSerializer.Serialize(new { processId = Environment.ProcessId }));
        Assert.Equal("PROTOCOL_MISMATCH", sender.Submit(new() { id = "new", type = "Read", @params = new() }).errorCode);
    }

    [Fact]
    public void PublishedSnapshotCanBeReadDuringAtomicReplacement()
    {
        var path = Path.Combine(root, "snapshot.json");
        var temporary = path + ".tmp";
        File.WriteAllText(path, "old");
        using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        File.WriteAllText(temporary, "new");
        File.Replace(temporary, path, null);
        Assert.Equal("new", CommandSender.ReadPublishedFile(path));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("C:\\escape")]
    [InlineData("")]
    public void QueryRejectsInvalidIdentifiers(string id) => Assert.Throws<ArgumentException>(() => sender.GetStatus(id));

    private void WriteResult(string id, string data) => File.WriteAllText(Path.Combine(root, "results", id + ".json"),
        "{\"protocolVersion\":2,\"id\":\"" + id + "\",\"success\":true,\"data\":" + data + "}");

    public void Dispose() => Directory.Delete(root, true);
}
