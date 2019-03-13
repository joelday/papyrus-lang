import { OperatorFunction, using, Unsubscribable, NEVER, concat, Observable, from } from 'rxjs';
import { Disposable } from 'vscode';
import { share, switchMap, distinctUntilChanged, shareReplay } from 'rxjs/operators';

class DisposableUnsubscribableProxy<T extends Unsubscribable> implements Disposable {
    private _unsubscribable: Unsubscribable;

    constructor(unsubscribable?: Unsubscribable) {
        this._unsubscribable = unsubscribable;
    }

    get unsubscribable() {
        return this._unsubscribable;
    }

    set unsubscribable(unsubscribable: Unsubscribable) {
        this._unsubscribable = unsubscribable;
    }

    dispose() {
        this._unsubscribable!.unsubscribe();
    }
}

export function asDisposable(unsubscribable: Unsubscribable): Disposable {
    return new DisposableUnsubscribableProxy(unsubscribable);
}

class UnsubscribableDisposableProxy<T extends Disposable> {
    private _disposable: Disposable;

    get disposable() {
        return this._disposable;
    }

    set disposable(disposable: Disposable) {
        this._disposable = disposable;
    }

    unsubscribe() {
        this._disposable!.dispose();
    }
}

export function asyncDisposable<T, R extends Disposable>(
    factory: (value: T) => R,
    start?: (instance: R) => Promise<void>,
    compare?: (x: T, y: T) => boolean
): OperatorFunction<T, R> {
    return (ob) =>
        ob.pipe(
            distinctUntilChanged(compare),
            switchMap((value: T) =>
                using<R>(
                    () => new UnsubscribableDisposableProxy<R>(),
                    (proxy: UnsubscribableDisposableProxy<R>) => {
                        const instance = factory(value);
                        proxy.disposable = instance;

                        return concat(
                            from([instance]),
                            (async () => {
                                if (start) {
                                    await start(instance);
                                }

                                return instance;
                            })(),
                            NEVER
                        );
                    }
                )
            ),
            shareReplay(1)
        );
}
