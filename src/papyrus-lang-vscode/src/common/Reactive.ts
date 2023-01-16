import { OperatorFunction, using, NEVER, concat, from } from 'rxjs';
import { Disposable } from 'vscode';
import { switchMap, distinctUntilChanged, shareReplay } from 'rxjs/operators';

export class UnsubscribableDisposableProxy<T extends Disposable> {
    private _disposable: T | undefined;

    get disposable() {
        return this._disposable;
    }

    set disposable(disposable: T | undefined) {
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
                    (proxy) => {
                        const instance = factory(value);
                        (proxy as UnsubscribableDisposableProxy<R>).disposable = instance;

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
