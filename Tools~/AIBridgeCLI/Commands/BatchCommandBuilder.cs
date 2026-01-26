using System;
using System.Collections.Generic;
using System.IO;
using AIBridgeCLI.Core;
using Newtonsoft.Json;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// Batch command builder: execute multiple commands at once
    /// </summary>
    public class BatchCommandBuilder : BaseCommandBuilder
    {
        public override string Type => "batch";
        public override string Description => "Execute multiple commands in a batch";

        public override string[] Actions => new[] { "execute", "from_file" };

        protected override Dictionary<string, List<ParameterInfo>> ActionParameters => new Dictionary<string, List<ParameterInfo>>
        {
            ["execute"] = new List<ParameterInfo>
            {
                new ParameterInfo("commands", "JSON array of commands", true)
            },
            ["from_file"] = new List<ParameterInfo>
            {
                new ParameterInfo("file", "Path to JSON file containing commands", true)
            }
        };

        public override CommandRequest Build(string action, Dictionary<string, string> options)
        {
            var request = new CommandRequest
            {
                id = PathHelper.GenerateCommandId(),
                type = Type,
                @params = new Dictionary<string, object>()
            };

            List<CommandRequest> commands = null;

            if (action == "from_file")
            {
                if (!options.TryGetValue("file", out var filePath))
                {
                    throw new ArgumentException("Missing required parameter: --file");
                }

                if (!File.Exists(filePath))
                {
                    throw new ArgumentException($"File not found: {filePath}");
                }

                var json = File.ReadAllText(filePath);
                commands = JsonConvert.DeserializeObject<List<CommandRequest>>(json);
            }
            else if (options.TryGetValue("commands", out var commandsJson))
            {
                commands = JsonConvert.DeserializeObject<List<CommandRequest>>(commandsJson);
            }
            else if (options.TryGetValue("json", out var jsonValue))
            {
                var batchData = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonValue);
                if (batchData.TryGetValue("commands", out var cmds))
                {
                    commands = JsonConvert.DeserializeObject<List<CommandRequest>>(JsonConvert.SerializeObject(cmds));
                }
            }

            if (commands == null || commands.Count == 0)
            {
                throw new ArgumentException("No commands provided for batch execution");
            }

            // Ensure each command has an ID
            foreach (var cmd in commands)
            {
                if (string.IsNullOrEmpty(cmd.id))
                {
                    cmd.id = PathHelper.GenerateCommandId();
                }
            }

            request.@params["commands"] = commands;

            return request;
        }
    }
}
