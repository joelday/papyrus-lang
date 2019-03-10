import { OperatorFunction, using, Unsubscribable, NEVER, concat } from 'rxjs';
import { Disposable } from 'vscode';
import { share, switchMap, distinctUntilChanged } from 'rxjs/operators';

interface DisposableUnsubscribableProxy<T extends Disposable> extends Unsubscribable {
    value: T;
}

class UnsubscribableDisposableProxy {
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
    factory: (value: T) => Promise<R>,
    compare?: (x: T, y: T) => boolean
): OperatorFunction<T, R> {
    return (ob) =>
        ob.pipe(
            distinctUntilChanged(compare),
            switchMap((value: T) =>
                using<R>(
                    () => new UnsubscribableDisposableProxy(),
                    (proxy: DisposableUnsubscribableProxy<R>) =>
                        concat((async () => (proxy.value = await factory(value)))(), NEVER)
                )
            ),
            share()
        );
}
