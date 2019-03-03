import { Event } from 'vscode';
import * as rx from 'rxjs';
import * as rxop from 'rxjs/operators';

export function eventToObservable<T>(event: Event<T>) {
    return new rx.Observable<T>((s) => {
        const disposable = event((value) => {
            s.next(value);
        });

        return {
            unsubscribe: () => disposable.dispose(),
        };
    });
}

export function eventToValueObservable<TEvent, TValue>(
    event: Event<TEvent>,
    getCurrent: () => TValue,
    map: (event: TEvent) => TValue
) {
    return new rx.Observable<TValue>((s) => {
        s.next(getCurrent());

        const disposable = event((value) => {
            s.next(map(value));
        });

        return {
            unsubscribe: () => disposable.dispose(),
        };
    });
}
