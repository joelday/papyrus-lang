import { getNameParts, Program } from '../program/Program';
import {
    CustomEventSymbol,
    EventSymbol,
    FunctionSymbol,
    GroupSymbol,
    PropertySymbol,
    ScriptSymbol,
    StateSymbol,
    StructSymbol,
    Symbol,
    SymbolKind,
    VariableSymbol,
} from '../symbols/Symbol';
import {
    ArrayType,
    intrinsicTypes,
    ScriptType,
    StructType,
    Type,
    TypeKind,
} from './Type';

import { isDescendentOfNodeOrSelf, visitAncestors } from '../common/TreeNode';
import { iterateMany, mapIterable } from '../common/Utilities';
import { Diagnostics } from '../Diagnostics';
import {
    ExpressionNode,
    IdentifierNode,
    isContainer,
    Node,
    NodeKind,
    TypeIdentifierNode,
} from '../parser/Node';
import { visitNode } from '../parser/NodeVisitor';
import { Parser } from '../parser/Parser';
import { SymbolBinder } from '../symbols/SymbolBinder';
import { Tokenizer } from '../tokenizer/Tokenizer';
import { TypeEvaluationVisitor } from './TypeEvaluationVisitor';
import { TypeValidationVisitor, validateTypes } from './TypeValidationVisitor';

export enum MemberTypes {
    None = 0,
    CustomEvent = 1,
    Event = 2,
    Function = 4,
    Group = 8,
    Property = 16,
    State = 32,
    Variable = 64,
    Struct = 128,
    All = CustomEvent |
        Event |
        Function |
        Group |
        Property |
        State |
        Variable |
        Struct,
}

export enum LookupFlags {
    Auto = 1,
    DeclaredOnly = 2,
    FlattenHierarchy = 4,
    Global = 8,
    Instance = 16,
    Native = 64,
    NonNative = 128,
    ReadOnly = 16384,
    Writable = 32768,
    NonAuto = 32768,
    AutoReadOnly = Auto | ReadOnly,
    Default = NonAuto |
        Auto |
        ReadOnly |
        Writable |
        Instance |
        Global |
        Native |
        NonNative,
}

export interface SymbolSearchResults {
    baseExpression: ExpressionNode;
    symbols: Symbol[];
}

interface Named {
    name: string;
}

function createArrayTypeSource(elementTypeName: string) {
    // tslint:disable:max-line-length
    return `Scriptname Array___${elementTypeName}

Function Clear() native
{Removes all items from the array, reducing it to a 0-length array.}

Function Add(${elementTypeName} akElement, int aiCount = 1) native
{Adds a new item to the array at the end, expanding it to fit. May add multiple items at once.}

Function Insert(${elementTypeName} akElement, int aiLocation) native
{Inserts a new item to the array at the specified position, expanding it to fit.}

Function Remove(int aiLocation, int aiCount = 1) native
{Removes an item or group of items from the array at the specified position, shrinking it to fit.}

Function RemoveLast() native
{Removes the last item from the array, shrinking it to fit.}

int Function Find(${elementTypeName} akElement, int aiStartIndex = 0) native
{Locates a particular value inside an array and returns the index.}

int Function FindStruct(string asVarName, var akElement, int aiStartIndex = 0) native
{Locates a particular value in a struct inside an array and returns the index}

int Function RFind(${elementTypeName} akElement, int aiStartIndex = -1) native
{Locates a particular value inside an array and returns the index, starting from the end of the array, and moving to the beginning.}

int Function RFindStruct(string asVarName, var akElement, int aiStartIndex = -1) native
{Locates a particular value in a struct inside an array and returns the index, starting from the end of the array, and moving to the beginning.}

int Property Length AutoReadonly
{The length of the array.}
`;
    // tslint:enable:max-line-length
}

function createSymbolFromSource(source: string) {
    const diagnostics = new Diagnostics('', source);
    const tokenizer = new Tokenizer();
    const tokens = Array.from(tokenizer.tokenize(source, diagnostics));

    const parser = new Parser();
    const scriptNode = parser.parse(tokens, diagnostics);

    const binder = new SymbolBinder();
    binder.bindSymbols(scriptNode, diagnostics);

    return scriptNode.symbol;
}

function createArrayTypeSymbol(elementTypeName: string) {
    const symbol = createSymbolFromSource(
        createArrayTypeSource(elementTypeName)
    );

    symbol.isGeneratedArray = true;
    symbol.name = `${elementTypeName}[]`;

    return symbol;
}

const symbolKindsToMemberTypes = new Map([
    [SymbolKind.CustomEvent, MemberTypes.CustomEvent],
    [SymbolKind.Event, MemberTypes.Event],
    [SymbolKind.Function, MemberTypes.Function],
    [SymbolKind.Group, MemberTypes.Group],
    [SymbolKind.Property, MemberTypes.Property],
    [SymbolKind.State, MemberTypes.State],
    [SymbolKind.Struct, MemberTypes.Struct],
    [SymbolKind.Variable, MemberTypes.Variable],
    [SymbolKind.Parameter, MemberTypes.Variable],
]);

export class TypeChecker {
    private readonly _program: Program;
    private readonly _typeEvaluationVisitor: TypeEvaluationVisitor;
    private readonly _typeValidationVisitor: TypeValidationVisitor;

    private readonly _arrayTypes: Map<string, ArrayType>;
    private readonly _arrayTypesBySymbol: Map<Symbol, ArrayType>;

    constructor(program: Program) {
        this._program = program;
        this._arrayTypes = new Map();
        this._arrayTypesBySymbol = new Map();

        this._typeEvaluationVisitor = new TypeEvaluationVisitor(this);
        this._typeValidationVisitor = new TypeValidationVisitor(this);
    }

    public getFunctionsForType(
        type: ScriptType | ArrayType,
        flags: LookupFlags = LookupFlags.Default
    ) {
        return Array.from(
            this.fromScriptHeirarchy(type, flags, (scriptType) => {
                return scriptType.symbol.members
                    .filter((s) => s.kind === SymbolKind.Function)
                    .filter((functionSymbol: FunctionSymbol) => {
                        return (
                            (this.checkFlag(
                                flags,
                                LookupFlags.Global,
                                () => functionSymbol.isGlobal
                            ) ||
                                this.checkFlag(
                                    flags,
                                    LookupFlags.Instance,
                                    () => !functionSymbol.isGlobal
                                )) &&
                            (this.checkFlag(
                                flags,
                                LookupFlags.Native,
                                () => functionSymbol.isNative
                            ) ||
                                this.checkFlag(
                                    flags,
                                    LookupFlags.NonNative,
                                    () => !functionSymbol.isNative
                                ))
                        );
                    });
            })
        ) as FunctionSymbol[];
    }

    public getEventsForType(
        type: ScriptType,
        flags: LookupFlags = LookupFlags.Default
    ) {
        return Array.from(
            this.fromScriptHeirarchy(type, flags, (scriptType: ScriptType) =>
                scriptType.symbol.members.filter(
                    (s) => s.kind === SymbolKind.Event
                )
            )
        ) as EventSymbol[];
    }

    public getGroupsForType(
        type: ScriptType,
        flags: LookupFlags = LookupFlags.Default
    ) {
        return Array.from(
            this.fromScriptHeirarchy(type, flags, (scriptType: ScriptType) =>
                scriptType.symbol.members.filter(
                    (s) => s.kind === SymbolKind.Group
                )
            )
        ) as GroupSymbol[];
    }

    public getPropertiesForType(
        type: ScriptType | ArrayType,
        flags: LookupFlags = LookupFlags.Default
    ) {
        return Array.from(
            this.fromScriptHeirarchy(type, flags, (scriptType) => {
                return scriptType.symbol.members
                    .filter((s) => s.kind === SymbolKind.Property)
                    .filter((property: PropertySymbol) => {
                        return (
                            (this.checkFlag(
                                flags,
                                LookupFlags.Auto,
                                () => property.isAuto
                            ) ||
                                this.checkFlag(
                                    flags,
                                    LookupFlags.NonAuto,
                                    () => !property.isAuto
                                )) &&
                            (this.checkFlag(
                                flags,
                                LookupFlags.ReadOnly,
                                () =>
                                    property.isAutoReadOnly || property.isConst
                            ) ||
                                this.checkFlag(
                                    flags,
                                    LookupFlags.Writable,
                                    () =>
                                        !property.isAutoReadOnly &&
                                        !property.isConst
                                ))
                        );
                    });
            })
        ) as PropertySymbol[];
    }

    public getCustomEventsForType(
        type: ScriptType,
        flags: LookupFlags = LookupFlags.Default
    ) {
        return Array.from(
            this.fromScriptHeirarchy(type, flags, (scriptType: ScriptType) =>
                scriptType.symbol.members.filter(
                    (s) => s.kind === SymbolKind.CustomEvent
                )
            )
        ) as CustomEventSymbol[];
    }

    public getStatesForType(
        type: ScriptType,
        flags: LookupFlags = LookupFlags.Default
    ) {
        return Array.from(
            this.fromScriptHeirarchy(type, flags, (scriptType: ScriptType) => {
                return scriptType.symbol.members
                    .filter((s) => s.kind === SymbolKind.State)
                    .filter((state: StateSymbol) => {
                        return (
                            this.checkFlag(
                                flags,
                                LookupFlags.Auto,
                                () => state.isAuto
                            ) ||
                            this.checkFlag(
                                flags,
                                LookupFlags.NonAuto,
                                () => !state.isAuto
                            )
                        );
                    });
            })
        ) as StateSymbol[];
    }

    public getMembers(
        type: ScriptType | StructType | ArrayType,
        memberTypes: MemberTypes = MemberTypes.All,
        lookupFlags: LookupFlags = LookupFlags.Default
    ) {
        return Array.from(
            iterateMany(this.iterateMemberTypes(type, memberTypes, lookupFlags))
        );
    }

    public getArrayTypeForName(fullName: string): ArrayType {
        if (!this._program.getTypeForName(fullName)) {
            return null;
        }

        if (!this._arrayTypes.has(fullName.toLowerCase())) {
            const type: ArrayType = {
                kind: TypeKind.Array,
                fullyQualifiedName: `${fullName}[]`,
                name: `${fullName}[]`,
                symbol: createArrayTypeSymbol(fullName),
                elementTypeName: fullName,
            };

            this._arrayTypes.set(fullName.toLowerCase(), type);
            this._arrayTypesBySymbol.set(type.symbol, type);
        }

        return this._arrayTypes.get(fullName.toLowerCase());
    }

    public getElementTypeForArrayType(arrayType: ArrayType) {
        return this._program.getTypeForName(arrayType.elementTypeName);
    }

    public getTypeOfSymbol(symbol: ScriptSymbol | StructSymbol) {
        if (symbol.kind === SymbolKind.Script && symbol.isGeneratedArray) {
            return this._arrayTypesBySymbol.get(symbol);
        }

        return this._program.getTypeForName(symbol.fullyQualifiedName);
    }

    public getTypeOfExpressionNode(node: ExpressionNode) {
        return visitNode<Type, ExpressionNode>(
            node,
            this._typeEvaluationVisitor
        );
    }

    public getTypeForTypeIdentifier(typeIdentifier: TypeIdentifierNode) {
        let type: Type = null;

        if (typeIdentifier.script.scriptFile) {
            type = this._program.getTypeForName(
                typeIdentifier.identifier.name,
                Array.from(
                    this.iterateScriptHierarchy(
                        typeIdentifier.script.scriptFile.type
                    )
                ).map((s) => s.fullyQualifiedName)
            );
        }

        if (!type) {
            type = this._program.getTypeForName(
                typeIdentifier.identifier.name,
                typeIdentifier.script.imports.map(
                    (importNode) => importNode.identifier.name
                )
            );
        }

        if (!type && typeIdentifier.script.scriptFile) {
            const currentScriptName =
                typeIdentifier.script.scriptFile.scriptName;

            const currentScriptNameParts = getNameParts(
                currentScriptName.toLowerCase()
            );
            const referencedTypeNameParts = getNameParts(
                typeIdentifier.identifier.name.toLowerCase()
            );

            if (
                referencedTypeNameParts.name === currentScriptNameParts.name ||
                referencedTypeNameParts.fullName.startsWith(
                    `${currentScriptNameParts.name}:`
                )
            ) {
                type = this._program.getTypeForName(
                    typeIdentifier.identifier.name,
                    [currentScriptNameParts.namespace]
                );
            }
        }

        if (type && typeIdentifier.isArray) {
            return this.getArrayTypeForName(type.fullyQualifiedName);
        }

        return type;
    }

    public getSymbolsForIdentifier(node: IdentifierNode): SymbolSearchResults {
        const baseExpression = this.getBaseExpressionForPossibleMemberAccess(
            node
        );

        if (baseExpression) {
            const possibleBaseIdentifierType = this.getTypeOfExpressionNode(
                baseExpression
            );

            if (
                possibleBaseIdentifierType &&
                possibleBaseIdentifierType.symbol &&
                possibleBaseIdentifierType.symbol.kind !== SymbolKind.Intrinsic
            ) {
                return {
                    baseExpression,
                    symbols: this.getMatchingSymbolsInScope(
                        possibleBaseIdentifierType.symbol.declaration.node,
                        node.name
                    ),
                };
            } else {
                return { baseExpression, symbols: [] };
            }
        }

        return {
            baseExpression: null,
            symbols: this.getMatchingSymbolsInScope(node, node.name),
        };
    }

    public *iterateLocalSymbolsInScope(
        fromNode: Node,
        memberTypes: MemberTypes = MemberTypes.All,
        lookupFlags: LookupFlags = LookupFlags.Default
    ) {
        for (const currentContainer of visitAncestors<Node>(fromNode, true)) {
            if (!isContainer(currentContainer)) {
                continue;
            }

            const symbolNames = new Set<string>();

            if (currentContainer.kind === NodeKind.Script) {
                const type = this.getTypeOfSymbol(
                    currentContainer.symbol
                ) as ScriptType;

                for (const member of this.getMembers(
                    type,
                    memberTypes,
                    lookupFlags
                )) {
                    symbolNames.add(member.fullyQualifiedName.toLowerCase());

                    yield member;
                }

                for (const importSymbol of currentContainer.symbol.imports) {
                    const importedScriptType = this._program.getTypeForName(
                        importSymbol.importedScript
                    ) as ScriptType;
                    if (!importedScriptType) {
                        continue;
                    }

                    for (const importedMember of this.getMembers(
                        importedScriptType,
                        (MemberTypes.Function & memberTypes) |
                            MemberTypes.Struct,
                        LookupFlags.Global |
                            LookupFlags.Native |
                            LookupFlags.NonNative
                    )) {
                        symbolNames.add(
                            importedMember.fullyQualifiedName.toLowerCase()
                        );

                        yield importedMember;
                    }
                }
            }

            if (
                (lookupFlags & LookupFlags.Instance) > 0 &&
                currentContainer.locals
            ) {
                for (const local of currentContainer.locals.values()) {
                    if (
                        !symbolNames.has(
                            local.fullyQualifiedName.toLowerCase()
                        ) &&
                        (memberTypes &
                            symbolKindsToMemberTypes.get(local.kind)) >
                            0
                    ) {
                        yield local;
                    }
                }
            }
        }
    }

    public getLocalSymbolsInScope(
        fromNode: Node,
        memberTypes: MemberTypes = MemberTypes.All,
        lookupFlags: LookupFlags = LookupFlags.Default
    ) {
        return Array.from(
            this.iterateLocalSymbolsInScope(fromNode, memberTypes, lookupFlags)
        );
    }

    public getMatchingSymbolsInScope(
        fromNode: Node,
        name: string,
        lookupFlags: LookupFlags = LookupFlags.Default
    ) {
        const scriptSymbol = fromNode.script.symbol;

        if (name.toLowerCase() === 'self') {
            return [scriptSymbol];
        }

        if (name.toLowerCase() === 'parent' && scriptSymbol.extendedScript) {
            const parentType = this._program.getScriptFileByName(
                scriptSymbol.extendedScript.name
            ).type;

            if (parentType) {
                return [parentType.symbol];
            }

            return [];
        }

        const results = this.getAvailableSymbolsAtNode(
            fromNode,
            this.getMemberTypesForScope(fromNode),
            lookupFlags
        );

        const matchingSymbols = results.symbols.filter(
            (s) => s.name.toLowerCase() === name.toLowerCase()
        );

        if (matchingSymbols.length > 0 || results.baseExpression) {
            return matchingSymbols;
        }

        const globalType = this._program.getTypeForName(name);
        if (globalType && globalType.kind !== TypeKind.Array) {
            return [globalType.symbol];
        }

        return [];
    }

    public isMemberAccessBaseExpressionStatic(accessNode: Node) {
        // Redundant, but faster.
        const baseExpression = this.getBaseExpressionForPossibleMemberAccess(
            accessNode
        );

        if (!baseExpression) {
            return false;
        }

        if (baseExpression.kind === NodeKind.IdentifierExpression) {
            const { symbols } = this.getSymbolsForIdentifier(
                baseExpression.identifier
            );

            // Kinda hacky, but here we're checking that this identifier points to the type by name directly.
            if (
                symbols.length > 0 &&
                symbols[0].kind === SymbolKind.Script &&
                symbols[0].name.toLowerCase() ===
                    baseExpression.identifier.name.toLowerCase()
            ) {
                return true;
            }
        }

        return false;
    }

    public getAvailableSymbolsAtNode(
        node: Node,
        memberTypes: MemberTypes = MemberTypes.All,
        lookupFlags: LookupFlags = LookupFlags.Default
    ): SymbolSearchResults {
        const baseExpression = this.getBaseExpressionForPossibleMemberAccess(
            node
        );

        if (baseExpression) {
            let modifiedLookupFlags = lookupFlags;

            const isGlobalAccess = this.isMemberAccessBaseExpressionStatic(
                node
            );

            if (isGlobalAccess) {
                modifiedLookupFlags ^= LookupFlags.Instance;
            } else {
                modifiedLookupFlags ^= LookupFlags.Global;
            }

            const possibleBaseIdentifierType = this.getTypeOfExpressionNode(
                baseExpression
            );

            if (
                possibleBaseIdentifierType &&
                possibleBaseIdentifierType.kind !== TypeKind.Intrinsic
            ) {
                return {
                    baseExpression,
                    symbols: this.getMembers(
                        possibleBaseIdentifierType,
                        this.getMemberTypesForScope(node),
                        modifiedLookupFlags
                    ),
                };
            } else {
                return {
                    baseExpression,
                    symbols: [],
                };
            }
        }

        return {
            baseExpression: null,
            symbols: this.getLocalSymbolsInScope(
                node,
                memberTypes,
                lookupFlags
            ),
        };
    }

    public getVariableIsBlockScoped(symbol: VariableSymbol) {
        return symbol.declaration.node.kind === NodeKind.DeclareStatement;
    }

    public checkTypesAndReferences(rootNode: Node, diagnostics: Diagnostics) {
        validateTypes(this._typeValidationVisitor, diagnostics, rootNode);
    }

    public getIdentifierForExpression(node: ExpressionNode) {
        if (
            node.kind === NodeKind.MemberAccessExpression &&
            node.accessExpression.kind === NodeKind.IdentifierExpression
        ) {
            return node.accessExpression.identifier;
        }

        if (node.kind === NodeKind.IdentifierExpression) {
            return node.identifier;
        }

        return null;
    }

    public getIdentifierIsWritable(node: IdentifierNode) {
        const { symbols } = this.getSymbolsForIdentifier(node);
        if (symbols.length > 0) {
            const symbol = symbols[0];
            if (symbol.kind === SymbolKind.Property) {
                return !symbol.isAutoReadOnly || !symbol.isConst;
            }

            if (symbol.kind === SymbolKind.Variable) {
                return !symbol.isConst;
            }

            if (symbol.kind === SymbolKind.Parameter) {
                return true;
            }
        }

        return false;
    }

    public getExpressionIsAssignable(node: ExpressionNode) {
        if (node.kind === NodeKind.IdentifierExpression) {
            return true;
        }

        if (
            node.kind === NodeKind.ArrayIndexExpression ||
            (node.kind === NodeKind.MemberAccessExpression &&
                node.accessExpression.kind === NodeKind.IdentifierExpression)
        ) {
            return true;
        }

        return false;
    }

    public getTypeIsAssignableFrom(to: Type, from: Type) {
        if (!to || !from) {
            return false;
        }

        if (to.kind === TypeKind.Array && from.kind === TypeKind.Array) {
            return true;
        }

        if (
            to === from ||
            to === intrinsicTypes.get('var') ||
            to === intrinsicTypes.get('string') ||
            to === intrinsicTypes.get('bool') ||
            to === intrinsicTypes.get('int')
        ) {
            return true;
        }

        if (
            to === intrinsicTypes.get('any') ||
            from === intrinsicTypes.get('any')
        ) {
            return true;
        }

        if (
            (to === intrinsicTypes.get('float') ||
                to === intrinsicTypes.get('int') ||
                to === intrinsicTypes.get('string') ||
                to === intrinsicTypes.get('bool')) &&
            (from === intrinsicTypes.get('float') ||
                from === intrinsicTypes.get('int') ||
                from === intrinsicTypes.get('string') ||
                from === intrinsicTypes.get('bool'))
        ) {
            return true;
        }

        if (to.kind !== TypeKind.Script || from.kind !== TypeKind.Script) {
            return false;
        }

        for (const fromTypeParent of this.iterateScriptHierarchy(from)) {
            if (
                fromTypeParent.fullyQualifiedName.toLowerCase() ===
                to.fullyQualifiedName.toLowerCase()
            ) {
                return true;
            }
        }

        return false;
    }

    private getMemberTypesForScope(fromNode: Node) {
        for (const ancestor of visitAncestors(fromNode, true)) {
            if (typeof ancestor.scopeMemberTypes !== 'undefined') {
                return ancestor.scopeMemberTypes;
            }
        }

        return MemberTypes.All ^ MemberTypes.Group ^ MemberTypes.State;
    }

    private *iterateScriptHierarchy(childScript: ScriptType) {
        let scriptType = childScript;
        while (scriptType) {
            yield scriptType;

            if (!this.getTypeOfSymbol(scriptType.symbol)) {
                return;
            }

            if (scriptType.symbol.extendedScript) {
                scriptType = this._program.getTypeForName(
                    scriptType.symbol.extendedScript.name
                ) as ScriptType;
            } else {
                scriptType = null;
            }
        }
    }

    private mapScriptHeirarchy<T>(
        childType: ScriptType,
        getFn: (scriptType: ScriptType) => T[]
    ): IterableIterator<T> {
        return iterateMany(
            mapIterable(this.iterateScriptHierarchy(childType), getFn)
        );
    }

    private *fromScriptHeirarchy<T extends Named>(
        childType: ScriptType | ArrayType,
        flags: LookupFlags,
        getFn: (scriptType: ScriptType | ArrayType) => T[]
    ): IterableIterator<T> {
        if (
            childType.kind !== TypeKind.Script ||
            (flags & LookupFlags.DeclaredOnly) > 0
        ) {
            for (const element of getFn(childType)) {
                yield element;
            }

            return;
        }

        const flatten = (flags & LookupFlags.FlattenHierarchy) > 0;
        const names = new Set<string>();

        for (const element of this.mapScriptHeirarchy(childType, getFn)) {
            if (
                !element ||
                (flatten && names.has(element.name.toLowerCase()))
            ) {
                continue;
            }

            names.add(element.name);
            yield element;
        }
    }

    private checkFlag(
        flags: LookupFlags,
        flag: LookupFlags,
        check: () => boolean
    ) {
        if ((flags & flag) > 0) {
            return check();
        }

        return false;
    }

    private *iterateMemberTypes(
        type: ScriptType | StructType | ArrayType,
        memberTypes: MemberTypes,
        lookupFlags: LookupFlags
    ): IterableIterator<Iterable<Symbol>> {
        if (
            (memberTypes & MemberTypes.Function) > 0 &&
            (type.kind === TypeKind.Script || type.kind === TypeKind.Array)
        ) {
            yield this.getFunctionsForType(type, lookupFlags);
        }

        if (
            (memberTypes & MemberTypes.Struct) > 0 &&
            type.kind === TypeKind.Script
        ) {
            yield type.symbol.members.filter(
                (s) => s.kind === SymbolKind.Struct
            );
        }

        if (
            (lookupFlags & LookupFlags.Global) > 0 &&
            (memberTypes & MemberTypes.CustomEvent) > 0 &&
            type.kind === TypeKind.Script
        ) {
            yield this.getCustomEventsForType(type, lookupFlags);
        }

        if ((lookupFlags & LookupFlags.Instance) === 0) {
            return;
        }

        if (
            (memberTypes & MemberTypes.Event) > 0 &&
            type.kind === TypeKind.Script
        ) {
            yield this.getEventsForType(type, lookupFlags);
        }

        if (
            (memberTypes & MemberTypes.Group) > 0 &&
            type.kind === TypeKind.Script
        ) {
            yield this.getGroupsForType(type, lookupFlags);
        }

        if (
            (memberTypes & MemberTypes.Property) > 0 &&
            (type.kind === TypeKind.Script || type.kind === TypeKind.Array)
        ) {
            yield this.getPropertiesForType(type, lookupFlags);
        }

        if (
            (memberTypes & MemberTypes.State) > 0 &&
            type.kind === TypeKind.Script
        ) {
            yield this.getStatesForType(type, lookupFlags);
        }

        if (
            (memberTypes & MemberTypes.Variable) > 0 &&
            (type.kind === TypeKind.Script || type.kind === TypeKind.Struct)
        ) {
            yield (type.symbol.members as Symbol[]).filter(
                (s) => s.kind === SymbolKind.Variable
            );
        }
    }

    private getBaseExpressionForPossibleMemberAccess(node: Node) {
        for (const ancestor of visitAncestors(node, true)) {
            if (ancestor.kind === NodeKind.FunctionCallExpressionParameter) {
                return null;
            }

            if (
                ancestor.kind === NodeKind.MemberAccessExpression &&
                !isDescendentOfNodeOrSelf(node, ancestor.baseExpression)
            ) {
                return ancestor.baseExpression;
            }
        }

        return null;
    }
}
