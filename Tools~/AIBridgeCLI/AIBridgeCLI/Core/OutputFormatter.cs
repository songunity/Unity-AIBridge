using System;
using System.Text.Json;

namespace AIBridgeCLI;

public enum OutputMode
{
    Pretty,
    Raw,
    Quiet
}

public static class OutputFormatter
{
    public static void PrintResult(CommandResult result, OutputMode mode)
    {
        switch (mode)
        {
            case OutputMode.Raw:
                PrintRaw(result);
                break;
            case OutputMode.Quiet:
                PrintQuiet(result);
                break;
            case OutputMode.Pretty:
            default:
                PrintPretty(result);
                break;
        }
    }

    private static void PrintRaw(CommandResult result)
    {
        var json = JsonSerializer.Serialize(result, JsonContext.Default.CommandResult);
        Console.WriteLine(json);
    }

    private static void PrintQuiet(CommandResult result)
    {
        if (result.success)
        {
            if (result.data != null)
            {
                var json = JsonSerializer.Serialize(result.data, JsonContext.Default.Object);
                Console.WriteLine(json);
            }
        }
        else
        {
            Console.Error.WriteLine(result.error ?? "Command failed");
        }
    }

    private static void PrintPretty(CommandResult result)
    {
        if (result.success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("✓ ");
            Console.ResetColor();
            Console.WriteLine($"Command executed successfully ({result.executionTime}ms)");

            if (result.data != null)
            {
                PrintData(result.data, "  ");
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("✗ ");
            Console.ResetColor();
            Console.WriteLine($"Command failed ({result.executionTime}ms)");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Error: {result.error}");
            Console.ResetColor();
        }
    }

    private static void PrintData(object data, string indent)
    {
        if (data == null) return;

        JsonElement element;
        if (data is JsonElement je)
        {
            element = je;
        }
        else
        {
            var json = JsonSerializer.Serialize(data, JsonContext.Default.Object);
            element = JsonDocument.Parse(json).RootElement;
        }

        PrintElement(element, indent);
    }

    private static void PrintElement(JsonElement element, string indent)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        Console.WriteLine($"{indent}{prop.Name}:");
                        PrintElement(prop.Value, indent + "  ");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{indent}{prop.Name}: ");
                        Console.ResetColor();
                        PrintValue(prop.Value);
                    }
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    {
                        Console.WriteLine($"{indent}[{index}]:");
                        PrintElement(item, indent + "  ");
                    }
                    else
                    {
                        Console.Write($"{indent}[{index}]: ");
                        PrintValue(item);
                    }
                    index++;
                }
                break;

            default:
                Console.Write(indent);
                PrintValue(element);
                break;
        }
    }

    private static void PrintValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("true");
                Console.ResetColor();
                break;

            case JsonValueKind.False:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("false");
                Console.ResetColor();
                break;

            case JsonValueKind.Null:
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("null");
                Console.ResetColor();
                break;

            case JsonValueKind.Number:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(element.ToString());
                Console.ResetColor();
                break;

            case JsonValueKind.String:
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(element.GetString());
                Console.ResetColor();
                break;

            default:
                Console.WriteLine(element.ToString());
                break;
        }
    }

    public static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Error: {message}");
        Console.ResetColor();
    }

    public static void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Error.WriteLine($"Warning: {message}");
        Console.ResetColor();
    }

    public static void PrintInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("✓ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }
}
