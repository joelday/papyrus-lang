using System;
using System.Collections.Immutable;
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
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        var document = p.TextDocument;
        if (document is not null && LspUris.TryGetFilePath(document.Uri, out var path))
        {
            var text = SourceText.From(document.Text ?? "");
            TextDocumentSync.TryApply(
                _workspace,
                snapshot.Solution,
                path,
                id => new WorkspaceChange.OpenDocument(id, text)
            );
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
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            LspUris.TryGetFilePath(p.TextDocument?.Uri, out var path)
            && TextDocumentSync.TryGetFullText(p, out var fullText)
        )
        {
            var text = SourceText.From(fullText);
            TextDocumentSync.TryApply(
                _workspace,
                snapshot.Solution,
                path,
                id => new WorkspaceChange.ChangeDocument(id, text)
            );
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
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (LspUris.TryGetFilePath(p.TextDocument?.Uri, out var path))
        {
            TextDocumentSync.TryApply(
                _workspace,
                snapshot.Solution,
                path,
                id => new WorkspaceChange.CloseDocument(id)
            );
        }

        return Task.FromResult(LspVoid.Instance);
    }
}

internal sealed class SnapshotHandler : ILspHandler<SnapshotParams?, SnapshotResult>
{
    internal const string MethodName = "papyrus/snapshot";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<SnapshotResult> HandleAsync(
        SnapshotParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(
            new SnapshotResult
            {
                Generation = snapshot.Generation,
                OpenDocumentCount = snapshot.Solution.OpenDocumentIds.Length,
                ProjectCount = snapshot.Solution.ProjectIds.Length,
            }
        );
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

internal static class LspDocuments
{
    internal static bool TryGetSemanticModel(
        LspSnapshot snapshot,
        TextDocumentIdentifier? document,
        out Papyrus.CodeAnalysis.SemanticModel model,
        out Papyrus.CodeAnalysis.Syntax.SyntaxTree tree,
        out string path
    )
    {
        model = null!;
        tree = null!;
        path = "";
        if (
            !TryGetTree(snapshot, document, out tree)
            || !LspUris.TryGetFilePath(document?.Uri, out path)
        )
        {
            return false;
        }

        var ids = snapshot.Solution.GetDocumentIdsForPath(path);
        if (ids.IsDefaultOrEmpty || !snapshot.Solution.TryGetSyntaxTree(ids[0], out tree))
        {
            return false;
        }

        model = snapshot.Solution.GetCompilation(ids[0].ProjectId).GetSemanticModel(tree);
        return true;
    }

    /// <summary>
    /// Resolves a request's document to a syntax tree. When the path belongs
    /// to several projects, the open document (whose buffer overlays every
    /// owning project identically) is preferred; otherwise the tie is broken
    /// deterministically by project path.
    /// </summary>
    internal static bool TryGetTree(
        LspSnapshot snapshot,
        TextDocumentIdentifier? document,
        out Papyrus.CodeAnalysis.Syntax.SyntaxTree tree
    )
    {
        tree = null!;
        if (!LspUris.TryGetFilePath(document?.Uri, out var path))
        {
            return false;
        }

        var ids = snapshot.Solution.GetDocumentIdsForPath(path);
        if (ids.IsDefaultOrEmpty)
        {
            return false;
        }

        var selected = ids[0];
        for (var i = 0; i < ids.Length; i++)
        {
            if (snapshot.Solution.IsDocumentOpen(ids[i]))
            {
                selected = ids[i];
                break;
            }

            if (string.CompareOrdinal(ids[i].ProjectId.Path, selected.ProjectId.Path) < 0)
            {
                selected = ids[i];
            }
        }

        return snapshot.Solution.TryGetSyntaxTree(selected, out tree);
    }
}

internal static class TextDocumentSync
{
    internal static void TryApply(
        PapyrusWorkspace workspace,
        PapyrusSolution solution,
        string path,
        Func<DocumentId, WorkspaceChange> create
    )
    {
        var ids = solution.GetDocumentIdsForPath(path);
        if (ids.IsDefaultOrEmpty)
        {
            return;
        }

        var changes = ImmutableArray.CreateBuilder<WorkspaceChange>(ids.Length);
        for (var i = 0; i < ids.Length; i++)
        {
            changes.Add(create(ids[i]));
        }

        workspace.ApplyChanges(changes.MoveToImmutable());
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
