using System;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.LanguageServer;
using Xunit;

namespace Papyrus.LanguageServer.Tests;

public sealed class LspRequestSchedulerTests
{
    [Fact]
    public async Task Mutating_requests_do_not_overlap()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var scheduler = new LspRequestScheduler();
        var current = 0;
        var max = 0;
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var handler = new DelegateHandler<int, int>(
            "test/mutate",
            mutatesSolution: true,
            async (value, _, cancellationToken) =>
            {
                var running = Interlocked.Increment(ref current);
                var snapshot = max;
                while (running > snapshot)
                {
                    snapshot = Interlocked.CompareExchange(ref max, running, snapshot);
                }

                if (value == 1)
                {
                    firstEntered.TrySetResult();
                    await releaseFirst.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                Interlocked.Decrement(ref current);
                return value;
            });

        var first = scheduler.ExecuteAsync(handler, 1, timeout.Token);
        await firstEntered.Task.WaitAsync(timeout.Token);
        var second = scheduler.ExecuteAsync(handler, 2, timeout.Token);

        await Task.Delay(50, timeout.Token);
        Assert.Equal(1, Volatile.Read(ref current));
        Assert.False(second.IsCompleted);

        releaseFirst.TrySetResult();
        Assert.Equal(1, await first.WaitAsync(timeout.Token));
        Assert.Equal(2, await second.WaitAsync(timeout.Token));
        Assert.Equal(1, Volatile.Read(ref max));
        Assert.Equal(2, scheduler.Generation);
    }

    [Fact]
    public async Task Non_mutating_is_cancelled_when_mutating_arrives()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var scheduler = new LspRequestScheduler();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var read = new DelegateHandler<int, int>(
            "test/read",
            mutatesSolution: false,
            async (_, _, cancellationToken) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return 1;
            });
        var mutate = new DelegateHandler<int, int>(
            "test/mutate",
            mutatesSolution: true,
            (_, _, _) => Task.FromResult(2));

        var readTask = scheduler.ExecuteAsync(read, 1, CancellationToken.None);
        await started.Task.WaitAsync(timeout.Token);
        Assert.Equal(2, await scheduler.ExecuteAsync(mutate, 2, timeout.Token).WaitAsync(timeout.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await readTask.WaitAsync(timeout.Token));
        Assert.Equal(1, scheduler.Generation);
    }

    [Fact]
    public async Task Non_mutating_started_after_mutate_cancel_does_not_observe_old_snapshot()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var scheduler = new LspRequestScheduler();
        var cancelledEpoch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMutateWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        scheduler.BeforeMutateWait = async cancellationToken =>
        {
            cancelledEpoch.TrySetResult();
            await releaseMutateWait.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        };

        var mutate = new DelegateHandler<int, int>(
            "test/mutate",
            mutatesSolution: true,
            (_, _, _) => Task.FromResult(2));
        var read = new DelegateHandler<int, int>(
            "test/read",
            mutatesSolution: false,
            (_, snapshot, _) => Task.FromResult(snapshot.Generation));

        var mutateTask = scheduler.ExecuteAsync(mutate, 2, timeout.Token);
        await cancelledEpoch.Task.WaitAsync(timeout.Token);

        var readTask = scheduler.ExecuteAsync(read, 1, CancellationToken.None);
        await Task.Delay(50, timeout.Token);

        releaseMutateWait.TrySetResult();
        Assert.Equal(2, await mutateTask.WaitAsync(timeout.Token));
        Assert.Equal(1, scheduler.Generation);

        if (readTask.IsCompleted)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await readTask.WaitAsync(timeout.Token));
        }
        else
        {
            Assert.Equal(1, await readTask.WaitAsync(timeout.Token));
        }
    }

    [Fact]
    public async Task Non_mutating_started_during_mutating_handler_waits_for_new_snapshot()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var scheduler = new LspRequestScheduler();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var mutate = new DelegateHandler<int, int>(
            "test/mutate",
            mutatesSolution: true,
            async (_, _, cancellationToken) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return 2;
            });
        var read = new DelegateHandler<int, int>(
            "test/read",
            mutatesSolution: false,
            (_, snapshot, _) => Task.FromResult(snapshot.Generation));

        var mutateTask = scheduler.ExecuteAsync(mutate, 2, timeout.Token);
        await entered.Task.WaitAsync(timeout.Token);

        var readTask = scheduler.ExecuteAsync(read, 1, timeout.Token);
        await Task.Delay(50, timeout.Token);
        Assert.False(readTask.IsCompleted);

        release.TrySetResult();
        Assert.Equal(2, await mutateTask.WaitAsync(timeout.Token));
        Assert.Equal(1, await readTask.WaitAsync(timeout.Token));
    }

    [Fact]
    public async Task Read_issued_while_an_edit_is_queued_sees_the_edit_instead_of_being_cancelled()
    {
        // e.g. didOpen queued behind project loading, then a hover: the hover must answer
        // from the opened document rather than fail with RequestCancelled.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var scheduler = new LspRequestScheduler();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var slowMutate = new DelegateHandler<int, int>(
            "test/slow",
            mutatesSolution: true,
            async (_, _, cancellationToken) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return 1;
            });
        var mutate = new DelegateHandler<int, int>(
            "test/mutate",
            mutatesSolution: true,
            (_, _, _) => Task.FromResult(2));
        var read = new DelegateHandler<int, int>(
            "test/read",
            mutatesSolution: false,
            (_, snapshot, _) => Task.FromResult(snapshot.Generation));

        var first = scheduler.ExecuteAsync(slowMutate, 1, timeout.Token);
        await entered.Task.WaitAsync(timeout.Token);
        var second = scheduler.ExecuteAsync(mutate, 2, timeout.Token);
        var readTask = scheduler.ExecuteAsync(read, 0, timeout.Token);

        release.TrySetResult();
        await first.WaitAsync(timeout.Token);
        await second.WaitAsync(timeout.Token);
        Assert.Equal(2, await readTask.WaitAsync(timeout.Token));
    }

    [Fact]
    public async Task Read_succeeds_after_mutating_wait_is_cancelled()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var scheduler = new LspRequestScheduler();
        using var mutateCts = new CancellationTokenSource();
        var promptCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        scheduler.BeforeMutateWait = async _ =>
        {
            promptCancelled.TrySetResult();
            await Task.Delay(Timeout.Infinite, mutateCts.Token).ConfigureAwait(false);
        };

        var mutate = new DelegateHandler<int, int>(
            "test/mutate",
            mutatesSolution: true,
            (_, _, _) => Task.FromResult(2));
        var read = new DelegateHandler<int, int>(
            "test/read",
            mutatesSolution: false,
            (_, snapshot, _) => Task.FromResult(snapshot.Generation));

        var mutateTask = scheduler.ExecuteAsync(mutate, 2, mutateCts.Token);
        await promptCancelled.Task.WaitAsync(timeout.Token);
        mutateCts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await mutateTask.WaitAsync(timeout.Token));
        Assert.Equal(0, scheduler.Generation);
        Assert.Equal(0, await scheduler.ExecuteAsync(read, 0, timeout.Token).WaitAsync(timeout.Token));
    }

    [Fact]
    public async Task Queued_mutator_cancelled_during_wait_does_not_leave_reads_stuck()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var scheduler = new LspRequestScheduler();
        using var mutateCts = new CancellationTokenSource();
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queuedAtWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var hold = new DelegateHandler<int, int>(
            "test/mutate-hold",
            mutatesSolution: true,
            async (_, _, cancellationToken) =>
            {
                holding.TrySetResult();
                await releaseHold.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return 1;
            });
        var queued = new DelegateHandler<int, int>(
            "test/mutate-queued",
            mutatesSolution: true,
            (_, _, _) => Task.FromResult(2));
        var read = new DelegateHandler<int, int>(
            "test/read",
            mutatesSolution: false,
            (_, snapshot, _) => Task.FromResult(snapshot.Generation));

        var holdTask = scheduler.ExecuteAsync(hold, 1, timeout.Token);
        await holding.Task.WaitAsync(timeout.Token);

        scheduler.BeforeMutateWait = _ =>
        {
            queuedAtWait.TrySetResult();
            return Task.CompletedTask;
        };

        var queuedTask = scheduler.ExecuteAsync(queued, 2, mutateCts.Token);
        await queuedAtWait.Task.WaitAsync(timeout.Token);
        await Task.Delay(50, timeout.Token);
        mutateCts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await queuedTask.WaitAsync(timeout.Token));

        releaseHold.TrySetResult();
        Assert.Equal(1, await holdTask.WaitAsync(timeout.Token));
        Assert.Equal(1, scheduler.Generation);
        Assert.Equal(1, await scheduler.ExecuteAsync(read, 0, timeout.Token).WaitAsync(timeout.Token));
    }

    [Fact]
    public async Task Non_mutating_requests_run_concurrently()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var scheduler = new LspRequestScheduler();
        var entered = 0;
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var handler = new DelegateHandler<int, int>(
            "test/read",
            mutatesSolution: false,
            async (value, _, cancellationToken) =>
            {
                if (Interlocked.Increment(ref entered) == 2)
                {
                    bothEntered.TrySetResult();
                }

                await bothEntered.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return value;
            });

        var first = scheduler.ExecuteAsync(handler, 1, timeout.Token);
        var second = scheduler.ExecuteAsync(handler, 2, timeout.Token);
        await bothEntered.Task.WaitAsync(timeout.Token);
        Assert.Equal(new[] { 1, 2 }, new[]
        {
            await first.WaitAsync(timeout.Token),
            await second.WaitAsync(timeout.Token),
        });
    }

    private sealed class DelegateHandler<TParams, TResult> : ILspHandler<TParams, TResult>
    {
        private readonly Func<TParams, LspSnapshot, CancellationToken, Task<TResult>> _handle;

        public DelegateHandler(
            string method,
            bool mutatesSolution,
            Func<TParams, LspSnapshot, CancellationToken, Task<TResult>> handle)
        {
            Method = method;
            MutatesSolution = mutatesSolution;
            _handle = handle;
        }

        public string Method { get; }

        public bool MutatesSolution { get; }

        public Task<TResult> HandleAsync(TParams p, LspSnapshot snapshot, CancellationToken ct)
        {
            return _handle(p, snapshot, ct);
        }
    }
}
