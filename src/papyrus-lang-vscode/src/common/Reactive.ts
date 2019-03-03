import { OperatorFunction, using, Unsubscribable, NEVER, concat } from 'rxjs';
import { Disposable } from 'vscode';
import { share, switchMap, distinctUntilChanged } from 'rxjs/operators';

interface DisposableUnsubscribableProxy<T extends Disposable> extends Unsubscribable {
    value: T;
}

export function disposableToUnsubscribableProxy<T extends Disposable>(disposable: T) {
    return {
        value: disposable,
        unsubscribe: () => {
            if (disposable) {
                disposable.dispose();
            }
        },
    };
}

export function asyncDisposable<T, R extends Disposable>(
    factory: (value: T) => R,
    start: (created: R) => Promise<void>,
    compare?: (x: T, y: T) => boolean
): OperatorFunction<T, R> {
    return (ob) =>
        ob.pipe(
            distinctUntilChanged(compare),
            switchMap((value: T) =>
                using<R>(
                    () => disposableToUnsubscribableProxy(factory(value)),
                    (instance: DisposableUnsubscribableProxy<R>) =>
                        concat(
                            (async () => {
                                if (instance.value) {
                                    await start(instance.value);
                                }
                                return instance.value;
                            })(),
                            NEVER
                        )
                )
            ),
            share()
        );
}
