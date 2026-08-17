using System;
using System.Threading;
using System.Threading.Tasks;

namespace Papyrus.CodeAnalysis.Symbols;

internal sealed class CompletionSlot
{
    internal const int NotStarted = 0;
    internal const int InProgress = 1;
    internal const int Complete = 2;
    internal const int Error = 3;

    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _state;
    private int _ownerThreadId;

    internal int State => Volatile.Read(ref _state);

    internal int OwnerThreadId => Volatile.Read(ref _ownerThreadId);

    internal bool IsFinished
    {
        get
        {
            var state = State;
            return state == Complete || state == Error;
        }
    }

    internal bool TryStart()
    {
        if (Interlocked.CompareExchange(ref _state, InProgress, NotStarted) != NotStarted)
        {
            return false;
        }

        Volatile.Write(ref _ownerThreadId, Environment.CurrentManagedThreadId);
        return true;
    }

    internal void Finish(bool error)
    {
        Volatile.Write(ref _ownerThreadId, 0);
        Interlocked.Exchange(ref _state, error ? Error : Complete);
        _tcs.TrySetResult();
    }

    internal void Wait(CancellationToken cancellationToken)
    {
        if (IsFinished)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        // Same-thread wait would deadlock the part's Task; cycle handling must return first.
        if (OwnerThreadId == Environment.CurrentManagedThreadId)
        {
            return;
        }

        _tcs.Task.Wait(cancellationToken);
    }
}
