import { ScriptSymbol, Symbol, SymbolKind } from '../symbols/Symbol';
import { Program } from './Program';
import { ScriptFile } from './ScriptFile';
import { ScriptReferenceValue } from './ScriptReferenceValue';

export function symbolCanHaveExternalReferences(symbol: Symbol) {
    switch (symbol.kind) {
        case SymbolKind.Script:
        case SymbolKind.CustomEvent:
        case SymbolKind.Struct:
        case SymbolKind.Property:
        case SymbolKind.Function:
            return true;
        default:
            return false;
    }
}

export class ReferenceResolver {
    private readonly _program: Program;
    private readonly _references: Map<
        string,
        Map<string, ScriptReferenceValue<boolean>>
    >;

    constructor(program: Program) {
        this._program = program;
        this._references = new Map();
    }

    public getDirectReferencingScriptFiles(scriptName: string) {
        return Array.from(this.iterateReferencingScriptNames(scriptName)).map(
            (referencingScriptName) =>
                this._program.getScriptFileByName(referencingScriptName)
        );
    }

    private *iterateReferencingScriptNames(scriptName: string) {
        const lowerCaseName = scriptName.toLowerCase();

        const scriptFile = this._program.getScriptFileByName(scriptName);

        if (!scriptFile) {
            this._references.delete(lowerCaseName);
            return null;
        }

        if (!this._references.has(lowerCaseName)) {
            this._references.set(lowerCaseName, new Map());
        }

        const referenceMap = this._references.get(lowerCaseName);

        for (const externalScriptName of this._program.scriptNames) {
            const lowerCaseExternalScriptName = externalScriptName.toLowerCase();

            if (
                referenceMap.has(lowerCaseExternalScriptName) &&
                !referenceMap.get(lowerCaseExternalScriptName).value
            ) {
                referenceMap.delete(lowerCaseExternalScriptName);
            } else {
                const newReference = this._program
                    .getScriptFileByName(lowerCaseExternalScriptName)
                    .maybeCreateVersionDerivedValue((externalScriptFile) => {
                        return externalScriptFile.scriptNode.scriptNode.identifiers.has(
                            lowerCaseName
                        );
                    });

                if (newReference) {
                    referenceMap.set(lowerCaseExternalScriptName, newReference);
                    yield lowerCaseExternalScriptName;
                }
            }
        }
    }
}
