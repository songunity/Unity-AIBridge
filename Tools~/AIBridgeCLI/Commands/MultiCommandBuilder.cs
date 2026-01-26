using System;
using System.Collections.Generic;
using AIBridgeCLI.Core;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// Multi command builder: execute multiple CLI commands in one call
    /// Supports simple command line syntax instead of complex JSON
    /// </summary>
    public class MultiCommandBuilder : BaseCommandBuilder
    {
        public override string Type => "multi";
        public override string Description => "Execute multiple commands efficiently in one call";

        public override string[] Actions => new[] { "run" };

        protected override Dictionary<string, List<ParameterInfo>> ActionParameters => new Dictionary<string, List<ParameterInfo>>
        {
            ["run"] = new List<ParameterInfo>
            {
                new ParameterInfo("commands", "Commands separated by semicolon or newline", false)
            }
        };

        /// <summary>
        /// Parse multiple command strings into batch request
        /// </summary>
        public CommandRequest BuildFromCommands(string[] commandLines, Dictionary<string, string> globalOptions)
        {
            var commands = new List<object>();

            foreach (var line in commandLines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                {
                    continue;
                }

                var parsed = ParseCommandLine(trimmed);
                if (parsed != null)
                {
                    commands.Add(parsed);
                }
            }

            if (commands.Count == 0)
            {
                throw new ArgumentException("No valid commands provided");
            }

            return new CommandRequest
            {
                id = PathHelper.GenerateCommandId(),
                type = "batch",
                @params = new Dictionary<string, object>
                {
                    ["commands"] = commands
                }
            };
        }

        /// <summary>
        /// Parse a single command line into command object
        /// Format: "type action --param1 value1 --param2 value2"
        /// </summary>
        private Dictionary<string, object> ParseCommandLine(string commandLine)
        {
            var parts = SplitCommandLine(commandLine);
            if (parts.Count == 0) return null;

            string type = parts[0];
            string action = null;
            var options = new Dictionary<string, object>();

            int i = 1;

            // Check if second part is action (not starting with --)
            if (i < parts.Count && !parts[i].StartsWith("--"))
            {
                action = parts[i];
                i++;
            }

            // Parse options
            while (i < parts.Count)
            {
                var part = parts[i];
                if (part.StartsWith("--"))
                {
                    var key = part.Substring(2);
                    if (i + 1 < parts.Count && !parts[i + 1].StartsWith("--"))
                    {
                        var value = parts[i + 1];
                        // Try to parse as number or boolean
                        options[key] = ParseValue(value);
                        i += 2;
                    }
                    else
                    {
                        options[key] = true;
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }

            // Add action to params if present
            if (!string.IsNullOrEmpty(action))
            {
                options["action"] = action;
            }

            return new Dictionary<string, object>
            {
                ["type"] = type,
                ["params"] = options
            };
        }

        /// <summary>
        /// Split command line respecting quotes (both single and double)
        /// </summary>
        private List<string> SplitCommandLine(string commandLine)
        {
            var result = new List<string>();
            var current = "";
            var inQuote = false;
            var quoteChar = ' ';

            for (int i = 0; i < commandLine.Length; i++)
            {
                var c = commandLine[i];

                if (!inQuote && (c == '"' || c == '\''))
                {
                    inQuote = true;
                    quoteChar = c;
                }
                else if (inQuote && c == quoteChar)
                {
                    inQuote = false;
                    quoteChar = ' ';
                }
                else if (!inQuote && c == ' ')
                {
                    if (!string.IsNullOrEmpty(current))
                    {
                        result.Add(current);
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }

            if (!string.IsNullOrEmpty(current))
            {
                result.Add(current);
            }

            return result;
        }

        /// <summary>
        /// Parse string value to appropriate type
        /// </summary>
        private new object ParseValue(string value)
        {
            if (bool.TryParse(value, out var boolVal))
                return boolVal;
            if (int.TryParse(value, out var intVal))
                return intVal;
            if (double.TryParse(value, out var doubleVal))
                return doubleVal;
            return value;
        }

        public override CommandRequest Build(string action, Dictionary<string, string> options)
        {
            // This is called when using standard CLI format
            // For multi command, we expect commands to be passed differently
            throw new ArgumentException("Use BuildFromCommands method for multi command");
        }
    }
}
