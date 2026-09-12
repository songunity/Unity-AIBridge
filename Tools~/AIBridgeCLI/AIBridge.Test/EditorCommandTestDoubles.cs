using System.Collections;
using System.Reflection;

// 只替代 Unity 调度边界；队列、状态与结果文件代码直接链接生产源码。
namespace AIBridge.Editor;

internal static class AIBridgeLogger
{
    public static void LogDebug(string text) { }
    public static void LogWarning(string text) { }
    public static void LogError(string text) { }
}
internal static class ScreenshotCacheManager { public static void CleanupOldScreenshots() { } }
internal static class EditorInstanceTracker { public static readonly string SessionId = "test-domain"; }
internal sealed class CommandEntry { public MethodInfo Method; }
internal static class CommandRegistry
{
    public static int Executions;
    public static Action<string> DuringExecution;
    public static bool DuplicateCallback;
    public static bool TryGetCommand(string name, out CommandEntry entry)
    {
        entry = new CommandEntry { Method = typeof(CommandRegistry).GetMethod(nameof(Execute)) };
        return name == "Test_Execute";
    }
    public static IEnumerator Execute(string id)
    {
        Executions++;
        DuringExecution?.Invoke(id);
        yield return CommandResult.SuccessWithId(id, new { executions = Executions });
    }
}
internal static class CommandParamBinder
{
    public static bool TryBind(CommandEntry entry, CommandRequest request, out object[] args, out string error)
    {
        args = new object[] { request.id }; error = null; return true;
    }
}
internal static class EditorCoroutineRunner
{
    public static void Start(IEnumerator iterator, Action<CommandResult> callback, string id)
    {
        while (iterator.MoveNext())
        {
            if (iterator.Current is not CommandResult result) continue;
            result.id = id;
            callback(result);
            if (CommandRegistry.DuplicateCallback) callback(CommandResult.FailureWithId(id, "duplicate completion"));
            return;
        }
    }
}
