using System;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.LanguageServer;
using Papyrus.Workspaces;
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
        private readonly Func<TParams, PapyrusSolution, CancellationToken, Task<TResult>> _handle;

        public DelegateHandler(
            string method,
            bool mutatesSolution,
            Func<TParams, PapyrusSolution, CancellationToken, Task<TResult>> handle)
        {
            Method = method;
            MutatesSolution = mutatesSolution;
            _handle = handle;
        }

        public string Method { get; }

        public bool MutatesSolution { get; }

        public Task<TResult> HandleAsync(TParams p, PapyrusSolution solution, CancellationToken ct)
        {
            return _handle(p, solution, ct);
        }
    }
}
