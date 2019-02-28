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
    initialValue: () => TValue,
    eventToValue: (event: TEvent) => TValue
) {
    const behaviorSubject = new rx.BehaviorSubject(initialValue());
    eventToObservable(event)
        .pipe(rxop.map(eventToValue))
        .subscribe(behaviorSubject);
    return behaviorSubject;
}
