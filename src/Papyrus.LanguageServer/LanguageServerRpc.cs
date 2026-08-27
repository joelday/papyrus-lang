using System;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace Papyrus.LanguageServer;

internal sealed class LanguageServerRpc : IDisposable
{
    private readonly TaskCompletionSource<int> _exit =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly LspRequestScheduler _scheduler;
    private readonly DidOpenHandler _didOpen;
    private readonly DidChangeHandler _didChange;
    private readonly DidCloseHandler _didClose;
    private readonly SnapshotHandler _snapshot;
    private readonly SemanticTokensFullHandler _semanticTokensFull;
    private readonly SemanticTokensRangeHandler _semanticTokensRange;

    private int _shutdown;
    private int _disposed;

    internal LanguageServerRpc()
        : this(new LspRequestScheduler())
    {
    }

    internal LanguageServerRpc(LspRequestScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        _scheduler = scheduler;
        _didOpen = new DidOpenHandler(scheduler.Workspace);
        _didChange = new DidChangeHandler(scheduler.Workspace);
        _didClose = new DidCloseHandler(scheduler.Workspace);
        _snapshot = new SnapshotHandler();
        _semanticTokensFull = new SemanticTokensFullHandler();
        _semanticTokensRange = new SemanticTokensRangeHandler();
    }

    internal Task<int> WaitForExitAsync() => _exit.Task;

    internal void NotifyDisconnected()
    {
        Dispose();
        _exit.TrySetResult(1);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _scheduler.Dispose();
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
                Capabilities = new ServerCapabilities
                {
                    TextDocumentSync = new TextDocumentSyncOptions
                    {
                        OpenClose = true,
                        Change = TextDocumentSyncKind.Full,
                    },
                    SemanticTokensProvider = LspSemanticTokens.CapabilityOptions(),
                },
                ServerInfo = new ServerInfo { Name = "Papyrus Language Server" },
            });
    }

    [JsonRpcMethod("initialized", UseSingleObjectParameterDeserialization = true)]
    public Task InitializedAsync(InitializedParams? parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    [JsonRpcMethod(DidOpenHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public async Task DidOpenAsync(DidOpenTextDocumentParams? parameters, CancellationToken cancellationToken)
    {
        await _scheduler.ExecuteAsync(
            _didOpen,
            parameters ?? new DidOpenTextDocumentParams(),
            cancellationToken).ConfigureAwait(false);
    }

    [JsonRpcMethod(DidChangeHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public async Task DidChangeAsync(DidChangeTextDocumentParams? parameters, CancellationToken cancellationToken)
    {
        await _scheduler.ExecuteAsync(
            _didChange,
            parameters ?? new DidChangeTextDocumentParams(),
            cancellationToken).ConfigureAwait(false);
    }

    [JsonRpcMethod(DidCloseHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public async Task DidCloseAsync(DidCloseTextDocumentParams? parameters, CancellationToken cancellationToken)
    {
        await _scheduler.ExecuteAsync(
            _didClose,
            parameters ?? new DidCloseTextDocumentParams(),
            cancellationToken).ConfigureAwait(false);
    }

    [JsonRpcMethod(SnapshotHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<SnapshotResult> SnapshotAsync(SnapshotParams? parameters, CancellationToken cancellationToken)
    {
        return _scheduler.ExecuteAsync(_snapshot, parameters, cancellationToken);
    }

    [JsonRpcMethod(SemanticTokensFullHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<SemanticTokens> SemanticTokensFullAsync(
        SemanticTokensParams? parameters,
        CancellationToken cancellationToken)
    {
        return _scheduler.ExecuteAsync(_semanticTokensFull, parameters, cancellationToken);
    }

    [JsonRpcMethod(SemanticTokensRangeHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<SemanticTokens> SemanticTokensRangeAsync(
        SemanticTokensRangeParams? parameters,
        CancellationToken cancellationToken)
    {
        return _scheduler.ExecuteAsync(_semanticTokensRange, parameters, cancellationToken);
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
        Dispose();
        _exit.TrySetResult(Volatile.Read(ref _shutdown) == 1 ? 0 : 1);
        return Task.CompletedTask;
    }
}
