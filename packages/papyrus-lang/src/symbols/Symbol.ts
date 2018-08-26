import {
    CustomEventDefinitionNode,
    DeclareStatementNode,
    EventDefinitionNode,
    Flags,
    FunctionDefinitionNode,
    FunctionParameterNode,
    GroupDefinitionNode,
    IdentifierNode,
    ImportNode,
    LiteralNode,
    PropertyDefinitionNode,
    ScriptNode,
    StateDefinitionNode,
    StructDefinitionNode,
    SymbolNode,
    VariableDefinitionNode,
} from '../parser/Node';
import { ScriptType, StructType, Type } from '../types/Type';

export enum SymbolKind {
    CustomEvent = 'CustomEvent',
    Event = 'Event',
    Function = 'Function',
    Import = 'Import',
    Group = 'Group',
    Parameter = 'Parameter',
    Property = 'Property',
    Script = 'Script',
    State = 'State',
    Struct = 'Struct',
    Variable = 'Variable',
    Intrinsic = 'Intrinsic',
}

export interface TypeReference {
    name: string;
    asArray: boolean;
}

export interface SymbolDeclaration<TNode extends SymbolNode = SymbolNode> {
    node: TNode;
    identifier: IdentifierNode;
}

export interface SymbolInterface<TKind extends SymbolKind = SymbolKind> {
    children: Symbol[];
    kind: TKind;
    name: string;
    parent: Symbol;

    readonly fullyQualifiedName: string;
}

export interface CustomEventSymbol
    extends SymbolInterface<SymbolKind.CustomEvent> {
    declaration: SymbolDeclaration<CustomEventDefinitionNode>;
}

export interface EventSymbol extends SymbolInterface<SymbolKind.Event> {
    declaration: SymbolDeclaration<EventDefinitionNode>;
    parameters: ParameterSymbol[];
    documentation: string;
}

export interface FunctionSymbol extends SymbolInterface<SymbolKind.Function> {
    declaration: SymbolDeclaration<FunctionDefinitionNode>;
    parameters: ParameterSymbol[];
    returnType: TypeReference;
    isGlobal: boolean;
    isNative: boolean;
    userFlags: string[];
    documentation: string;
}

export interface ImportSymbol extends SymbolInterface<SymbolKind.Import> {
    declaration: SymbolDeclaration<ImportNode>;
    importedScript: string;
}

export interface PropertySymbol extends SymbolInterface<SymbolKind.Property> {
    declaration: SymbolDeclaration<PropertyDefinitionNode>;
    getter: FunctionSymbol;
    isAutoReadOnly: boolean;
    isAuto: boolean;
    isConst: boolean;
    setter: FunctionSymbol;
    valueType: TypeReference;
    userFlags: string[];
    documentation: string;
}

export type ScriptMemberSymbol =
    | CustomEventSymbol
    | EventSymbol
    | FunctionSymbol
    | GroupSymbol
    | PropertySymbol
    | StateSymbol
    | StructSymbol
    | VariableSymbol;

export interface ScriptSymbol extends SymbolInterface<SymbolKind.Script> {
    declaration: SymbolDeclaration<ScriptNode>;
    members: ScriptMemberSymbol[];
    extendedScript: TypeReference;
    imports: ImportSymbol[];
    isGeneratedArray: boolean;
    isNative: boolean;
    isConst: boolean;
    userFlags: string[];
    documentation: string;
}

export interface GroupSymbol extends SymbolInterface<SymbolKind.Group> {
    declaration: SymbolDeclaration<GroupDefinitionNode>;
    properties: PropertySymbol[];
}

export interface StateSymbol extends SymbolInterface<SymbolKind.State> {
    declaration: SymbolDeclaration<StateDefinitionNode>;
    members: (FunctionSymbol | EventSymbol)[];
    isAuto: boolean;
}

export interface StructSymbol extends SymbolInterface<SymbolKind.Struct> {
    declaration: SymbolDeclaration<StructDefinitionNode>;
    members: VariableSymbol[];
}

export interface VariableSymbolBase<TKind extends SymbolKind>
    extends SymbolInterface<TKind> {
    valueType: TypeReference;
}

export interface VariableSymbol
    extends VariableSymbolBase<SymbolKind.Variable> {
    declaration: SymbolDeclaration<
        VariableDefinitionNode | DeclareStatementNode
    >;

    isConst: boolean;
}

export interface ParameterSymbol
    extends VariableSymbolBase<SymbolKind.Parameter> {
    declaration: SymbolDeclaration<FunctionParameterNode>;
    parentFunction: FunctionSymbol | EventSymbol;
    isOptional: boolean;
    defaultValue: LiteralNode;
}

export interface IntrinsicSymbol
    extends SymbolInterface<SymbolKind.Intrinsic> {}

// prettier-ignore
export type DeclarableSymbol<T extends SymbolKind = SymbolKind> =
    T extends SymbolKind.CustomEvent ? CustomEventSymbol :
    T extends SymbolKind.Event ? EventSymbol :
    T extends SymbolKind.Function ? FunctionSymbol :
    T extends SymbolKind.Group ? GroupSymbol :
    T extends SymbolKind.Import ? ImportSymbol :
    T extends SymbolKind.Parameter ? ParameterSymbol :
    T extends SymbolKind.Property ? PropertySymbol :
    T extends SymbolKind.Script ? ScriptSymbol :
    T extends SymbolKind.State ? StateSymbol :
    T extends SymbolKind.Struct ? StructSymbol :
    T extends SymbolKind.Variable ? VariableSymbol :
    never;

// prettier-ignore
export type Symbol<T extends SymbolKind = SymbolKind> =
    T extends SymbolKind.CustomEvent ? CustomEventSymbol :
    T extends SymbolKind.Event ? EventSymbol :
    T extends SymbolKind.Function ? FunctionSymbol :
    T extends SymbolKind.Group ? GroupSymbol :
    T extends SymbolKind.Import ? ImportSymbol :
    T extends SymbolKind.Parameter ? ParameterSymbol :
    T extends SymbolKind.Property ? PropertySymbol :
    T extends SymbolKind.Script ? ScriptSymbol :
    T extends SymbolKind.State ? StateSymbol :
    T extends SymbolKind.Struct ? StructSymbol :
    T extends SymbolKind.Variable ? VariableSymbol :
    T extends SymbolKind.Intrinsic ? IntrinsicSymbol :
    never;

export type SymbolTable = Map<string, Symbol>;

export class SymbolObject<TKind extends SymbolKind = SymbolKind>
    implements SymbolInterface<TKind> {
    private readonly _kind: TKind;
    private readonly _children: SymbolObject[] = [];

    private _parent: SymbolObject;
    private _declaration: SymbolDeclaration;
    private _name: string;
    private _fullyQualifiedName: string;

    get kind() {
        return this._kind;
    }

    get children() {
        return this._children as any;
    }

    get declaration() {
        return this._declaration;
    }

    set declaration(declaration: SymbolDeclaration) {
        this._declaration = declaration;
    }

    get name() {
        return this._name;
    }

    set name(name: string) {
        this._name = name;
        this._fullyQualifiedName = null;
    }

    get parent() {
        return this._parent as Symbol;
    }

    set parent(value: Symbol) {
        if (this._parent === value) {
            return;
        }

        if (this._parent) {
            this._parent._children.splice(
                this._parent._children.indexOf(this),
                1
            );
        }

        this._parent = value as SymbolObject;

        if (this._parent) {
            this._parent._children.push(this);
        }

        this._fullyQualifiedName = null;
    }

    get fullyQualifiedName(): string {
        return (
            this._fullyQualifiedName ||
            (this._fullyQualifiedName =
                (this._parent ? `${this._parent.fullyQualifiedName}:` : '') +
                this._name)
        );
    }

    constructor(kind: TKind) {
        this._kind = kind;
    }
}

export function createSymbol<TKind extends SymbolKind>(
    kind: TKind
): Symbol<TKind> {
    return (new SymbolObject(kind) as any) as Symbol<TKind>;
}
