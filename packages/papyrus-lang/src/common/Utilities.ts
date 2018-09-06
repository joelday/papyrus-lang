import { Deferred } from './Deferred';
import { Range } from './Range';
import { IterableScannableIterator, Scanner } from './Scanner';

export function delayAsync(durationMs: number = 0) {
    const deferred = new Deferred<void>();
    setTimeout(() => deferred.resolve(), durationMs);
    return deferred.promise;
}

export function createIterator<T>(iterable: Iterable<T>) {
    return iterable[Symbol.iterator]();
}

export function* iterateWhere<T>(
    iterable: Iterable<T>,
    filter: (element: T) => boolean
) {
    for (const element of iterable) {
        if (filter(element)) {
            yield element;
        }
    }
}

export function* iterateMany<T>(
    iterableOfIterables:
        | Iterable<Iterable<T>>
        | Iterable<T[]>
        | Iterable<T>[]
        | T[][]
): IterableIterator<T> {
    for (const iterable of iterableOfIterables) {
        for (const element of iterable) {
            yield element;
        }
    }
}

export function lastOfIterable<T>(iterable: Iterable<T>) {
    let value: T = null;

    for (const current of iterable) {
        value = current;
    }

    return value;
}

export function* mapIterable<T, U>(
    iterable: Iterable<T>,
    map: (value: T) => U
) {
    for (const element of iterable) {
        yield map(element);
    }
}

export function clamp(value: number, min: number, max: number) {
    return Math.min(Math.max(value, min), max);
}

export function asScannableIterator<T>(
    iterator: Iterator<T>
): IterableScannableIterator<T> {
    if (iterator instanceof Scanner) {
        return iterator;
    }

    return new Scanner<T>(iterator);
}

export function* iterateNumbersForStartAndCount(start: number, count: number) {
    let value = start;
    const end = start + count;

    while (value < end) {
        yield value;
        value++;
    }
}

export function iterateNumbersForRange(range: Range) {
    return iterateNumbersForStartAndCount(range.start, range.start + range.end);
}

export function* iteratorOf<T>(value: T, count: number) {
    for (const _num of iterateNumbersForStartAndCount(0, count)) {
        yield value;
    }
}

export function stringOf(value: string, count: number) {
    return Array.from(iteratorOf(value, count)).join('');
}

export function flushIterator(iterator: Iterator<any>) {
    while (!iterator.next().done) {}
}
