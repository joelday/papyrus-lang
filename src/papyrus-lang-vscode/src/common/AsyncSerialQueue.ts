/**
 * A minimal serialized-async-work queue.
 *
 * The old extension used RxJS's `switchMap`/`distinctUntilChanged`/`shareReplay`
 * combination (see the removed `asyncDisposable` operator) to guarantee that:
 *   - only one "current" async operation (e.g. starting/stopping a language
 *     client) is running at a time for a given key,
 *   - a new request supersedes any request that is still pending, and
 *   - callers always observe a consistent, race-free view of "the last thing
 *     that was asked for."
 *
 * `AsyncSerialQueue` reproduces just that guarantee without a reactive
 * streams library: `enqueue` always runs after any previously enqueued work
 * for this queue has settled, so callers get FIFO ordering and no
 * interleaving, which is enough to keep a language client's start/stop
 * lifecycle race-free when configuration changes rapidly (e.g. several
 * settings edits in a row, or a workspace-folder change during startup).
 */
export class AsyncSerialQueue {
    private _tail: Promise<unknown> = Promise.resolve();

    /**
     * Schedules `work` to run only after every previously enqueued piece of
     * work on this queue has completed (successfully or not). Returns a
     * promise that resolves/rejects with `work`'s own outcome.
     */
    enqueue<T>(work: () => Promise<T>): Promise<T> {
        const result = this._tail.then(work, work);

        // Swallow the result/error for the internal tail chain so that a
        // failed operation doesn't permanently wedge the queue for
        // subsequent callers -- each caller still observes their own
        // operation's real outcome via the returned promise.
        this._tail = result.then(
            () => undefined,
            () => undefined
        );

        return result;
    }
}
