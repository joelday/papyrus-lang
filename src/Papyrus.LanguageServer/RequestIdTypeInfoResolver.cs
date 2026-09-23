using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using StreamJsonRpc;

namespace Papyrus.LanguageServer;

/// <summary>
/// Metadata for StreamJsonRpc's <see cref="RequestId"/>, the parameter of
/// <c>$/cancelRequest</c>. Its own converter is internal, so the source-generated
/// context cannot include it; without this, cancellation notifications fail to
/// deserialize and are silently dropped.
/// </summary>
internal sealed class RequestIdTypeInfoResolver : IJsonTypeInfoResolver
{
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) =>
        type == typeof(RequestId) ? JsonMetadataServices.CreateValueInfo<RequestId>(options, new RequestIdConverter()) : null;

    private sealed class RequestIdConverter : JsonConverter<RequestId>
    {
        public override RequestId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.Number => new RequestId(reader.GetInt64()),
                JsonTokenType.String => new RequestId(reader.GetString()),
                _ => RequestId.Null,
            };

        public override void Write(Utf8JsonWriter writer, RequestId value, JsonSerializerOptions options)
        {
            if (value.Number is { } number)
            {
                writer.WriteNumberValue(number);
            }
            else if (value.String is { } text)
            {
                writer.WriteStringValue(text);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
