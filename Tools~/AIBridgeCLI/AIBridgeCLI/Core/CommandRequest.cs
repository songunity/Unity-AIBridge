namespace AIBridgeCLI;

/// <summary>
/// Represents a command request to be sent to Unity
/// </summary>
public class CommandRequest
{
    public int protocolVersion { get; set; } = 2;
    public string id { get; set; }
    public string type { get; set; }
    public Dictionary<string, object> @params { get; set; }
}

/// <summary>
/// Represents a command result from Unity
/// </summary>
public class CommandResult
{
    public int? protocolVersion { get; set; }
    public string id { get; set; }
    public bool success { get; set; }
    public string error { get; set; }
    public string errorCode { get; set; }
    public string status { get; set; }
    public object timings { get; set; }
    public object data { get; set; }
    public long executionTime { get; set; }
}