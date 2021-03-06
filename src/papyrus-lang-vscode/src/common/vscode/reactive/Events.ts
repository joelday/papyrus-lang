import { Event } from 'vscode';
import { Observable } from 'rxjs';
import { shareReplay } from 'rxjs/operators';

export function eventToValueObservable<TEvent, TValue = TEvent>(
    event: Event<TEvent>,
    getCurrent: () => TValue,
    map: (event: TEvent) => TValue = (e) => (e as any) as TValue
) {
    return new Observable<TValue>((s) => {
        s.next(getCurrent());

        const disposable = event((value) => {
            const newValue = map(value);

            if (typeof newValue !== 'undefined') {
                s.next(newValue);
            }
        });

        return {
            unsubscribe: () => disposable.dispose(),
        };
    }).pipe(shareReplay(1));
}
