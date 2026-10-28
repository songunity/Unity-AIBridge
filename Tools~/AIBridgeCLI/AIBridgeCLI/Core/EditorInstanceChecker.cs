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

    public static (bool alive, string error) Check(string exchangeDirectory = null)
    {
        var exchangeDir = exchangeDirectory ?? PathHelper.GetExchangeDirectory();
        var metadataPath = Path.Combine(exchangeDir, MetadataFileName);

        if (!File.Exists(metadataPath))
            return (false, $"No Editor metadata at {metadataPath}. Verify the project/CLI path and Editor process before retrying. Existing help may be read with Help --offline --raw.");

        EditorInstanceMetadata metadata;
        try
        {
            var json = CommandSender.ReadPublishedFile(metadataPath);
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
                return (false, $"Unity Editor process {metadata.processId} has a stale heartbeat ({(DateTime.UtcNow - lastUpdated).TotalSeconds:F0}s). Check compilation/import progress and Editor logs; wait for the heartbeat to change before retrying. This does not prove the Editor has exited.");
        }

        return (true, null);
    }
}

internal class EditorInstanceMetadata
{
    public int schemaVersion { get; set; }
    public int protocolVersion { get; set; }
    public string sessionId { get; set; }
    public int processId { get; set; }
    public string projectRoot { get; set; }
    public string projectName { get; set; }
    public string windowTitle { get; set; }
    public string lastUpdatedUtc { get; set; }
}
