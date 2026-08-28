using System;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace Papyrus.LanguageServer;

internal sealed class LanguageServerRpc : IDisposable
{
    private readonly TaskCompletionSource<int> _exit = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly LspRequestScheduler _scheduler;
    private readonly DidOpenHandler _didOpen;
    private readonly DidChangeHandler _didChange;
    private readonly DidCloseHandler _didClose;
    private readonly SnapshotHandler _snapshot;
    private readonly SemanticTokensFullHandler _semanticTokensFull;
    private readonly SemanticTokensRangeHandler _semanticTokensRange;
    private readonly FoldingRangeHandler _foldingRange;
    private readonly SelectionRangeHandler _selectionRange;
    private readonly DocumentSymbolHandler _documentSymbol;
    private readonly DocumentSyntaxTreeHandler _syntaxTree;
    private readonly FormattingHandler _formatting;
    private readonly HoverHandler _hover;
    private readonly DefinitionHandler _definition;
    private readonly CompletionHandler _completion;
    private readonly SignatureHelpHandler _signatureHelp;
    private readonly ReferencesHandler _references;
    private readonly RenameHandler _rename;
    private readonly PrepareRenameHandler _prepareRename;

    private int _shutdown;
    private int _disposed;

    internal LanguageServerRpc()
        : this(new LspRequestScheduler()) { }

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
        _foldingRange = new FoldingRangeHandler();
        _selectionRange = new SelectionRangeHandler();
        _documentSymbol = new DocumentSymbolHandler();
        _syntaxTree = new DocumentSyntaxTreeHandler();
        _formatting = new FormattingHandler();
        _hover = new HoverHandler();
        _definition = new DefinitionHandler();
        _completion = new CompletionHandler();
        _signatureHelp = new SignatureHelpHandler();
        _references = new ReferencesHandler();
        _rename = new RenameHandler();
        _prepareRename = new PrepareRenameHandler();
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
        CancellationToken cancellationToken
    )
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
                    FoldingRangeProvider = true,
                    SelectionRangeProvider = true,
                    DocumentSymbolProvider = true,
                    DocumentFormattingProvider = true,
                    HoverProvider = true,
                    DefinitionProvider = true,
                    CompletionProvider = new CompletionOptions { TriggerCharacters = ["."] },
                    SignatureHelpProvider = new SignatureHelpOptions
                    {
                        TriggerCharacters = ["(", ","],
                    },
                    ReferencesProvider = true,
                    RenameProvider = new RenameOptions { PrepareProvider = true },
                },
                ServerInfo = new ServerInfo { Name = "Papyrus Language Server" },
            }
        );
    }

    [JsonRpcMethod("initialized", UseSingleObjectParameterDeserialization = true)]
    public Task InitializedAsync(InitializedParams? parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    [JsonRpcMethod(DidOpenHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public async Task DidOpenAsync(
        DidOpenTextDocumentParams? parameters,
        CancellationToken cancellationToken
    )
    {
        await _scheduler
            .ExecuteAsync(
                _didOpen,
                parameters ?? new DidOpenTextDocumentParams(),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    [JsonRpcMethod(DidChangeHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public async Task DidChangeAsync(
        DidChangeTextDocumentParams? parameters,
        CancellationToken cancellationToken
    )
    {
        await _scheduler
            .ExecuteAsync(
                _didChange,
                parameters ?? new DidChangeTextDocumentParams(),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    [JsonRpcMethod(DidCloseHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public async Task DidCloseAsync(
        DidCloseTextDocumentParams? parameters,
        CancellationToken cancellationToken
    )
    {
        await _scheduler
            .ExecuteAsync(
                _didClose,
                parameters ?? new DidCloseTextDocumentParams(),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    [JsonRpcMethod(SnapshotHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<SnapshotResult> SnapshotAsync(
        SnapshotParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_snapshot, parameters, cancellationToken);
    }

    [JsonRpcMethod(
        SemanticTokensFullHandler.MethodName,
        UseSingleObjectParameterDeserialization = true
    )]
    public Task<SemanticTokens> SemanticTokensFullAsync(
        SemanticTokensParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_semanticTokensFull, parameters, cancellationToken);
    }

    [JsonRpcMethod(
        SemanticTokensRangeHandler.MethodName,
        UseSingleObjectParameterDeserialization = true
    )]
    public Task<SemanticTokens> SemanticTokensRangeAsync(
        SemanticTokensRangeParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_semanticTokensRange, parameters, cancellationToken);
    }

    [JsonRpcMethod(FoldingRangeHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<FoldingRange[]> FoldingRangeAsync(
        FoldingRangeParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_foldingRange, parameters, cancellationToken);
    }

    [JsonRpcMethod(
        SelectionRangeHandler.MethodName,
        UseSingleObjectParameterDeserialization = true
    )]
    public Task<SelectionRange[]> SelectionRangeAsync(
        SelectionRangeParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_selectionRange, parameters, cancellationToken);
    }

    [JsonRpcMethod(
        DocumentSymbolHandler.MethodName,
        UseSingleObjectParameterDeserialization = true
    )]
    public Task<DocumentSymbol[]> DocumentSymbolAsync(
        DocumentSymbolParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_documentSymbol, parameters, cancellationToken);
    }

    [JsonRpcMethod(
        DocumentSyntaxTreeHandler.MethodName,
        UseSingleObjectParameterDeserialization = true
    )]
    public Task<DocumentSyntaxTree?> SyntaxTreeAsync(
        DocumentSyntaxTreeParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_syntaxTree, parameters, cancellationToken);
    }

    [JsonRpcMethod(FormattingHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<TextEdit[]> FormattingAsync(
        DocumentFormattingParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_formatting, parameters, cancellationToken);
    }

    [JsonRpcMethod(HoverHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<Hover?> HoverAsync(
        TextDocumentPositionParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_hover, parameters, cancellationToken);
    }

    [JsonRpcMethod(DefinitionHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<Location[]> DefinitionAsync(
        DefinitionParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_definition, parameters, cancellationToken);
    }

    [JsonRpcMethod(CompletionHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<CompletionItem[]> CompletionAsync(
        CompletionParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_completion, parameters, cancellationToken);
    }

    [JsonRpcMethod(SignatureHelpHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<SignatureHelp?> SignatureHelpAsync(
        SignatureHelpParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_signatureHelp, parameters, cancellationToken);
    }

    [JsonRpcMethod(ReferencesHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<Location[]> ReferencesAsync(
        ReferenceParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_references, parameters, cancellationToken);
    }

    [JsonRpcMethod(RenameHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<WorkspaceEdit?> RenameAsync(
        RenameParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_rename, parameters, cancellationToken);
    }

    [JsonRpcMethod(
        PrepareRenameHandler.MethodName,
        UseSingleObjectParameterDeserialization = true
    )]
    public Task<PrepareRenameResult?> PrepareRenameAsync(
        TextDocumentPositionParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_prepareRename, parameters, cancellationToken);
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
