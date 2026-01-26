using System.Collections.Generic;
using AIBridgeCLI.Core;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// MenuItem command builder: invoke menu items
    /// </summary>
    public class MenuItemCommandBuilder : BaseCommandBuilder
    {
        public override string Type => "menu_item";
        public override string Description => "Invoke Unity menu items";

        public override string[] Actions => new[] { "invoke" };

        protected override Dictionary<string, List<ParameterInfo>> ActionParameters => new Dictionary<string, List<ParameterInfo>>
        {
            ["invoke"] = new List<ParameterInfo>
            {
                new ParameterInfo("menuPath", "Full menu path (e.g., 'GameObject/Create Empty')", true)
            },
            [""] = new List<ParameterInfo>
            {
                new ParameterInfo("menuPath", "Full menu path (e.g., 'GameObject/Create Empty')", true)
            }
        };

        public override CommandRequest Build(string action, Dictionary<string, string> options)
        {
            // For menu_item, we don't need action parameter
            var request = new CommandRequest
            {
                id = PathHelper.GenerateCommandId(),
                type = Type,
                @params = new Dictionary<string, object>()
            };

            foreach (var kvp in options)
            {
                if (kvp.Key == "json" || kvp.Key == "stdin") continue;
                request.@params[kvp.Key] = ParseValue(kvp.Value);
            }

            return request;
        }
    }
}
