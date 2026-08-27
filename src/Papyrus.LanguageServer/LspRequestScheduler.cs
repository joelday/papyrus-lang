using System;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.Workspaces;

namespace Papyrus.LanguageServer;

internal sealed class LspRequestScheduler : IDisposable
{
    private readonly PapyrusWorkspace _workspace;
    private readonly SemaphoreSlim _mutate = new(1, 1);
    private readonly object _gate = new();

    private CancellationTokenSource _nonMutatingCts = new();
    private CancellationTokenSource? _retiredNonMutating;
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

    /// <summary>
    /// Test hook: runs after the current read epoch is cancelled and before
    /// <c>_mutate</c> is acquired.
    /// </summary>
    internal Func<CancellationToken, Task>? BeforeMutateWait { get; set; }

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
            _nonMutatingCts.Dispose();
            _retiredNonMutating?.Dispose();
            _retiredNonMutating = null;
            _workspace.Dispose();
        }

        _mutate.Dispose();
    }

    private async Task<TResult> ExecuteMutatingAsync<TParams, TResult>(
        ILspHandler<TParams, TResult> handler,
        TParams parameters,
        CancellationToken cancellationToken)
    {
        // Cancel the live read epoch without rotating so a reader that starts
        // before we own _mutate still observes a cancelled token.
        CancelNonMutatingEpoch();
        try
        {
            if (BeforeMutateWait is { } beforeWait)
            {
                await beforeWait(cancellationToken).ConfigureAwait(false);
            }

            await _mutate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Prompt-cancel already ran; without a rotate, later reads would
            // copy the cancelled epoch until some other mutator finished.
            RestoreReadEpochIfLockFree();
            throw;
        }

        try
        {
            // Rotate only while exclusive: new reads either wait for this
            // mutation's snapshot or were already cancelled above.
            RotateNonMutatingEpoch();
            ThrowIfDisposed();
            var snapshot = CaptureSnapshot();
            var result = await handler.HandleAsync(parameters, snapshot, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _generation);
            return result;
        }
        finally
        {
            // A queued mutator may have prompt-cancelled our epoch and then
            // backed out of WaitAsync. Revive before dropping the lock.
            EnsureLiveReadEpoch();
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
        LspSnapshot snapshot;
        try
        {
            ThrowIfDisposed();
            snapshot = CaptureSnapshot();
        }
        finally
        {
            _mutate.Release();
        }

        return await handler.HandleAsync(parameters, snapshot, linked.Token).ConfigureAwait(false);
    }

    private LspSnapshot CaptureSnapshot()
    {
        return new LspSnapshot(_workspace.CurrentSolution, Volatile.Read(ref _generation));
    }

    private void CancelNonMutatingEpoch()
    {
        lock (_gate)
        {
            ThrowIfDisposed_NoLock();
            _nonMutatingCts.Cancel();
        }
    }

    private void RotateNonMutatingEpoch()
    {
        lock (_gate)
        {
            ThrowIfDisposed_NoLock();
            ReplaceNonMutatingEpoch_NoLock();
        }
    }

    private void EnsureLiveReadEpoch()
    {
        lock (_gate)
        {
            if (_disposed || !_nonMutatingCts.IsCancellationRequested)
            {
                return;
            }

            ReplaceNonMutatingEpoch_NoLock();
        }
    }

    private void RestoreReadEpochIfLockFree()
    {
        bool acquired;
        try
        {
            acquired = _mutate.Wait(0);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        if (!acquired)
        {
            return;
        }

        try
        {
            EnsureLiveReadEpoch();
        }
        finally
        {
            _mutate.Release();
        }
    }

    private void ReplaceNonMutatingEpoch_NoLock()
    {
        if (!_nonMutatingCts.IsCancellationRequested)
        {
            _nonMutatingCts.Cancel();
        }

        _retiredNonMutating?.Dispose();
        _retiredNonMutating = _nonMutatingCts;
        _nonMutatingCts = new CancellationTokenSource();
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
