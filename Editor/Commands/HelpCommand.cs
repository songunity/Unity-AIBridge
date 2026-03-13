using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace AIBridge.Editor
{
    public static class HelpCommand
    {
        [AIBridge("Get help for all registered commands, or detailed info for a specific one",
            "AIBridgeCLI Help",
            "Help")]
        public static IEnumerator Help(
            [Description("Command name to get detailed help for (leave empty for all commands)")] string command = "")
        {
            if (string.IsNullOrEmpty(command))
            {
                var all = CommandRegistry.GetAll()
                    .OrderBy(e => e.Name)
                    .Select(e => new { e.Name, e.Description, e.Example });
                yield return CommandResult.Success(new { count = CommandRegistry.GetAll().Count(), commands = all });
            }
            else
            {
                if (!CommandRegistry.TryGetCommand(command, out var entry))
                {
                    yield return CommandResult.Failure($"Command '{command}' not found");
                    yield break;
                }
                yield return CommandResult.Success(BuildDetail(entry));
            }
        }

        private static object BuildDetail(CommandEntry entry)
        {
            var parameters = entry.Parameters.Select(p => new
            {
                name = p.Name,
                type = entry.GetTypeName(p),
                required = entry.IsRequired(p),
                description = entry.GetParamDescription(p),
                defaultValue = p.HasDefaultValue ? p.DefaultValue?.ToString() : null
            });

            return new
            {
                entry.Name,
                entry.Description,
                entry.Example,
                parameters
            };
        }
    }
}
