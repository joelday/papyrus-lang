using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Workspaces;

namespace Papyrus.LanguageServer;

internal static class LspDiagnostics
{
    internal static Diagnostic[] ToLsp(
        ImmutableArray<Papyrus.CodeAnalysis.Diagnostic> diagnostics,
        TextLineMap lineMap
    )
    {
        var result = new Diagnostic[diagnostics.Length];
        for (var i = 0; i < diagnostics.Length; i++)
        {
            var diagnostic = diagnostics[i];
            result[i] = new Diagnostic
            {
                Range = DocumentSymbolHandler.ToRange(lineMap, diagnostic.Span),
                Severity = ToLspSeverity(diagnostic.Severity),
                Code = diagnostic.Id,
                Source = "papyrus",
                Message = diagnostic.GetMessage(),
            };
        }

        return result;
    }

    private static int ToLspSeverity(Papyrus.CodeAnalysis.DiagnosticSeverity severity)
    {
        return severity switch
        {
            Papyrus.CodeAnalysis.DiagnosticSeverity.Error => 1,
            Papyrus.CodeAnalysis.DiagnosticSeverity.Warning => 2,
            Papyrus.CodeAnalysis.DiagnosticSeverity.Info => 3,
            _ => 4,
        };
    }
}

/// <summary>
/// Pull diagnostics (<c>textDocument/diagnostic</c>, LSP 3.17). Complements the
/// server-initiated push in <see cref="LanguageServerRpc"/>, which republishes
/// on every workspace change (edits and out-of-editor file-watcher updates).
/// </summary>
internal sealed class DiagnosticHandler : ILspHandler<DiagnosticParams?, DocumentDiagnosticReport>
{
    internal const string MethodName = "textDocument/diagnostic";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<DocumentDiagnosticReport> HandleAsync(
        DiagnosticParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            !LspDocuments.TryGetDocumentId(snapshot, p?.TextDocument, out var documentId)
            || !snapshot.Solution.TryGetSyntaxTree(documentId, out var tree)
        )
        {
            return Task.FromResult(new DocumentDiagnosticReport());
        }

        var diagnostics = snapshot.Solution.GetDiagnostics(documentId, ct);
        var lineMap = tree.GetText().Lines;
        return Task.FromResult(
            new DocumentDiagnosticReport { Items = LspDiagnostics.ToLsp(diagnostics, lineMap) }
        );
    }
}

/// <summary>
/// Sends server-initiated notifications. Kept as a thin abstraction over
/// <see cref="StreamJsonRpc.JsonRpc"/> so handlers/tests never depend on the
/// transport directly.
/// </summary>
internal interface ILspNotifier
{
    Task NotifyAsync(string method, object? parameters, CancellationToken cancellationToken);
}

internal sealed class JsonRpcNotifier : ILspNotifier
{
    private readonly StreamJsonRpc.JsonRpc _jsonRpc;

    internal JsonRpcNotifier(StreamJsonRpc.JsonRpc jsonRpc)
    {
        ArgumentNullException.ThrowIfNull(jsonRpc);
        _jsonRpc = jsonRpc;
    }

    public Task NotifyAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return NotifyWithParameterObjectAsync(method, parameters);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Notification payloads go through LspJsonContext."
    )]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "Notification payloads go through LspJsonContext."
    )]
    private Task NotifyWithParameterObjectAsync(string method, object? parameters)
    {
        return _jsonRpc.NotifyWithParameterObjectAsync(method, parameters);
    }
}
