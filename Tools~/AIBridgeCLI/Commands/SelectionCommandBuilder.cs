using System.Collections.Generic;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// Selection command builder: get, set, clear, add, remove
    /// </summary>
    public class SelectionCommandBuilder : BaseCommandBuilder
    {
        public override string Type => "selection";
        public override string Description => "Selection operations (get, set, clear, add, remove)";

        public override string[] Actions => new[]
        {
            "get", "set", "clear", "add", "remove"
        };

        protected override Dictionary<string, List<ParameterInfo>> ActionParameters => new Dictionary<string, List<ParameterInfo>>
        {
            ["get"] = new List<ParameterInfo>
            {
                new ParameterInfo("includeComponents", "Include component info in results", false, "false")
            },
            ["set"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("assetPath", "Path to an asset", false),
                new ParameterInfo("instanceId", "Instance ID of the object", false),
                new ParameterInfo("instanceIds", "Multiple instance IDs (comma-separated)", false)
            },
            ["clear"] = new List<ParameterInfo>(),
            ["add"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("assetPath", "Path to an asset", false),
                new ParameterInfo("instanceId", "Instance ID of the object", false)
            },
            ["remove"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("assetPath", "Path to an asset", false),
                new ParameterInfo("instanceId", "Instance ID of the object", false)
            }
        };
    }
}
