using Newtonsoft.Json.Linq;

namespace AIBridge.Editor.Tests;

/// <summary>真实文件锁及生产 CommandWatcher 回归，断言业务入口不丢失且只执行一次。</summary>
public sealed class EditorCommandWatcherTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "aibridge-dispatch-" + Guid.NewGuid().ToString("N"));
    private readonly CommandWatcher watcher;
    public EditorCommandWatcherTests()
    {
        CommandRegistry.Executions = 0;
        CommandRegistry.DuringExecution = null;
        CommandRegistry.DuplicateCallback = false;
        watcher = new CommandWatcher(root);
    }
    private string FileAt(string folder, string id) => Path.Combine(root, folder, id + ".json");
    private void Submit(string id) => File.WriteAllText(FileAt("commands", id), "{\"protocolVersion\":2,\"id\":\"" + id + "\",\"type\":\"Test_Execute\",\"params\":{}}");
    private JObject Result(string id) => JObject.Parse(File.ReadAllText(FileAt("results", id)));

    [Fact]
    public void RunningStatusLockKeepsCommandUntilRelease()
    {
        if (!OperatingSystem.IsWindows()) return;
        Submit("locked"); watcher.ScanForCommands();
        using (var held = new FileStream(FileAt("status", "locked"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(watcher.ProcessOneCommand());
            Assert.Equal(0, CommandRegistry.Executions);
            Assert.False(File.Exists(FileAt("results", "locked")));
        }
        Assert.True(watcher.ProcessOneCommand());
        Assert.Equal(1, CommandRegistry.Executions);
        Assert.True((bool)Result("locked")["success"]);
        Assert.False(watcher.ProcessOneCommand());
    }

    [Fact]
    public async Task PersistentStatusFailureReturnsNotExecutedAndQueueProgresses()
    {
        Submit("blocked"); watcher.ScanForCommands();
        var obstruction = FileAt("status", "blocked") + ".tmp";
        Directory.CreateDirectory(obstruction);
        Assert.False(watcher.ProcessOneCommand());
        await Task.Delay(2100);
        Assert.True(watcher.ProcessOneCommand());
        Assert.Equal("STATUS_WRITE_FAILED", (string)Result("blocked")["errorCode"]);
        Assert.Equal(0, CommandRegistry.Executions);
        Submit("next"); watcher.ScanForCommands(); watcher.ProcessOneCommand();
        Assert.True((bool)Result("next")["success"]);
        Assert.Equal(1, CommandRegistry.Executions);
    }

    [Fact]
    public void QueuedStateFailurePreservesOriginalInput()
    {
        Submit("queued");
        var obstruction = FileAt("status", "queued") + ".tmp";
        Directory.CreateDirectory(obstruction);
        watcher.ScanForCommands();
        Assert.True(File.Exists(FileAt("commands", "queued")));
        Assert.False(watcher.ProcessOneCommand());
        Directory.Delete(obstruction);
        watcher.ScanForCommands(); watcher.ProcessOneCommand();
        Assert.Equal(1, CommandRegistry.Executions);
    }

    [Fact]
    public void CompletionStateFailureCannotOverwritePublishedSuccess()
    {
        if (!OperatingSystem.IsWindows()) return;
        FileStream held = null;
        try
        {
            CommandRegistry.DuringExecution = id => held = new FileStream(FileAt("status", id), FileMode.Open, FileAccess.Read, FileShare.Read);
            CommandRegistry.DuplicateCallback = true;
            Submit("done"); watcher.ScanForCommands();
            Assert.True(watcher.ProcessOneCommand());
            var original = File.ReadAllText(FileAt("results", "done"));
            Assert.True((bool)Result("done")["success"]);
            held.Dispose(); held = null;
            watcher.ScanForCommands();
            Assert.Equal(original, File.ReadAllText(FileAt("results", "done")));
            Assert.Equal("completed", (string)JObject.Parse(File.ReadAllText(FileAt("status", "done")))["status"]);
            Assert.Equal(1, CommandRegistry.Executions);
        }
        finally { held?.Dispose(); }
    }

    [Fact]
    public async Task ResultPublicationRetriesWithoutExecutingAgain()
    {
        Submit("result");
        var obstruction = FileAt("results", "result") + ".tmp";
        Directory.CreateDirectory(obstruction);
        watcher.ScanForCommands(); watcher.ProcessOneCommand();
        Assert.Equal(1, CommandRegistry.Executions);
        Assert.False(File.Exists(FileAt("results", "result")));
        Directory.Delete(obstruction);
        await Task.Delay(1100);
        watcher.ScanForCommands(); watcher.ProcessOneCommand();
        Assert.True((bool)Result("result")["success"]);
        Assert.Equal(1, CommandRegistry.Executions);
    }

    [Fact]
    public void InputDeleteLockDoesNotCauseDuplicateExecution()
    {
        if (!OperatingSystem.IsWindows()) return;
        Submit("input");
        using (var held = new FileStream(FileAt("commands", "input"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            watcher.ScanForCommands(); watcher.ProcessOneCommand();
            watcher.ScanForCommands(); watcher.ProcessOneCommand();
            Assert.Equal(1, CommandRegistry.Executions);
        }
        watcher.ScanForCommands();
        Assert.False(File.Exists(FileAt("commands", "input")));
    }

    [Fact]
    public void LockedStaleFileDoesNotAbortCommandPump()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = FileAt("status", "stale");
        File.WriteAllText(path, "{}"); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-11));
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Submit("fresh"); watcher.ScanForCommands(); watcher.ProcessOneCommand();
        Assert.True((bool)Result("fresh")["success"]);
    }

    public void Dispose() { CommandRegistry.DuringExecution = null; Directory.Delete(root, true); }
}
