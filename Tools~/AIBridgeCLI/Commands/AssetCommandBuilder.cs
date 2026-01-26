using System.Collections.Generic;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// Asset command builder: find, search, import, refresh, get_path, load
    /// </summary>
    public class AssetCommandBuilder : BaseCommandBuilder
    {
        public override string Type => "asset";
        public override string Description => "AssetDatabase operations (find, search, import, refresh)";

        public override string[] Actions => new[]
        {
            "find", "search", "import", "refresh", "get_path", "load"
        };

        protected override Dictionary<string, List<ParameterInfo>> ActionParameters => new Dictionary<string, List<ParameterInfo>>
        {
            ["find"] = new List<ParameterInfo>
            {
                new ParameterInfo("filter", "Search filter (e.g., 't:Prefab', 't:Texture2D')", true),
                new ParameterInfo("searchInFolders", "Folders to search in (comma-separated)", false),
                new ParameterInfo("maxResults", "Maximum number of results", false, "100")
            },
            ["search"] = new List<ParameterInfo>
            {
                new ParameterInfo("mode", "Search mode: all, prefab, scene, script, texture, material, audio, animation, shader, font, model, so", false, "all"),
                new ParameterInfo("filter", "Custom Unity filter (overrides mode)", false),
                new ParameterInfo("keyword", "Search keyword (appended to filter)", false),
                new ParameterInfo("searchInFolders", "Folders to search in (comma-separated)", false),
                new ParameterInfo("maxResults", "Maximum number of results", false, "100")
            },
            ["import"] = new List<ParameterInfo>
            {
                new ParameterInfo("assetPath", "Path to the asset to reimport", true),
                new ParameterInfo("forceUpdate", "Force update the asset", false, "false")
            },
            ["refresh"] = new List<ParameterInfo>
            {
                new ParameterInfo("forceUpdate", "Force update all assets", false, "false")
            },
            ["get_path"] = new List<ParameterInfo>
            {
                new ParameterInfo("guid", "GUID of the asset", true)
            },
            ["load"] = new List<ParameterInfo>
            {
                new ParameterInfo("assetPath", "Path to the asset", true)
            }
        };
    }
}
