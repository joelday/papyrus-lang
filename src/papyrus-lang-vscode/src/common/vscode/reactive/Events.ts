import { Event } from 'vscode';
import { Observable } from 'rxjs';
import { shareReplay } from 'rxjs/operators';

export function eventToValueObservable<TEvent, TValue = TEvent>(
    event: Event<TEvent | undefined>,
    getCurrent: () => TValue,
    map: ((event: TEvent) => TValue | undefined) | undefined,
    emitUndefined: true
): Observable<TValue | undefined>;

export function eventToValueObservable<TEvent, TValue = TEvent>(
    event: Event<TEvent>,
    getCurrent: () => TValue,
    map: ((event: TEvent) => TValue) | undefined,
    emitUndefined: false
): Observable<Exclude<TValue, undefined>>;

export function eventToValueObservable<TEvent, TValue = TEvent>(
    event: Event<TEvent>,
    getCurrent: () => TValue,
    map?: (event: TEvent) => TValue | undefined,
    emitUndefined?: boolean
): Observable<Exclude<TValue, undefined>>;

export function eventToValueObservable<TEvent, TValue = TEvent>(
    event: Event<TEvent>,
    getCurrent: () => TValue,
    map: (event: TEvent) => TValue = (e) => e as unknown as TValue,
    emitUndefined: boolean = false
) {
    return new Observable<TValue>((s) => {
        const initialValue = getCurrent();
        if (!(typeof initialValue === 'undefined' && !emitUndefined)) {
            s.next(initialValue);
        }

        const disposable = event((value) => {
            const newValue = map(value);

            if (typeof newValue === 'undefined' && !emitUndefined) {
                return;
            }

            s.next(newValue);
        });

        return {
            unsubscribe: () => disposable.dispose(),
        };
    }).pipe(shareReplay(1));
}
