function fin<T, U = T>(promise: Promise<T>, func: (resultOrError: any) => U) {
    return promise.then(func, func);
}

export class SinglePendingAsync<T> {
    private _currentPromise: Promise<T>;
    private _pendingExecutor: { executor: () => Promise<T>; onSkipped?: () => void };

    execute(executor: () => Promise<T>, onSkipped?: () => void) {
        if (this._currentPromise) {
            if (this._pendingExecutor && this._pendingExecutor.onSkipped) {
                this._pendingExecutor.onSkipped();
            }

            this._pendingExecutor = { executor, onSkipped };

            return fin(this._currentPromise, (result: T) => {
                if (this._pendingExecutor) {
                    const pendingExecutor = this._pendingExecutor;
                    this._pendingExecutor = null;

                    return this.execute(pendingExecutor.executor);
                }

                return result;
            });
        }

        this._currentPromise = executor();

        fin(this._currentPromise, () => {
            this._currentPromise = null;
        });

        return this._currentPromise;
    }

    dispose() {
        if (this._pendingExecutor && this._pendingExecutor.onSkipped) {
            this._pendingExecutor.onSkipped();
        }

        this._pendingExecutor = null;
    }
}
