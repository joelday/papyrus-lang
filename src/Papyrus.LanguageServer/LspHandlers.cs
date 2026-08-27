using System;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Workspaces;

namespace Papyrus.LanguageServer;

internal readonly struct LspVoid
{
    internal static LspVoid Instance => default;
}

internal sealed class DidOpenHandler : ILspHandler<DidOpenTextDocumentParams, LspVoid>
{
    internal const string MethodName = "textDocument/didOpen";

    private readonly PapyrusWorkspace _workspace;

    internal DidOpenHandler(PapyrusWorkspace workspace)
    {
        _workspace = workspace;
    }

    public string Method => MethodName;

    public bool MutatesSolution => true;

    public Task<LspVoid> HandleAsync(
        DidOpenTextDocumentParams p,
        PapyrusSolution solution,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var document = p.TextDocument;
        if (document is not null
            && LspUris.TryGetFilePath(document.Uri, out var path))
        {
            var text = SourceText.From(document.Text ?? "");
            TextDocumentSync.TryApply(
                _workspace,
                path,
                id => new WorkspaceChange.OpenDocument(id, text));
        }

        return Task.FromResult(LspVoid.Instance);
    }
}

internal sealed class DidChangeHandler : ILspHandler<DidChangeTextDocumentParams, LspVoid>
{
    internal const string MethodName = "textDocument/didChange";

    private readonly PapyrusWorkspace _workspace;

    internal DidChangeHandler(PapyrusWorkspace workspace)
    {
        _workspace = workspace;
    }

    public string Method => MethodName;

    public bool MutatesSolution => true;

    public Task<LspVoid> HandleAsync(
        DidChangeTextDocumentParams p,
        PapyrusSolution solution,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (LspUris.TryGetFilePath(p.TextDocument?.Uri, out var path)
            && TextDocumentSync.TryGetFullText(p, out var fullText))
        {
            var text = SourceText.From(fullText);
            TextDocumentSync.TryApply(
                _workspace,
                path,
                id => new WorkspaceChange.ChangeDocument(id, text));
        }

        return Task.FromResult(LspVoid.Instance);
    }
}

internal sealed class DidCloseHandler : ILspHandler<DidCloseTextDocumentParams, LspVoid>
{
    internal const string MethodName = "textDocument/didClose";

    private readonly PapyrusWorkspace _workspace;

    internal DidCloseHandler(PapyrusWorkspace workspace)
    {
        _workspace = workspace;
    }

    public string Method => MethodName;

    public bool MutatesSolution => true;

    public Task<LspVoid> HandleAsync(
        DidCloseTextDocumentParams p,
        PapyrusSolution solution,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (LspUris.TryGetFilePath(p.TextDocument?.Uri, out var path))
        {
            TextDocumentSync.TryApply(
                _workspace,
                path,
                id => new WorkspaceChange.CloseDocument(id));
        }

        return Task.FromResult(LspVoid.Instance);
    }
}

internal sealed class SnapshotHandler : ILspHandler<SnapshotParams?, SnapshotResult>
{
    internal const string MethodName = "papyrus/snapshot";

    private readonly LspRequestScheduler _scheduler;

    internal SnapshotHandler(LspRequestScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<SnapshotResult> HandleAsync(
        SnapshotParams? p,
        PapyrusSolution solution,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(
            new SnapshotResult
            {
                Generation = _scheduler.Generation,
                OpenDocumentCount = solution.OpenDocumentIds.Length,
                ProjectCount = solution.ProjectIds.Length,
            });
    }
}

internal static class LspUris
{
    internal static bool TryGetFilePath(string? uri, out string path)
    {
        path = "";
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || !parsed.IsFile)
        {
            return false;
        }

        path = parsed.LocalPath;
        return !string.IsNullOrWhiteSpace(path);
    }
}

internal static class TextDocumentSync
{
    internal static void TryApply(
        PapyrusWorkspace workspace,
        string path,
        Func<DocumentId, WorkspaceChange> create)
    {
        var projectIds = workspace.CurrentSolution.ProjectIds;
        for (var i = 0; i < projectIds.Length; i++)
        {
            var id = DocumentId.Create(projectIds[i], path);
            workspace.ApplyChange(create(id));
        }
    }

    internal static bool TryGetFullText(DidChangeTextDocumentParams parameters, out string text)
    {
        text = "";
        var changes = parameters.ContentChanges;
        if (changes is null || changes.Length == 0)
        {
            return false;
        }

        var last = changes[^1];
        if (last.Range is not null || last.Text is null)
        {
            return false;
        }

        text = last.Text;
        return true;
    }
}
