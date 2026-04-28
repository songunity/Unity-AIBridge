using System.Text;

namespace AIBridgeCLI;

public static class HelpProvider
{
    public static string GetGlobalHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("AIBridgeCLI - Unity command forwarder");
        sb.AppendLine();
        sb.AppendLine("Usage:");
        sb.AppendLine("  AIBridgeCLI <CommandName> [options]");
        sb.AppendLine();
        sb.AppendLine("Global Options:");
        sb.AppendLine("  --timeout <ms>     Timeout in milliseconds (default: 5000)");
        sb.AppendLine("  --no-wait          Don't wait for result");
        sb.AppendLine("  --raw              Output raw JSON");
        sb.AppendLine("  --quiet            Quiet mode");
        sb.AppendLine("  --json <json>      Merge JSON object into forwarded params (overrides same keys)");
        sb.AppendLine("  --stdin            Read params JSON from stdin");
        sb.AppendLine("  --help             Show this help");
        sb.AppendLine();
        sb.AppendLine("Examples:");
        sb.AppendLine("  AIBridgeCLI GameObjectCommand_Find --name \"Main Camera\"");
        sb.AppendLine("  AIBridgeCLI ScreenshotCommand_Image");
        sb.AppendLine("  AIBridgeCLI GetLogsCommand_Log --count 10");
        sb.AppendLine("  AIBridgeCLI Help");
        sb.AppendLine();
        sb.AppendLine("Tip: use [AIBridgeCLI Help --command XXX] command to get command detail.");

        return sb.ToString();
    }
}