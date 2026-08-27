using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.Workspaces;

namespace Papyrus.LanguageServer;

internal sealed class LspRequestScheduler : IDisposable
{
    private readonly PapyrusWorkspace _workspace;
    private readonly SemaphoreSlim _mutate = new(1, 1);
    private readonly object _gate = new();
    private readonly List<CancellationTokenSource> _retiredNonMutating = new();

    private CancellationTokenSource _nonMutatingCts = new();
    private int _generation;
    private bool _disposed;

    internal LspRequestScheduler()
        : this(new PapyrusWorkspace(TimeProvider.System, enableFileWatching: false))
    {
    }

    internal LspRequestScheduler(PapyrusWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _workspace = workspace;
    }

    internal PapyrusWorkspace Workspace => _workspace;

    internal int Generation => Volatile.Read(ref _generation);

    internal async Task<TResult> ExecuteAsync<TParams, TResult>(
        ILspHandler<TParams, TResult> handler,
        TParams parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ThrowIfDisposed();

        if (handler.MutatesSolution)
        {
            return await ExecuteMutatingAsync(handler, parameters, cancellationToken).ConfigureAwait(false);
        }

        return await ExecuteNonMutatingAsync(handler, parameters, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _nonMutatingCts.Cancel();
            _retiredNonMutating.Add(_nonMutatingCts);
            for (var i = 0; i < _retiredNonMutating.Count; i++)
            {
                _retiredNonMutating[i].Dispose();
            }

            _retiredNonMutating.Clear();
            _workspace.Dispose();
        }

        _mutate.Dispose();
    }

    private async Task<TResult> ExecuteMutatingAsync<TParams, TResult>(
        ILspHandler<TParams, TResult> handler,
        TParams parameters,
        CancellationToken cancellationToken)
    {
        // In-flight reads are on a snapshot that is about to be replaced.
        CancelInFlightNonMutating();
        await _mutate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var solution = _workspace.CurrentSolution;
            var result = await handler.HandleAsync(parameters, solution, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _generation);
            return result;
        }
        finally
        {
            _mutate.Release();
        }
    }

    private async Task<TResult> ExecuteNonMutatingAsync<TParams, TResult>(
        ILspHandler<TParams, TResult> handler,
        TParams parameters,
        CancellationToken cancellationToken)
    {
        CancellationToken nonMutatingToken;
        lock (_gate)
        {
            ThrowIfDisposed_NoLock();
            nonMutatingToken = _nonMutatingCts.Token;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, nonMutatingToken);

        // Wait only long enough to observe a stable snapshot; run the handler
        // without holding the mutate lock so reads can overlap.
        await _mutate.WaitAsync(linked.Token).ConfigureAwait(false);
        PapyrusSolution solution;
        try
        {
            ThrowIfDisposed();
            solution = _workspace.CurrentSolution;
        }
        finally
        {
            _mutate.Release();
        }

        return await handler.HandleAsync(parameters, solution, linked.Token).ConfigureAwait(false);
    }

    private void CancelInFlightNonMutating()
    {
        lock (_gate)
        {
            ThrowIfDisposed_NoLock();
            _nonMutatingCts.Cancel();
            _retiredNonMutating.Add(_nonMutatingCts);
            _nonMutatingCts = new CancellationTokenSource();
        }
    }

    private void ThrowIfDisposed()
    {
        lock (_gate)
        {
            ThrowIfDisposed_NoLock();
        }
    }

    private void ThrowIfDisposed_NoLock()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
