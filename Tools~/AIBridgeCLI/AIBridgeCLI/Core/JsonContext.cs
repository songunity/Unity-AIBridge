using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIBridgeCLI;

[JsonSerializable(typeof(CommandRequest))]
[JsonSerializable(typeof(CommandResult))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(object[]), TypeInfoPropertyName = "ObjectArray")]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
internal partial class JsonContext : JsonSerializerContext
{
}
