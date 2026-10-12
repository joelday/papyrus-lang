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

    /// <summary>Sends a parameterless request to the client (e.g. a refresh).</summary>
    Task RequestAsync(string method, CancellationToken cancellationToken) => Task.CompletedTask;
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

    public Task RequestAsync(string method, CancellationToken cancellationToken) =>
        _jsonRpc.InvokeWithCancellationAsync(method, null, cancellationToken);

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

/// <summary>
/// <c>workspace/diagnostic</c>: every project's own scripts (Folders and Scripts, not
/// Imports) that are not open; open documents are pulled per document. A document whose
/// diagnostics match the client's previous result is reported <c>unchanged</c>.
/// </summary>
internal sealed class WorkspaceDiagnosticHandler : ILspHandler<WorkspaceDiagnosticParams?, WorkspaceDiagnosticReport>
{
    internal const string MethodName = "workspace/diagnostic";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<WorkspaceDiagnosticReport> HandleAsync(
        WorkspaceDiagnosticParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        var previous = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in p?.PreviousResultIds ?? [])
        {
            if (LspUris.TryGetFilePath(id.Uri, out var path))
            {
                previous[System.IO.Path.GetFullPath(path)] = id.Value;
            }
        }

        var solution = snapshot.Solution;
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new System.Collections.Generic.List<WorkspaceDocumentDiagnosticReport>();
        foreach (var projectId in solution.ProjectIds)
        {
            foreach (var documentId in solution.GetSourceDocumentIds(projectId))
            {
                ct.ThrowIfCancellationRequested();
                var path = System.IO.Path.GetFullPath(documentId.Path);
                if (!seen.Add(path)
                    || solution.IsDocumentOpen(documentId)
                    || !solution.TryGetSyntaxTree(documentId, out var tree))
                {
                    continue;
                }

                var diagnostics = LspDiagnostics.ToLsp(solution.GetDiagnostics(documentId, ct), tree.GetText().Lines);
                var resultId = ResultId(diagnostics);
                var uri = LspUris.ToUri(path);
                items.Add(
                    previous.TryGetValue(path, out var last) && last == resultId
                        ? new WorkspaceDocumentDiagnosticReport { Kind = "unchanged", ResultId = resultId, Uri = uri }
                        : new WorkspaceDocumentDiagnosticReport { Kind = "full", ResultId = resultId, Uri = uri, Items = diagnostics }
                );
            }
        }

        return Task.FromResult(new WorkspaceDiagnosticReport { Items = items.ToArray() });
    }

    // Identifies the report's content, so an unchanged document costs no payload.
    private static string ResultId(Diagnostic[] diagnostics)
    {
        var hash = new HashCode();
        foreach (var d in diagnostics)
        {
            hash.Add(d.Code);
            hash.Add(d.Message);
            hash.Add(d.Severity);
            hash.Add(d.Range?.Start?.Line);
            hash.Add(d.Range?.Start?.Character);
            hash.Add(d.Range?.End?.Line);
            hash.Add(d.Range?.End?.Character);
        }

        return diagnostics.Length + ":" + hash.ToHashCode().ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Drives <c>workspace/diagnostic</c> as a long poll. The client re-requests two seconds
/// after every response, so while nothing has changed since the last answer the request
/// is held until something does; changes are debounced so a burst of edits costs one
/// pass; and a pass interrupted by an edit (which cancels in-flight reads) quietly
/// restarts rather than failing, since the client stops polling after a few errors.
/// </summary>
internal sealed class WorkspaceDiagnosticsPoller
{
    private readonly object _gate = new();
    private readonly TimeSpan _quietPeriod;
    private int _version;
    private int _answeredVersion = -1;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal WorkspaceDiagnosticsPoller(TimeSpan quietPeriod)
    {
        _quietPeriod = quietPeriod;
    }

    internal void NotifyChanged()
    {
        TaskCompletionSource changed;
        lock (_gate)
        {
            _version++;
            changed = _changed;
            _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        changed.TrySetResult();
    }

    internal async Task<WorkspaceDiagnosticReport> PollAsync(
        WorkspaceDiagnosticParams? p,
        Func<CancellationToken, Task<WorkspaceDiagnosticReport>> compute,
        CancellationToken ct
    )
    {
        var waitedForChange = false;
        while (true)
        {
            int version;
            Task changed;
            lock (_gate)
            {
                version = _version;
                changed = _changed.Task;
            }

            // Answered already and nothing changed since: wait for a change.
            if (version == Volatile.Read(ref _answeredVersion) && p?.PreviousResultIds is { Length: > 0 })
            {
                await changed.WaitAsync(ct).ConfigureAwait(false);
                waitedForChange = true;
                continue;
            }

            if (waitedForChange)
            {
                await Task.Delay(_quietPeriod, ct).ConfigureAwait(false);
                lock (_gate)
                {
                    if (_version != version)
                    {
                        continue;
                    }
                }
            }

            try
            {
                var report = await compute(ct).ConfigureAwait(false);
                Volatile.Write(ref _answeredVersion, version);
                return report;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // An edit cancelled the pass; start over once things are quiet.
                waitedForChange = true;
            }
        }
    }
}
