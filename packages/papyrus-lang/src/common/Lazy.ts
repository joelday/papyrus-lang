export class Lazy<T> {
    private readonly _factory: () => T;
    private readonly _expired: (value: T) => boolean;
    private _value: T;

    constructor(
        factory: () => T,
        expired: (value: T) => boolean = () => false
    ) {
        this._factory = factory;
        this._expired = expired;
    }

    get value() {
        if (typeof this._value === 'undefined' || this._expired(this._value)) {
            this._value = this._factory();
        }

        return this._value;
    }

    get currentValue() {
        return this._value;
    }

    public refresh() {
        this._value = this._factory();
    }
}

export class LazyTokenEqualityMemoized<T> extends Lazy<T> {
    private _token: any;

    constructor(factory: () => T, getToken: () => any) {
        super(factory, () => {
            const newToken = getToken();
            if (newToken !== this._token) {
                this._token = newToken;
                return true;
            }

            return false;
        });
    }
}
