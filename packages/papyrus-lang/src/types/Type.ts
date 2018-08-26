import { Flags, ScriptNode } from '../parser/Node';
import {
    createSymbol,
    IntrinsicSymbol,
    ScriptSymbol,
    StructSymbol,
    Symbol,
    SymbolKind,
} from '../symbols/Symbol';

export type TypeTable = Map<string, Type>;

export enum TypeKind {
    Script = 'Script',
    Struct = 'Struct',
    Array = 'Array',
    Intrinsic = 'Intrinsic',
}

export interface TypeInterface<TKind extends TypeKind> {
    kind: TKind;
    name: string;
    readonly fullyQualifiedName: string;
}

export interface ScriptType extends TypeInterface<TypeKind.Script> {
    symbol: ScriptSymbol;
    structTypes: Map<string, StructType>;
}

export interface StructType extends TypeInterface<TypeKind.Struct> {
    symbol: StructSymbol;
    parentScriptType: ScriptType;
}

export interface ArrayType extends TypeInterface<TypeKind.Array> {
    symbol: ScriptSymbol;
    elementTypeName: string;
}

export enum IntrinsicKind {
    Float = 'float',
    Int = 'int',
    Bool = 'bool',
    String = 'string',
    Var = 'var',
    Void = 'void',
    Any = 'any',
}

export interface IntrinsicType extends TypeInterface<TypeKind.Intrinsic> {
    symbol: IntrinsicSymbol;
    intrinsicKind: IntrinsicKind;
}

// prettier-ignore
export type Type<T extends TypeKind = TypeKind> =
    T extends TypeKind.Script ? ScriptType :
    T extends TypeKind.Struct ? StructType :
    T extends TypeKind.Array ? ArrayType :
    T extends TypeKind.Intrinsic ? IntrinsicType :
    never;

function createIntrinsicTypes(): TypeTable {
    const typeSymbols = new Map<string, IntrinsicType>(
        Object.values(IntrinsicKind).map<[string, IntrinsicType]>((name) => [
            name,
            {
                kind: TypeKind.Intrinsic,
                name,
                intrinsicKind: IntrinsicKind[name] as IntrinsicKind,
                symbol: (() => {
                    const symbol = createSymbol(SymbolKind.Intrinsic);
                    symbol.name = name;
                    return symbol;
                })(),
                fullyQualifiedName: name,
            },
        ])
    );

    typeSymbols.set('customeventname', typeSymbols.get('string'));
    typeSymbols.set('scripteventname', typeSymbols.get('string'));

    return typeSymbols;
}

export const intrinsicTypes = createIntrinsicTypes();

export function getTypeOfScript(scriptNode: ScriptNode) {
    if (!scriptNode) {
        return null;
    }

    const scriptSymbol = scriptNode.symbol;

    const scriptType: ScriptType = {
        kind: TypeKind.Script,
        symbol: scriptSymbol,
        name: scriptSymbol.name,
        structTypes: null,
        get fullyQualifiedName() {
            return scriptSymbol.fullyQualifiedName;
        },
    };

    scriptType.structTypes = new Map(
        scriptSymbol.members.filter((m) => m.kind === SymbolKind.Struct).map(
            (s) =>
                [
                    s.name.toLowerCase(),
                    {
                        kind: TypeKind.Struct as TypeKind.Struct,
                        symbol: s as StructSymbol,
                        parentScriptType: scriptType,
                        name: s.name,
                        get fullyQualifiedName() {
                            return s.fullyQualifiedName;
                        },
                    },
                ] as [string, StructType]
        )
    );

    return scriptType;
}
