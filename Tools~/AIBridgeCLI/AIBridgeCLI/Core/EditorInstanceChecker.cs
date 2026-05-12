using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace AIBridgeCLI;

internal static class EditorInstanceChecker
{
    private const string MetadataFileName = "editor-instance.json";
    private static readonly TimeSpan MaxMetadataAge = TimeSpan.FromSeconds(10);

    public static (bool alive, string error) Check()
    {
        var exchangeDir = PathHelper.GetExchangeDirectory();
        var metadataPath = Path.Combine(exchangeDir, MetadataFileName);

        if (!File.Exists(metadataPath))
            return (false, "Unity Editor is not running or AIBridge is not active (metadata file not found).");

        EditorInstanceMetadata metadata;
        try
        {
            var json = File.ReadAllText(metadataPath);
            metadata = JsonSerializer.Deserialize(json, JsonContext.Default.EditorInstanceMetadata);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to read Unity Editor metadata: {ex.Message}");
        }

        if (metadata == null)
            return (false, "Unity Editor metadata is empty or invalid.");

        if (metadata.processId <= 0)
            return (false, "Unity Editor metadata does not contain a valid process ID.");

        // Check if process is alive
        try
        {
            var process = Process.GetProcessById(metadata.processId);
            process.Refresh();
            if (process.HasExited)
                return (false, "Unity Editor process has exited.");
        }
        catch (ArgumentException)
        {
            return (false, $"Unity Editor process {metadata.processId} is no longer running.");
        }
        catch
        {
            // Can't verify process, fall through to timestamp check
        }

        // Check heartbeat freshness
        if (!string.IsNullOrEmpty(metadata.lastUpdatedUtc) &&
            DateTime.TryParse(metadata.lastUpdatedUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var lastUpdated))
        {
            if (DateTime.UtcNow - lastUpdated > MaxMetadataAge)
                return (false, "Unity Editor is not responding (heartbeat stale). Reopen or refocus the Unity Editor.");
        }

        return (true, null);
    }
}

internal class EditorInstanceMetadata
{
    public int schemaVersion { get; set; }
    public int processId { get; set; }
    public string projectRoot { get; set; }
    public string projectName { get; set; }
    public string windowTitle { get; set; }
    public string lastUpdatedUtc { get; set; }
}
