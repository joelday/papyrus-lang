export class Deferred<T> {
    private readonly _promise: Promise<T>;

    private _resolve: (value?: T | PromiseLike<T>) => void;
    private _reject: (reason?: any) => void;

    constructor() {
        // tslint:disable-next-line:promise-must-complete
        this._promise = new Promise<T>((resolve, reject) => {
            this._resolve = resolve;
            this._reject = reject;
        });
    }

    public resolve(value?: T | PromiseLike<T>) {
        this._resolve(value);
    }

    public reject(reason?: any) {
        this._reject(reason);
    }

    get promise() {
        return this._promise;
    }
}
