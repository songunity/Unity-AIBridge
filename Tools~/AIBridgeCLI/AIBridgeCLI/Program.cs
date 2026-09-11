using System.Text;
using System.Text.Json;
using AIBridgeCLI.Commands;

namespace AIBridgeCLI;

public class Program
{
    public static int Main(string[] args)
    {
        // Set console output encoding to UTF-8
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            if (args.Any(arg => string.Equals(arg, "--raw", StringComparison.OrdinalIgnoreCase)))
                OutputFormatter.PrintResult(CommandSender.Failure(null, "CLI_ERROR", ex.Message), OutputMode.Raw);
            else OutputFormatter.PrintError(ex.Message);
            return 1;
        }
    }

    static int Run(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "runtime", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeCliCommand.Execute(args.Skip(1).ToArray());
        }

        if (args.Length > 0 && args[0] == "command")
        {
            if (args.Length < 2 || (args[1] != "status" && args[1] != "result"))
                throw new ArgumentException("Use command status|result --id <id> [--wait] [--timeout <ms>].");
            var query = ParsedArgs.Parse(args.Skip(1).ToArray());
            query.Options.TryGetValue("id", out var id);
            CommandSender.ValidateId(id);
            var client = new CommandSender(query.Timeout);
            var response = args[1] == "status" ? client.GetStatus(id)
                : query.Options.ContainsKey("wait") ? client.WaitForResult(id, query.Timeout)
                : client.TryGetResult(id) ?? CommandSender.Failure(id, "RESULT_UNAVAILABLE", "No retained result; query command status.");
            OutputFormatter.PrintResult(response, query.OutputMode);
            return response.success ? 0 : 1;
        }

        var parsed = ParsedArgs.Parse(args);
            
        // Global help
        if (parsed.Help && string.IsNullOrEmpty(parsed.CommandName))
        {
            Console.WriteLine(HelpProvider.GetGlobalHelp());
            return 0;
        }

        if (parsed.CommandName == "Compile")
        {
            var compileResult = CompileUnityCommand.Compile(parsed.Timeout);
            OutputFormatter.PrintResult(compileResult, parsed.OutputMode);
            return compileResult.success ? 0 : 1;
        }

        // Handle stdin input
        if (parsed.Stdin)
        {
            var stdinJson = Console.In.ReadToEnd();
            if (!string.IsNullOrWhiteSpace(stdinJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(stdinJson);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("stdin JSON must be an object.");
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (!parsed.Options.ContainsKey(prop.Name))
                            {
                                parsed.Options[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                                    ? prop.Value.GetString()
                                    : prop.Value.GetRawText();
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    throw new ArgumentException("Invalid JSON from stdin");
                }
            }
        }

        // Handle --json merge (overrides same keys per help contract)
        if (parsed.Options.TryGetValue("json", out var jsonStr) && !string.IsNullOrWhiteSpace(jsonStr))
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonStr);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("--json must be an object.");
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (!CliConstants.GlobalOptions.Contains(prop.Name))
                        {
                            parsed.Options[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                                ? prop.Value.GetString()
                                : prop.Value.GetRawText();
                        }
                    }
                }
            }
            catch (JsonException)
            {
                throw new ArgumentException("Invalid JSON in --json argument");
            }
        }

        // Build request
        var request = RequestBuilder.BuildRequest(parsed);

        var sender = new CommandSender(parsed.Timeout);

        if (parsed.NoWait)
        {
            var sent = sender.Submit(request);
            OutputFormatter.PrintResult(sent, parsed.OutputMode);
            return sent.success ? 0 : 1;
        }

        var result = sender.SendCommand(request);
        OutputFormatter.PrintResult(result, parsed.OutputMode);
        return result.success ? 0 : 1;
    }


    static int Test(string[] args, out CommandRequest request)
    {
        var parsed = ParsedArgs.Parse(args);
        request = null;

        if (string.Equals(parsed.CommandName, "focus", StringComparison.OrdinalIgnoreCase))
            return 0;

        if (parsed.Help && string.IsNullOrEmpty(parsed.CommandName))
        {
            Console.WriteLine(HelpProvider.GetGlobalHelp());
            return 0;
        }

        try
        {
            request = RequestBuilder.BuildRequest(parsed);
        }
        catch (ArgumentException ex)
        {
            OutputFormatter.PrintError(ex.Message);
            return 1;
        }

        return 0;
    }
}
