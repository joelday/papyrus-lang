using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace Papyrus.LanguageServer;

internal sealed class LanguageServerRpc
{
    private readonly TaskCompletionSource<int> _exit =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _shutdown;

    internal Task<int> WaitForExitAsync() => _exit.Task;

    internal void NotifyDisconnected()
    {
        _exit.TrySetResult(1);
    }

    [JsonRpcMethod("initialize", UseSingleObjectParameterDeserialization = true)]
    public Task<InitializeResult> InitializeAsync(
        InitializeParams? parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            new InitializeResult
            {
                Capabilities = new ServerCapabilities(),
                ServerInfo = new ServerInfo { Name = "Papyrus Language Server" },
            });
    }

    [JsonRpcMethod("initialized", UseSingleObjectParameterDeserialization = true)]
    public Task InitializedAsync(InitializedParams? parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    [JsonRpcMethod("shutdown")]
    public Task ShutdownAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Volatile.Write(ref _shutdown, 1);
        return Task.CompletedTask;
    }

    [JsonRpcMethod("exit")]
    public Task ExitAsync()
    {
        _exit.TrySetResult(Volatile.Read(ref _shutdown) == 1 ? 0 : 1);
        return Task.CompletedTask;
    }
}
