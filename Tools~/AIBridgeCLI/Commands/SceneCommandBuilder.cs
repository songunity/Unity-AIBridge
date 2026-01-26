using System.Collections.Generic;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// Scene command builder: load, save, get_hierarchy, get_active, new
    /// </summary>
    public class SceneCommandBuilder : BaseCommandBuilder
    {
        public override string Type => "scene";
        public override string Description => "Scene operations (load, save, hierarchy, new)";

        public override string[] Actions => new[]
        {
            "load", "save", "get_hierarchy", "get_active", "new"
        };

        protected override Dictionary<string, List<ParameterInfo>> ActionParameters => new Dictionary<string, List<ParameterInfo>>
        {
            ["load"] = new List<ParameterInfo>
            {
                new ParameterInfo("scenePath", "Path to the scene file", true),
                new ParameterInfo("mode", "Load mode: single or additive", false, "single")
            },
            ["save"] = new List<ParameterInfo>
            {
                new ParameterInfo("saveAs", "Path to save as (optional, saves current if not specified)", false)
            },
            ["get_hierarchy"] = new List<ParameterInfo>
            {
                new ParameterInfo("depth", "Max depth to traverse", false),
                new ParameterInfo("includeInactive", "Include inactive GameObjects", false, "true")
            },
            ["get_active"] = new List<ParameterInfo>(),
            ["new"] = new List<ParameterInfo>
            {
                new ParameterInfo("setup", "Scene setup: default or empty", false, "default")
            }
        };
    }
}
