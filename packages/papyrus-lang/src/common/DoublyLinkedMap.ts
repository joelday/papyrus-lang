export class DoublyLinkedMap<TKey, TValue> {
    get keys() {
        return this._keyValue.keys();
    }

    get values() {
        return this._valueKey.keys();
    }
    public static fromMap<TKey, TValue>(
        map: ReadonlyMap<TKey, TValue>,
        transformKey: (key: TKey) => TKey
    ) {
        const newMap = new DoublyLinkedMap<TKey, TValue>();

        for (const entry of map.entries()) {
            const transformedKey = transformKey
                ? transformKey(entry[0])
                : entry[0];
            newMap.set(transformedKey, entry[1]);
        }

        return newMap;
    }

    private readonly _keyValue: Map<TKey, TValue> = new Map();
    private readonly _valueKey: Map<TValue, TKey> = new Map();

    public getValueFromKey(key: TKey): TValue {
        return this._keyValue.get(key);
    }

    public getKeyFromValue(value: TValue): TKey {
        return this._valueKey.get(value);
    }

    public hasValue(value: TValue) {
        return this._valueKey.has(value);
    }

    public hasKey(key: TKey) {
        return this._keyValue.has(key);
    }

    public set(key: TKey, value: TValue) {
        this._keyValue.set(key, value);
        this._valueKey.set(value, key);
    }

    public deleteByKey(key: TKey) {
        if (this._keyValue.has(key)) {
            const value = this._keyValue.get(key);
            this._valueKey.delete(value);
            this._keyValue.delete(key);
        }
    }

    public deleteByValue(value: TValue) {
        if (this._valueKey.has(value)) {
            const key = this._valueKey.get(value);
            this._keyValue.delete(key);
            this._valueKey.delete(value);
        }
    }

    public clear() {
        this._keyValue.clear();
        this._valueKey.clear();
    }
}
