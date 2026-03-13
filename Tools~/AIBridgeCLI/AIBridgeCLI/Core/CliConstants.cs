namespace AIBridgeCLI;

public static class CliConstants
{
    public const int DEFAULT_TIMEOUT = 5000;

    public static readonly HashSet<string> GlobalOptions = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "action", "json", "stdin", "timeout", "no-wait", "raw", "quiet", "help", "show-warnings", "cmd"
    };
}