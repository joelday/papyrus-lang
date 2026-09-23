using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace Papyrus.LanguageServer;

internal static class LanguageServerHost
{
    internal static Task<int> RunStdioAsync(CancellationToken cancellationToken = default)
    {
        // Leave stdio open: disposing Console streams on some hosts closes the process handles.
        var input = PipeReader.Create(
            Console.OpenStandardInput(),
            new StreamPipeReaderOptions(leaveOpen: true)
        );
        var output = PipeWriter.Create(
            Console.OpenStandardOutput(),
            new StreamPipeWriterOptions(leaveOpen: true)
        );
        return RunAsync(input, output, cancellationToken);
    }

    internal static async Task<int> RunAsync(
        PipeReader input,
        PipeWriter output,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        using var target = new LanguageServerRpc();
        var formatter = CreateFormatter();
        var handler = new HeaderDelimitedMessageHandler(output, input, formatter);
        using var jsonRpc = new JsonRpc(handler);
        AddRpcTarget(jsonRpc, target);
        target.Notifier = new JsonRpcNotifier(jsonRpc);
        jsonRpc.CancelLocallyInvokedMethodsWhenConnectionIsClosed = true;
        jsonRpc.Disconnected += (_, _) => target.NotifyDisconnected();
        jsonRpc.StartListening();

        using (cancellationToken.Register(static state => ((JsonRpc)state!).Dispose(), jsonRpc))
        {
            return await target
                .WaitForExitAsync()
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "RPC methods are registered from LanguageServerRpc."
    )]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "RPC methods are registered from LanguageServerRpc."
    )]
    private static void AddRpcTarget(JsonRpc jsonRpc, LanguageServerRpc target)
    {
        jsonRpc.AddLocalRpcTarget(
            target,
            new JsonRpcTargetOptions
            {
                NotifyClientOfEvents = false,
                UseSingleObjectParameterDeserialization = true,
            }
        );
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "User payloads go through LspJsonContext."
    )]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "User payloads go through LspJsonContext."
    )]
    private static IJsonRpcMessageFormatter CreateFormatter()
    {
        var inner = new SystemTextJsonFormatter
        {
            JsonSerializerOptions =
            {
                TypeInfoResolver = JsonTypeInfoResolver.Combine(LspJsonContext.Default, new RequestIdTypeInfoResolver()),
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            },
        };
        return new LspMessageFormatter(inner);
    }
}
