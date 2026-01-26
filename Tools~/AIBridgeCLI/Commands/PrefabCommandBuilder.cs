using System.Collections.Generic;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// Prefab command builder: instantiate, save, unpack, get_info, apply
    /// </summary>
    public class PrefabCommandBuilder : BaseCommandBuilder
    {
        public override string Type => "prefab";
        public override string Description => "Prefab operations (instantiate, save, unpack, apply)";

        public override string[] Actions => new[]
        {
            "instantiate", "save", "unpack", "get_info", "apply"
        };

        protected override Dictionary<string, List<ParameterInfo>> ActionParameters => new Dictionary<string, List<ParameterInfo>>
        {
            ["instantiate"] = new List<ParameterInfo>
            {
                new ParameterInfo("prefabPath", "Path to the prefab asset", true),
                new ParameterInfo("posX", "X position", false, "0"),
                new ParameterInfo("posY", "Y position", false, "0"),
                new ParameterInfo("posZ", "Z position", false, "0")
            },
            ["save"] = new List<ParameterInfo>
            {
                new ParameterInfo("gameObjectPath", "Path to the GameObject (uses selection if not specified)", false),
                new ParameterInfo("savePath", "Path to save the prefab", true)
            },
            ["unpack"] = new List<ParameterInfo>
            {
                new ParameterInfo("gameObjectPath", "Path to the prefab instance (uses selection if not specified)", false),
                new ParameterInfo("completely", "Unpack completely (recursive)", false, "false")
            },
            ["get_info"] = new List<ParameterInfo>
            {
                new ParameterInfo("prefabPath", "Path to the prefab asset", false),
                new ParameterInfo("gameObjectPath", "Path to the prefab instance", false)
            },
            ["apply"] = new List<ParameterInfo>
            {
                new ParameterInfo("gameObjectPath", "Path to the prefab instance (uses selection if not specified)", false)
            }
        };
    }
}
