export interface ScannableIterator<T> extends Iterator<T> {
    readonly done: boolean;
    readonly current: IteratorResult<T>;
    previous(): IteratorResult<T>;
    peek(value?: any): IteratorResult<T>;
}

export interface IterableScannableIterator<T>
    extends ScannableIterator<T>,
        IterableIterator<T> {}

export class Scanner<T> implements IterableScannableIterator<T> {
    private readonly _iterator: Iterator<T>;
    private readonly _safeDone: () => T;

    private readonly _left: IteratorResult<T>[] = [];
    private readonly _right: IteratorResult<T>[] = [];
    private _current: IteratorResult<T> = null;

    constructor(iterator: Iterator<T>, safeDone?: () => T) {
        this._iterator = iterator;
        this._safeDone = safeDone;
        this._right.push(this.asSafeDone(this._iterator.next()));
    }

    public get done() {
        return this._current && this._current.done;
    }

    public get current() {
        return this._current;
    }

    public previous(): IteratorResult<T> {
        if (this._left.length > 0) {
            this._right.push(this.asSafeDone(this._current));
            this._current = this._left.pop();
        }

        return this._current;
    }
    public peek(): IteratorResult<T> {
        if (this._right.length === 0) {
            this._right.push(this.asSafeDone(this._iterator.next()));
        }

        return this._right[this._right.length - 1];
    }

    public peekPrevious(): IteratorResult<T> {
        if (this._left.length === 0) {
            return null;
        }

        return this._left[this._left.length - 1];
    }

    public next() {
        if (this._current && this._current.done) {
            return this._current;
        }

        this._left.push(this._current);

        if (this._right.length > 0) {
            this._current = this._right.pop();
        } else {
            this._current = this.asSafeDone(this._iterator.next());
        }

        return this._current;
    }

    public skipWhile(fn: (value: T) => boolean) {
        let upcoming = this.peek();
        while (!upcoming.done && fn(upcoming.value)) {
            this.next();
            upcoming = this.peek();
        }
    }

    public *nextWhile(fn: (value: T) => boolean) {
        if (fn(this.current.value)) {
            yield this.current;
        }

        let upcoming = this.peek();
        while (!upcoming.done && fn(upcoming.value)) {
            yield this.next();
            upcoming = this.peek();
        }
    }

    public doLookahead<TReturn>(lookaheadFn: () => TReturn) {
        const currentLeftStackLength = this._left.length;

        try {
            return lookaheadFn();
        } finally {
            while (this._left.length !== currentLeftStackLength) {
                if (this._left.length > currentLeftStackLength) {
                    this.previous();
                } else {
                    this.next();
                }
            }
        }
    }

    // tslint:disable-next-line:function-name
    public [Symbol.iterator](): IterableIterator<T> {
        return this;
    }

    private asSafeDone(result: IteratorResult<T>) {
        if (this._safeDone && result.done) {
            result.value = this._safeDone();
        }

        return result;
    }
}
