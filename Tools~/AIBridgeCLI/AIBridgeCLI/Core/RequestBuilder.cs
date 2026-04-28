using System.Text.Json;

namespace AIBridgeCLI;

public static class RequestBuilder
{
    public static CommandRequest BuildRequest(ParsedArgs parsed)
    {
        return new CommandRequest
        {
            id = PathHelper.GenerateCommandId(),
            type = parsed.CommandName,
            @params = BuildPackedParams(parsed)
        };
    }

    public static Dictionary<string, object> BuildPackedParams(ParsedArgs parsed)
    {
        var packedParams = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in parsed.Options)
        {
            if (CliConstants.GlobalOptions.Contains(kvp.Key))
            {
                continue;
            }

            packedParams[kvp.Key] = ParseValue(kvp.Value);
        }

        return packedParams;
    }

    public static object ParseValue(string value)
    {
        if (value == null)
        {
            return null;
        }

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (value.StartsWith("[") && value.EndsWith("]"))
        {
            try
            {
                var array = JsonSerializer.Deserialize(value, JsonContext.Default.ObjectArray);
                if (array != null)
                {
                    return array;
                }
            }
            catch
            {
            }
        }

        if (long.TryParse(value, out var longValue))
        {
            return longValue;
        }

        if (double.TryParse(value, out var doubleValue))
        {
            return doubleValue;
        }

        return value;
    }
}