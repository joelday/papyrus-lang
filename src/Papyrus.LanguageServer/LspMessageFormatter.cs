using System.Buffers;
using System.Text;
using System.Text.Json;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;
using StreamJsonRpc.Reflection;

namespace Papyrus.LanguageServer;

/// <summary>
/// Drops top-level <c>"params": null</c> before STJ JSON-RPC deserialize.
/// StreamJsonRpc treats a JSON null params token as present and throws.
/// </summary>
internal sealed class LspMessageFormatter
    : IJsonRpcMessageFormatter,
        IJsonRpcMessageTextFormatter,
        IJsonRpcInstanceContainer,
        IJsonRpcMessageFactory,
        IJsonRpcFormatterTracingCallbacks
{
    private readonly SystemTextJsonFormatter _inner;

    internal LspMessageFormatter(SystemTextJsonFormatter inner)
    {
        _inner = inner;
    }

    public Encoding Encoding
    {
        get => _inner.Encoding;
        set => _inner.Encoding = value;
    }

    public JsonRpcMessage Deserialize(ReadOnlySequence<byte> contentBuffer)
    {
        return _inner.Deserialize(OmitNullParams(contentBuffer));
    }

    public JsonRpcMessage Deserialize(ReadOnlySequence<byte> contentBuffer, Encoding encoding)
    {
        return _inner.Deserialize(OmitNullParams(contentBuffer), encoding);
    }

    public void Serialize(IBufferWriter<byte> bufferWriter, JsonRpcMessage message)
    {
        _inner.Serialize(bufferWriter, message);
    }

#pragma warning disable CS0618
    public object GetJsonText(JsonRpcMessage message) => _inner.GetJsonText(message);
#pragma warning restore CS0618

    JsonRpc IJsonRpcInstanceContainer.Rpc
    {
        set => ((IJsonRpcInstanceContainer)_inner).Rpc = value;
    }

    JsonRpcRequest IJsonRpcMessageFactory.CreateRequestMessage()
    {
        return ((IJsonRpcMessageFactory)_inner).CreateRequestMessage();
    }

    JsonRpcError IJsonRpcMessageFactory.CreateErrorMessage()
    {
        return ((IJsonRpcMessageFactory)_inner).CreateErrorMessage();
    }

    JsonRpcResult IJsonRpcMessageFactory.CreateResultMessage()
    {
        return ((IJsonRpcMessageFactory)_inner).CreateResultMessage();
    }

    void IJsonRpcFormatterTracingCallbacks.OnSerializationComplete(
        JsonRpcMessage message,
        ReadOnlySequence<byte> encodedMessage
    )
    {
        ((IJsonRpcFormatterTracingCallbacks)_inner).OnSerializationComplete(
            message,
            encodedMessage
        );
    }

    private static ReadOnlySequence<byte> OmitNullParams(ReadOnlySequence<byte> buffer)
    {
        try
        {
            var reader = new Utf8JsonReader(buffer);
            if (!HasTopLevelNullParams(ref reader))
            {
                return buffer;
            }
        }
        catch (JsonException)
        {
            return buffer;
        }

        return RewriteWithoutNullParams(buffer);
    }

    private static bool HasTopLevelNullParams(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return false;
        }

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    var isParams = reader.ValueTextEquals("params"u8);
                    if (!reader.Read())
                    {
                        return false;
                    }

                    if (isParams)
                    {
                        return reader.TokenType == JsonTokenType.Null;
                    }

                    if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                    {
                        reader.Skip();
                    }

                    break;
                case JsonTokenType.EndObject:
                    return false;
            }
        }

        return false;
    }

    private static ReadOnlySequence<byte> RewriteWithoutNullParams(ReadOnlySequence<byte> buffer)
    {
        using var document = JsonDocument.Parse(buffer);
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("params"u8))
                {
                    continue;
                }

                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return new ReadOnlySequence<byte>(output.WrittenSpan.ToArray());
    }
}
