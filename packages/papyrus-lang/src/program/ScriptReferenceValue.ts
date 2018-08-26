import { LazyTokenEqualityMemoized } from '../common/Lazy';
import { Program } from './Program';
import { ScriptFile } from './ScriptFile';

export class ScriptReferenceValue<T> {
    private readonly _value: LazyTokenEqualityMemoized<T>;

    constructor(
        program: Program,
        scriptName: string,
        resolver: (file: ScriptFile) => T
    ) {
        this._value = new LazyTokenEqualityMemoized(
            () => {
                const file = program.getScriptFileByName(scriptName);
                return file ? resolver(file) : null;
            },
            () => {
                const file = program.getScriptFileByName(scriptName);
                return file ? file.version : null;
            }
        );
    }

    get value() {
        return this._value.value;
    }
}
