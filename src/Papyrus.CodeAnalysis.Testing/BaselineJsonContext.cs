using System.Text.Json.Serialization;

namespace Papyrus.CodeAnalysis.Testing;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BaselineDocument))]
internal sealed partial class BaselineJsonContext : JsonSerializerContext
{
}
