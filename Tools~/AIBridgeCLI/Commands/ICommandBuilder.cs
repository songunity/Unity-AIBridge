using System.Collections.Generic;
using AIBridgeCLI.Core;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// Interface for command builders
    /// </summary>
    public interface ICommandBuilder
    {
        /// <summary>
        /// The command type (e.g., "editor", "gameobject", "transform")
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Available actions for this command type
        /// </summary>
        string[] Actions { get; }

        /// <summary>
        /// Description of this command type
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Build a CommandRequest from parsed arguments
        /// </summary>
        /// <param name="action">The action to perform (can be null for single-action commands)</param>
        /// <param name="options">Key-value options from command line</param>
        /// <returns>A CommandRequest ready to be sent</returns>
        CommandRequest Build(string action, Dictionary<string, string> options);

        /// <summary>
        /// Get help text for this command
        /// </summary>
        /// <param name="action">Specific action to get help for (null for general help)</param>
        string GetHelp(string action = null);
    }

    /// <summary>
    /// Parameter definition for help text
    /// </summary>
    public class ParameterInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public string DefaultValue { get; set; }

        public ParameterInfo(string name, string description, bool required = false, string defaultValue = null)
        {
            Name = name;
            Description = description;
            Required = required;
            DefaultValue = defaultValue;
        }
    }
}
