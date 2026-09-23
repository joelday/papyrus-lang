using System;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.Workspaces;
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
    private readonly RangeFormattingHandler _rangeFormatting = new();
    private readonly CodeLensHandler _codeLens = new();
    private readonly CodeLensResolveHandler _codeLensResolve = new();
    private readonly OnTypeFormattingHandler _onTypeFormatting = new();
    private readonly HoverHandler _hover;
    private readonly DefinitionHandler _definition;
    private readonly CompletionHandler _completion;
    private readonly CompletionResolveHandler _completionResolve;
    private readonly SignatureHelpHandler _signatureHelp;
    private readonly ReferencesHandler _references;
    private readonly RenameHandler _rename;
    private readonly PrepareRenameHandler _prepareRename;
    private readonly DocumentHighlightHandler _documentHighlight;
    private readonly InlayHintHandler _inlayHint;
    private readonly DiagnosticHandler _diagnostic;
    private readonly WorkspaceDiagnosticHandler _workspaceDiagnostic = new();
    private readonly WorkspaceDiagnosticsPoller _workspaceDiagnostics = new(TimeSpan.FromSeconds(1));
    private volatile bool _clientPullsDiagnostics;
    private volatile bool _clientRefreshesDiagnostics;
    private readonly DocumentScriptInfoHandler _scriptInfo;
    private readonly ProjectInfosHandler _projectInfos;
    private readonly WorkspaceSymbolHandler _workspaceSymbol = new();
    private readonly TypeDefinitionHandler _typeDefinition = new();
    private readonly ImplementationHandler _implementation = new();
    private readonly PrepareCallHierarchyHandler _prepareCallHierarchy = new();
    private readonly IncomingCallsHandler _incomingCalls = new();
    private readonly OutgoingCallsHandler _outgoingCalls = new();
    private readonly PrepareTypeHierarchyHandler _prepareTypeHierarchy = new();
    private readonly SupertypesHandler _supertypes = new();
    private readonly SubtypesHandler _subtypes = new();
    private readonly DocumentAssemblyHandler _assembly;
    private readonly WorkspaceFoldersHandler _workspaceFolders;
    private readonly WatchedFilesHandler _watchedFiles;
    private readonly OpenBuffers _openBuffers = new();
    private WorkspaceFolderChange? _initialWorkspaceFolders;

    private int _shutdown;
    private int _disposed;

    internal LanguageServerRpc()
        : this(new LspRequestScheduler()) { }

    internal LanguageServerRpc(LspRequestScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        _scheduler = scheduler;
        _didOpen = new DidOpenHandler(scheduler.Workspace, _openBuffers);
        _didChange = new DidChangeHandler(scheduler.Workspace, _openBuffers);
        _didClose = new DidCloseHandler(scheduler.Workspace, _openBuffers);
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
        _completionResolve = new CompletionResolveHandler();
        _signatureHelp = new SignatureHelpHandler();
        _references = new ReferencesHandler();
        _rename = new RenameHandler();
        _prepareRename = new PrepareRenameHandler();
        _documentHighlight = new DocumentHighlightHandler();
        _inlayHint = new InlayHintHandler();
        _diagnostic = new DiagnosticHandler();
        _scriptInfo = new DocumentScriptInfoHandler();
        _projectInfos = new ProjectInfosHandler();
        _assembly = new DocumentAssemblyHandler();
        _workspaceFolders = new WorkspaceFoldersHandler(scheduler.Workspace, () => Notifier, _openBuffers);
        _watchedFiles = new WatchedFilesHandler(scheduler.Workspace, () => Notifier, _openBuffers);
        _scheduler.Workspace.WorkspaceChanged += OnWorkspaceChanged;
    }

    /// <summary>
    /// Set once the transport is listening so <see cref="OnWorkspaceChanged"/> can push
    /// <c>textDocument/publishDiagnostics</c>. Left null in tests that never attach a client.
    /// </summary>
    internal ILspNotifier? Notifier { get; set; }

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

        _scheduler.Workspace.WorkspaceChanged -= OnWorkspaceChanged;
        _scheduler.Dispose();
    }

    // Fired for every workspace mutation -- LSP-request-driven (didOpen/didChange) and
    // file-watcher-driven (out-of-editor edits) alike -- so this single hook keeps open
    // documents' diagnostics current regardless of what triggered the change.
    private void OnWorkspaceChanged(object? sender, WorkspaceChangeEventArgs e)
    {
        _workspaceDiagnostics.NotifyChanged();
        if (Notifier is not { } notifier || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (!_clientPullsDiagnostics)
        {
            _ = PublishDiagnosticsAsync(notifier, e.NewSolution);
        }
        else if (_clientRefreshesDiagnostics && ChangedOutsideEditor(e.Changes))
        {
            // The client re-pulls on its own edits, but not on files changed on disk.
            _ = RefreshDiagnosticsAsync(notifier);
        }
    }

    private static bool ChangedOutsideEditor(System.Collections.Immutable.ImmutableArray<WorkspaceChange> changes)
    {
        foreach (var change in changes)
        {
            if (change is not (WorkspaceChange.OpenDocument or WorkspaceChange.ChangeDocument or WorkspaceChange.CloseDocument))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task RefreshDiagnosticsAsync(ILspNotifier notifier)
    {
        try
        {
            await notifier.RequestAsync("workspace/diagnostic/refresh", CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best effort, like the push path.
        }
    }

    private static async Task PublishDiagnosticsAsync(
        ILspNotifier notifier,
        Papyrus.Workspaces.PapyrusSolution solution
    )
    {
        foreach (var documentId in solution.OpenDocumentIds)
        {
            try
            {
                if (!solution.TryGetSyntaxTree(documentId, out var tree))
                {
                    continue;
                }

                var diagnostics = solution.GetDiagnostics(documentId);
                var lineMap = tree.GetText().Lines;
                await notifier
                    .NotifyAsync(
                        "textDocument/publishDiagnostics",
                        new PublishDiagnosticsParams
                        {
                            Uri = LspUris.ToUri(documentId.Path),
                            Diagnostics = LspDiagnostics.ToLsp(diagnostics, lineMap),
                        },
                        CancellationToken.None
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Best-effort push: a client disconnect or a transient analyzer error
                // should not tear down the workspace-change subscription.
            }
        }
    }

    [JsonRpcMethod("initialize", UseSingleObjectParameterDeserialization = true)]
    public Task<InitializeResult> InitializeAsync(
        InitializeParams? parameters,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        _clientPullsDiagnostics = parameters?.Capabilities?.TextDocument?.Diagnostic is not null;
        _clientRefreshesDiagnostics = parameters?.Capabilities?.Workspace?.Diagnostics?.RefreshSupport == true;

        // Projects load on `initialized`, once the client can receive log messages.
        Volatile.Write(
            ref _initialWorkspaceFolders,
            WorkspaceFolderChange.FromInitialize(parameters)
        );
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
                    DocumentRangeFormattingProvider = true,
                    CodeLensProvider = new CodeLensOptions { ResolveProvider = true },
                    DocumentOnTypeFormattingProvider = new DocumentOnTypeFormattingOptions
                    {
                        FirstTriggerCharacter = OnTypeFormattingHandler.TriggerCharacters[0],
                    },
                    HoverProvider = true,
                    DefinitionProvider = true,
                    CompletionProvider = new CompletionOptions
                    {
                        TriggerCharacters = ["."],
                        ResolveProvider = true,
                    },
                    SignatureHelpProvider = new SignatureHelpOptions
                    {
                        TriggerCharacters = ["(", ","],
                    },
                    ReferencesProvider = true,
                    RenameProvider = new RenameOptions { PrepareProvider = true },
                    DocumentHighlightProvider = true,
                    InlayHintProvider = true,
                    DiagnosticProvider = new DiagnosticOptions
                    {
                        InterFileDependencies = true,
                        WorkspaceDiagnostics = true,
                    },
                    WorkspaceSymbolProvider = true,
                    TypeDefinitionProvider = true,
                    ImplementationProvider = true,
                    CallHierarchyProvider = true,
                    TypeHierarchyProvider = true,
                    Workspace = new WorkspaceServerCapabilities
                    {
                        WorkspaceFolders = new WorkspaceFoldersServerCapabilities
                        {
                            Supported = true,
                            ChangeNotifications = true,
                        },
                    },
                },
                ServerInfo = new ServerInfo { Name = "Papyrus Language Server" },
            }
        );
    }

    [JsonRpcMethod("initialized", UseSingleObjectParameterDeserialization = true)]
    public async Task InitializedAsync(
        InitializedParams? parameters,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref _initialWorkspaceFolders, null) is { } folders)
        {
            await _scheduler
                .ExecuteAsync(_workspaceFolders, folders, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    [JsonRpcMethod(
        WorkspaceFoldersHandler.MethodName,
        UseSingleObjectParameterDeserialization = true
    )]
    public async Task DidChangeWorkspaceFoldersAsync(
        DidChangeWorkspaceFoldersParams? parameters,
        CancellationToken cancellationToken
    )
    {
        await _scheduler
            .ExecuteAsync(
                _workspaceFolders,
                WorkspaceFolderChange.FromDidChange(parameters),
                cancellationToken
            )
            .ConfigureAwait(false);
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

    [JsonRpcMethod(CodeLensHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<CodeLens[]> CodeLensAsync(CodeLensParams? parameters, CancellationToken cancellationToken) =>
        _scheduler.ExecuteAsync(_codeLens, parameters, cancellationToken);

    [JsonRpcMethod(CodeLensResolveHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<CodeLens?> CodeLensResolveAsync(CodeLens? parameters, CancellationToken cancellationToken) =>
        _scheduler.ExecuteAsync(_codeLensResolve, parameters, cancellationToken);

    [JsonRpcMethod(RangeFormattingHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<TextEdit[]> RangeFormattingAsync(
        DocumentRangeFormattingParams? parameters,
        CancellationToken cancellationToken
    ) => _scheduler.ExecuteAsync(_rangeFormatting, parameters, cancellationToken);

    [JsonRpcMethod(OnTypeFormattingHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<TextEdit[]> OnTypeFormattingAsync(
        DocumentOnTypeFormattingParams? parameters,
        CancellationToken cancellationToken
    ) => _scheduler.ExecuteAsync(_onTypeFormatting, parameters, cancellationToken);

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

    [JsonRpcMethod(
        CompletionResolveHandler.MethodName,
        UseSingleObjectParameterDeserialization = true
    )]
    public Task<CompletionItem?> CompletionResolveAsync(
        CompletionItem? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_completionResolve, parameters, cancellationToken);
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

    [JsonRpcMethod(PrepareRenameHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<PrepareRenameResult?> PrepareRenameAsync(
        TextDocumentPositionParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_prepareRename, parameters, cancellationToken);
    }

    [JsonRpcMethod(
        DocumentHighlightHandler.MethodName,
        UseSingleObjectParameterDeserialization = true
    )]
    public Task<DocumentHighlight[]> DocumentHighlightAsync(
        DocumentHighlightParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_documentHighlight, parameters, cancellationToken);
    }

    [JsonRpcMethod(InlayHintHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<InlayHint[]> InlayHintAsync(
        InlayHintParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_inlayHint, parameters, cancellationToken);
    }

    [JsonRpcMethod(WorkspaceDiagnosticHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<WorkspaceDiagnosticReport> WorkspaceDiagnosticAsync(
        WorkspaceDiagnosticParams? parameters,
        CancellationToken cancellationToken
    ) =>
        _workspaceDiagnostics.PollAsync(
            parameters,
            ct => _scheduler.ExecuteAsync(_workspaceDiagnostic, parameters, ct),
            cancellationToken
        );

    [JsonRpcMethod(DiagnosticHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<DocumentDiagnosticReport> DiagnosticAsync(
        DiagnosticParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_diagnostic, parameters, cancellationToken);
    }

    [JsonRpcMethod(
        DocumentScriptInfoHandler.MethodName,
        UseSingleObjectParameterDeserialization = true
    )]
    public Task<DocumentScriptInfo> ScriptInfoAsync(
        DocumentScriptInfoParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_scriptInfo, parameters, cancellationToken);
    }

    [JsonRpcMethod(WorkspaceSymbolHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<SymbolInformation[]> WorkspaceSymbolAsync(
        WorkspaceSymbolParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_workspaceSymbol, parameters, cancellationToken);
    }

    [JsonRpcMethod(WatchedFilesHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task DidChangeWatchedFilesAsync(DidChangeWatchedFilesParams? parameters, CancellationToken cancellationToken) =>
        _scheduler.ExecuteAsync(_watchedFiles, parameters, cancellationToken);

    [JsonRpcMethod(TypeDefinitionHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<Location[]> TypeDefinitionAsync(DefinitionParams? parameters, CancellationToken cancellationToken) =>
        _scheduler.ExecuteAsync(_typeDefinition, parameters, cancellationToken);

    [JsonRpcMethod(ImplementationHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<Location[]> ImplementationAsync(DefinitionParams? parameters, CancellationToken cancellationToken) =>
        _scheduler.ExecuteAsync(_implementation, parameters, cancellationToken);

    [JsonRpcMethod(PrepareCallHierarchyHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<HierarchyItem[]?> PrepareCallHierarchyAsync(
        TextDocumentPositionParams? parameters,
        CancellationToken cancellationToken
    ) => _scheduler.ExecuteAsync(_prepareCallHierarchy, parameters, cancellationToken);

    [JsonRpcMethod(IncomingCallsHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<CallHierarchyIncomingCall[]?> IncomingCallsAsync(
        HierarchyItemParams? parameters,
        CancellationToken cancellationToken
    ) => _scheduler.ExecuteAsync(_incomingCalls, parameters, cancellationToken);

    [JsonRpcMethod(OutgoingCallsHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<CallHierarchyOutgoingCall[]?> OutgoingCallsAsync(
        HierarchyItemParams? parameters,
        CancellationToken cancellationToken
    ) => _scheduler.ExecuteAsync(_outgoingCalls, parameters, cancellationToken);

    [JsonRpcMethod(PrepareTypeHierarchyHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<HierarchyItem[]?> PrepareTypeHierarchyAsync(
        TextDocumentPositionParams? parameters,
        CancellationToken cancellationToken
    ) => _scheduler.ExecuteAsync(_prepareTypeHierarchy, parameters, cancellationToken);

    [JsonRpcMethod(SupertypesHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<HierarchyItem[]?> SupertypesAsync(HierarchyItemParams? parameters, CancellationToken cancellationToken) =>
        _scheduler.ExecuteAsync(_supertypes, parameters, cancellationToken);

    [JsonRpcMethod(SubtypesHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<HierarchyItem[]?> SubtypesAsync(HierarchyItemParams? parameters, CancellationToken cancellationToken) =>
        _scheduler.ExecuteAsync(_subtypes, parameters, cancellationToken);

    [JsonRpcMethod(ProjectInfosHandler.MethodName, UseSingleObjectParameterDeserialization = true)]
    public Task<ProjectInfos> ProjectInfosAsync(
        ProjectInfosParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_projectInfos, parameters, cancellationToken);
    }

    [JsonRpcMethod(
        DocumentAssemblyHandler.MethodName,
        UseSingleObjectParameterDeserialization = true
    )]
    public Task<DocumentAssembly> AssemblyAsync(
        DocumentAssemblyParams? parameters,
        CancellationToken cancellationToken
    )
    {
        return _scheduler.ExecuteAsync(_assembly, parameters, cancellationToken);
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
