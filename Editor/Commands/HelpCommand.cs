using System.Collections;
using System.ComponentModel;
using System.Linq;

namespace AIBridge.Editor
{
    public static class HelpCommand
    {
        [AIBridge("获取特定命令的详细信息",
            @"
AIBridgeCLI Help --command GameObjectCommand_Find  # 获取Find命令的详细信息",
            "Help")]
        public static IEnumerator Help(
            [Description("要获取详细帮助的命令名称")] string command = null)
        {
            if (string.IsNullOrEmpty(command))
            {
                var all = CommandRegistry.GetAll()
                    .OrderBy(e => e.Name)
                    .Select(e => new { e.Name, e.Description });
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

        internal static void ExportCatalog(string directory)
        {
            var path = System.IO.Path.Combine(directory, "command-catalog.json");
            var catalog = new
            {
                protocolVersion = 2,
                sessionId = EditorInstanceTracker.SessionId,
                generatedAtUtc = System.DateTime.UtcNow.ToString("O"),
                commands = CommandRegistry.GetAll().OrderBy(e => e.Name).Select(BuildDetail).ToArray()
            };
            try
            {
                var temporary = path + ".tmp";
                System.IO.File.WriteAllText(temporary, Newtonsoft.Json.JsonConvert.SerializeObject(catalog));
                if (System.IO.File.Exists(path)) System.IO.File.Replace(temporary, path, null);
                else System.IO.File.Move(temporary, path);
            }
            catch (System.IO.IOException ex) { AIBridgeLogger.LogWarning("Help catalog publication delayed until next initialization: " + ex.Message); }
            catch (System.UnauthorizedAccessException ex) { AIBridgeLogger.LogWarning("Help catalog unavailable: " + ex.Message); }
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
