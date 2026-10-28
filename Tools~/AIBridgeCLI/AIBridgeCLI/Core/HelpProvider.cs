using System.Text;
using System.Text.Json;

namespace AIBridgeCLI;

public static class HelpProvider
{
    // Catalog is produced by the loaded Editor registry, never a separately maintained command list.
    public static CommandResult ReadCatalog(string directory, string command, bool offline = false)
    {
        var path = Path.Combine(directory, "command-catalog.json");
        var metadataPath = Path.Combine(directory, "editor-instance.json");
        if (!File.Exists(path) || (!offline && !File.Exists(metadataPath))) return null;
        try
        {
            using var catalog = JsonDocument.Parse(CommandSender.ReadPublishedFile(path));
            using var metadata = File.Exists(metadataPath) ? JsonDocument.Parse(CommandSender.ReadPublishedFile(metadataPath)) : null;
            var root = catalog.RootElement;
            if (root.GetProperty("protocolVersion").GetInt32() != 2
                || (metadata != null && (metadata.RootElement.GetProperty("protocolVersion").GetInt32() != 2
                    || root.GetProperty("sessionId").GetString() != metadata.RootElement.GetProperty("sessionId").GetString()))) return null;
            var commands = root.GetProperty("commands");
            Dictionary<string, object> data;
            if (string.IsNullOrEmpty(command))
                data = new Dictionary<string, object> { ["count"] = (long)commands.GetArrayLength(), ["commands"] = commands.Clone() };
            else
            {
                var found = commands.EnumerateArray().FirstOrDefault(c => c.GetProperty("Name").GetString() == command);
                // A custom command may have been registered after catalog publication; let live Help resolve it.
                if (found.ValueKind == JsonValueKind.Undefined) return null;
                data = found.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value.Clone());
            }
            data["helpCatalog"] = new Dictionary<string, object>
            {
                ["source"] = "editor-session-catalog",
                ["sessionId"] = root.GetProperty("sessionId").Clone(),
                ["generatedAtUtc"] = root.GetProperty("generatedAtUtc").Clone(),
                ["sessionMetadataPresent"] = metadata != null
            };
            return new CommandResult { protocolVersion = 2, success = true, status = "completed", data = data };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }

    public static string GetGlobalHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("AIBridgeCLI - Unity command forwarder");
        sb.AppendLine();
        sb.AppendLine("Usage:");
        sb.AppendLine("  AIBridgeCLI <CommandName> [options]");
        sb.AppendLine("  AIBridgeCLI command status --id <id> --raw");
        sb.AppendLine("  AIBridgeCLI command result --id <id> [--wait --timeout <ms>] --raw");
        sb.AppendLine("  AIBridgeCLI runtime <subcommand> [options]");
        sb.AppendLine();
        sb.AppendLine("Global Options:");
        sb.AppendLine("  --timeout <ms>     Timeout in milliseconds (default: 5000)");
        sb.AppendLine("  --no-wait          Don't wait for result");
        sb.AppendLine("  --raw              Output raw JSON");
        sb.AppendLine("  --quiet            Quiet mode");
        sb.AppendLine("  --json <json>      Merge JSON object into forwarded params (overrides same keys)");
        sb.AppendLine("  --stdin            Read params JSON from stdin (CLI args take precedence)");
        sb.AppendLine("  --help             Show this help");
        sb.AppendLine();
        sb.AppendLine("Examples:");
        sb.AppendLine("  AIBridgeCLI GameObjectCommand_Find --name \"Main Camera\"");
        sb.AppendLine("  AIBridgeCLI ScreenshotCommand_Image");
        sb.AppendLine("  AIBridgeCLI Log --count 10");
        sb.AppendLine("  AIBridgeCLI runtime discover --raw");
        sb.AppendLine("  AIBridgeCLI runtime status --transport http --url http://127.0.0.1:27182 --raw");
        sb.AppendLine("  AIBridgeCLI Help --command CodeExecuteCommand_Execute --offline --raw");
        sb.AppendLine();
        sb.AppendLine("Tip: use [AIBridgeCLI Help --command XXX] command to get command detail.");

        return sb.ToString();
    }
}
