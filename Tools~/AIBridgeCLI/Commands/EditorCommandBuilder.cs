using System.Collections.Generic;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// Editor command builder: log, undo, redo, compile, refresh, play, stop, pause, get_state
    /// </summary>
    public class EditorCommandBuilder : BaseCommandBuilder
    {
        public override string Type => "editor";
        public override string Description => "Editor operations (log, undo, redo, compile, play mode)";

        public override string[] Actions => new[]
        {
            "log", "undo", "redo", "compile", "refresh", "play", "stop", "pause", "get_state"
        };

        protected override Dictionary<string, List<ParameterInfo>> ActionParameters => new Dictionary<string, List<ParameterInfo>>
        {
            ["log"] = new List<ParameterInfo>
            {
                new ParameterInfo("message", "Log message to output", true),
                new ParameterInfo("logType", "Log type: Log, Warning, Error", false, "Log")
            },
            ["undo"] = new List<ParameterInfo>
            {
                new ParameterInfo("count", "Number of undo operations", false, "1")
            },
            ["redo"] = new List<ParameterInfo>
            {
                new ParameterInfo("count", "Number of redo operations", false, "1")
            },
            ["compile"] = new List<ParameterInfo>(),
            ["refresh"] = new List<ParameterInfo>
            {
                new ParameterInfo("forceUpdate", "Force update assets", false, "false")
            },
            ["play"] = new List<ParameterInfo>(),
            ["stop"] = new List<ParameterInfo>(),
            ["pause"] = new List<ParameterInfo>
            {
                new ParameterInfo("toggle", "Toggle pause state", false, "true"),
                new ParameterInfo("pause", "Set pause state directly", false)
            },
            ["get_state"] = new List<ParameterInfo>()
        };
    }
}
