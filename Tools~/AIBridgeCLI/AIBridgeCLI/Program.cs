using System.Text;
using System.Text.Json;
using AIBridgeCLI.Commands;

namespace AIBridgeCLI;

internal class Program
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
            OutputFormatter.PrintError(ex.Message);
            return 1;
        }
    }

    static int Run(string[] args)
    {
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
                    OutputFormatter.PrintError("Invalid JSON from stdin");
                    return 1;
                }
            }
        }

        // Handle --json merge
        if (parsed.Options.TryGetValue("json", out var jsonStr) && !string.IsNullOrWhiteSpace(jsonStr))
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonStr);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (!CliConstants.GlobalOptions.Contains(prop.Name) && !parsed.Options.ContainsKey(prop.Name))
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
                OutputFormatter.PrintError("Invalid JSON in --json argument");
                return 1;
            }
        }

        // Build request
        CommandRequest request;
        try
        {
            request = RequestBuilder.BuildRequest(parsed);
        }
        catch (ArgumentException ex)
        {
            OutputFormatter.PrintError(ex.Message);
            return 1;
        }

        var sender = new CommandSender(parsed.Timeout);

        if (parsed.NoWait)
        {
            var commandId = sender.SendCommandNoWait(request);
            if (parsed.OutputMode == OutputMode.Pretty)
                OutputFormatter.PrintInfo($"Command sent with ID: {commandId}");
            else
                Console.WriteLine(JsonSerializer.Serialize(new { id = commandId, status = "sent" }, JsonContext.Default.Object));
            return 0;
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