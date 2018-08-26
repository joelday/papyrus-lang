import { visitAncestors, visitTree } from '../common/TreeNode';
import { flushIterator } from '../common/Utilities';
import { Diagnostics } from '../Diagnostics';
import {
    ContainerNode,
    CustomEventDefinitionNode,
    DeclareStatementNode,
    EventDefinitionNode,
    Flags,
    FunctionDefinitionNode,
    FunctionParameterNode,
    GroupDefinitionNode,
    ImportNode,
    isContainer,
    LanguageFlagNode,
    Node,
    NodeKind,
    PropertyDefinitionNode,
    ScriptNode,
    StateDefinitionNode,
    StructDefinitionNode,
    UserFlagNode,
    VariableDefinitionNode,
} from '../parser/Node';
import {
    NodeVisitor,
    visitAll,
    visitNode,
    visitNodes,
} from '../parser/NodeVisitor';
import {
    createSymbol,
    DeclarableSymbol,
    EventSymbol,
    FunctionSymbol,
    GroupSymbol,
    ImportSymbol,
    ParameterSymbol,
    PropertySymbol,
    ScriptMemberSymbol,
    StateSymbol,
    Symbol,
    SymbolDeclaration,
    SymbolKind,
    SymbolTable,
    VariableSymbol,
} from './Symbol';

export class SymbolDeclarationVisitor extends NodeVisitor<Symbol, Symbol> {
    private readonly _diagnostics: Diagnostics;

    constructor(diagnostics: Diagnostics) {
        super();
        this._diagnostics = diagnostics;
    }

    public visitScript(node: ScriptNode) {
        return this.declareSymbol(
            SymbolKind.Script,
            {
                node: node,
                identifier: node.header.identifier,
            },
            undefined,
            (symbol) => {
                node.locals = null;

                symbol.members = node.definitions.map((d) =>
                    visitNode(d, this, symbol)
                ) as ScriptMemberSymbol[];

                for (const group of symbol.members.filter(
                    (member) => member.kind === SymbolKind.Group
                )) {
                    symbol.members.push(...(group as GroupSymbol).properties);
                }

                for (const state of symbol.members.filter(
                    (member) => member.kind === SymbolKind.State
                )) {
                    symbol.members.push(...(state as StateSymbol).members);
                }

                symbol.imports = node.imports.map((d) =>
                    visitNode(d, this, symbol)
                ) as ImportSymbol[];

                const languageFlags = node.header.flags.filter(
                    (f) => f.kind === NodeKind.LanguageFlag
                ) as LanguageFlagNode[];

                symbol.isNative = languageFlags.some(
                    (f) => f.flag === Flags.Native
                );
                symbol.isConst = languageFlags.some(
                    (f) => f.flag === Flags.Const
                );

                symbol.userFlags = node.header.flags
                    .filter((f) => f.kind === NodeKind.UserFlag)
                    .map((n: UserFlagNode) => n.name);

                symbol.extendedScript = node.header.extendedIdentifier
                    ? {
                          name: node.header.extendedIdentifier.identifier.name,
                          asArray: false,
                      }
                    : null;

                if (
                    !symbol.extendedScript &&
                    node.header.identifier.name.toLowerCase() !== 'scriptobject'
                ) {
                    symbol.extendedScript = {
                        name: 'ScriptObject',
                        asArray: false,
                    };
                }

                if (node.header.docComment) {
                    symbol.documentation = node.header.docComment.text;
                }
            }
        );
    }

    public visitGroupDefinition(node: GroupDefinitionNode, parent: Symbol) {
        return this.declareSymbol(
            SymbolKind.Group,
            {
                node: node,
                identifier: node.identifier,
            },
            parent,
            (symbol) => {
                symbol.properties = node.properties.map((d) =>
                    visitNode(d, this, symbol)
                ) as PropertySymbol[];
            }
        );
    }

    public visitCustomEventDefinition(
        node: CustomEventDefinitionNode,
        parent: Symbol
    ) {
        return this.declareSymbol(
            SymbolKind.CustomEvent,
            {
                node,
                identifier: node.identifier,
            },
            parent,
            undefined,
            (container: ScriptNode) => container.customEvents
        );
    }

    public visitDeclareStatement(node: DeclareStatementNode, parent: Symbol) {
        return this.declareSymbol(
            SymbolKind.Variable,
            {
                node,
                identifier: node.identifier,
            },
            parent,
            (symbol) => {
                symbol.valueType = {
                    name: node.typeIdentifier.identifier.name,
                    asArray: node.typeIdentifier.isArray,
                };
            }
        );
    }

    public visitEventDefinition(node: EventDefinitionNode, parent: Symbol) {
        return this.declareSymbol(
            SymbolKind.Event,
            {
                node,
                identifier: node.header.identifier.identifier,
            },
            parent,
            (symbol) => {
                symbol.parameters = node.header.parameters.parameters.map((p) =>
                    visitNode(p, this, symbol)
                ) as ParameterSymbol[];

                if (node.statements) {
                    for (const statement of node.statements) {
                        visitAll(statement, this, symbol);
                    }
                }

                if (node.header.leadingDocComment) {
                    symbol.documentation = node.header.leadingDocComment.text;
                }

                if (node.header.docComment) {
                    if (!symbol.documentation) {
                        symbol.documentation = '';
                    } else {
                        symbol.documentation += '\r\n\r\n';
                    }

                    symbol.documentation += node.header.docComment.text;
                }
            },
            () => null
        );
    }

    public visitFunctionDefinition(
        node: FunctionDefinitionNode,
        parent: Symbol
    ) {
        return this.declareSymbol(
            SymbolKind.Function,
            {
                node,
                identifier: node.header.identifier,
            },
            parent,
            (symbol) => {
                const languageFlags = node.header.flags.filter(
                    (f) => f.kind === NodeKind.LanguageFlag
                ) as LanguageFlagNode[];

                symbol.isGlobal = languageFlags.some(
                    (f) => f.flag === Flags.Global
                );
                symbol.isNative = languageFlags.some(
                    (f) => f.flag === Flags.Native
                );

                symbol.userFlags = node.header.flags
                    .filter((f) => f.kind === NodeKind.UserFlag)
                    .map((n: UserFlagNode) => n.name);

                symbol.parameters = node.header.parameters.parameters.map((p) =>
                    visitNode(p, this, symbol)
                ) as ParameterSymbol[];

                symbol.returnType = node.header.returnTypeIdentifier
                    ? {
                          name:
                              node.header.returnTypeIdentifier.identifier.name,
                          asArray: node.header.returnTypeIdentifier.isArray,
                      }
                    : null;

                if (node.header.leadingDocComment) {
                    symbol.documentation = node.header.leadingDocComment.text;
                }

                if (node.header.docComment) {
                    if (!symbol.documentation) {
                        symbol.documentation = '';
                    } else {
                        symbol.documentation += '\r\n\r\n';
                    }

                    symbol.documentation += node.header.docComment.text;
                }

                if (node.statements) {
                    for (const statement of node.statements) {
                        visitAll(statement, this, symbol);
                    }
                }
            }
        );
    }

    public visitFunctionParameter(node: FunctionParameterNode, parent: Symbol) {
        return this.declareSymbol(
            SymbolKind.Parameter,
            {
                node,
                identifier: node.identifier,
            },
            parent,
            (symbol) => {
                symbol.valueType = {
                    name: node.typeIdentifier.identifier.name,
                    asArray: node.typeIdentifier.isArray,
                };
                symbol.isOptional = node.isOptional;
                symbol.parentFunction = node.parent as any;
                symbol.defaultValue = node.defaultValue;
            }
        );
    }

    public visitPropertyDefinition(
        node: PropertyDefinitionNode,
        parent: Symbol
    ) {
        return this.declareSymbol(
            SymbolKind.Property,
            {
                node,
                identifier: node.identifier,
            },
            parent,
            (symbol) => {
                const getter = node.functions.find(
                    (f) => f.header.identifier.name.toLowerCase() === 'get'
                );
                if (getter) {
                    symbol.getter = visitNode(
                        getter,
                        this,
                        symbol
                    ) as FunctionSymbol;
                }

                const setter = node.functions.find(
                    (f) => f.header.identifier.name.toLowerCase() === 'set'
                );

                if (setter) {
                    symbol.setter = visitNode(
                        setter,
                        this,
                        symbol
                    ) as FunctionSymbol;
                }

                symbol.valueType = {
                    name: node.typeIdentifier.identifier.name,
                    asArray: node.typeIdentifier.isArray,
                };

                symbol.isAuto =
                    (getter && !setter) ||
                    node.flags.some(
                        (f) =>
                            f.kind === NodeKind.LanguageFlag &&
                            (f.flag === Flags.Auto ||
                                f.flag === Flags.AutoReadOnly)
                    );

                symbol.isAutoReadOnly =
                    (getter && !setter) ||
                    node.flags.some(
                        (f) =>
                            f.kind === NodeKind.LanguageFlag &&
                            f.flag === Flags.AutoReadOnly
                    );

                symbol.isConst =
                    (getter && !setter) ||
                    node.flags.some(
                        (f) =>
                            f.kind === NodeKind.LanguageFlag &&
                            f.flag === Flags.Const
                    );

                symbol.userFlags = node.flags
                    .filter((n) => n.kind === NodeKind.UserFlag)
                    .map((n: UserFlagNode) => n.name);

                if (node.leadingDocComment) {
                    symbol.documentation = node.leadingDocComment.text;
                }

                if (node.docComment) {
                    if (!symbol.documentation) {
                        symbol.documentation = '';
                    } else {
                        symbol.documentation += '\r\n\r\n';
                    }

                    symbol.documentation += node.docComment.text;
                }

                // Still produce symbols for invalid functions.
                flushIterator(
                    visitNodes(
                        node.functions.filter(
                            (f) => f !== getter && f !== setter
                        ),
                        this,
                        symbol
                    )
                );
            }
        );
    }

    public visitStateDefinition(node: StateDefinitionNode, parent: Symbol) {
        return this.declareSymbol(
            SymbolKind.State,
            {
                node,
                identifier: node.identifier,
            },
            parent,
            (symbol) => {
                symbol.members = node.definitions.map((d) =>
                    visitNode(d, this, symbol)
                ) as (EventSymbol | FunctionSymbol)[];
            },
            (container: ScriptNode) => container.states
        );
    }

    public visitStructDefinition(node: StructDefinitionNode, parent: Symbol) {
        return this.declareSymbol(
            SymbolKind.Struct,
            {
                node,
                identifier: node.identifier,
            },
            parent,
            (symbol) => {
                symbol.members = node.members.map((d) =>
                    visitNode(d, this, symbol)
                ) as VariableSymbol[];
            }
        );
    }

    public visitImport(node: ImportNode, parent: Symbol) {
        return this.declareSymbol(
            SymbolKind.Import,
            {
                node,
                identifier: node.identifier,
            },
            parent,
            (symbol) => {
                symbol.importedScript = node.identifier.name;
            }
        );
    }

    public visitVariableDefinition(
        node: VariableDefinitionNode,
        parent: Symbol
    ) {
        return this.declareSymbol(
            SymbolKind.Variable,
            {
                node,
                identifier: node.identifier,
            },
            parent,
            (symbol) => {
                symbol.valueType = {
                    name: node.typeIdentifier.identifier.name,
                    asArray: node.typeIdentifier.isArray,
                };

                symbol.isConst = node.flags.some(
                    (f) =>
                        f.kind === NodeKind.LanguageFlag &&
                        f.flag === Flags.Const
                );
            }
        );
    }

    private declareSymbol<TKind extends SymbolKind>(
        kind: TKind,
        declaration: DeclarableSymbol<TKind>['declaration'],
        parent: Symbol,
        symbolFn?: (symbol: DeclarableSymbol<TKind>) => void,
        symbolTableFn: (node: ContainerNode) => SymbolTable = (node) =>
            node.locals
    ) {
        if (declaration.node.symbol) {
            return declaration.node.symbol;
        }

        if (!declaration.identifier) {
            this._diagnostics.addError(
                'Identifier expected.',
                declaration.node.range
            );
        }

        const symbol = createSymbol(kind) as DeclarableSymbol;
        symbol.parent = parent;
        symbol.declaration = declaration;
        symbol.name = declaration.identifier
            ? declaration.identifier.name
            : '<unknown>';
        declaration.node.symbol = symbol;

        if (!declaration.identifier) {
            return symbol;
        }

        for (const ancestor of visitAncestors(declaration.node as Node)) {
            if (isContainer(ancestor)) {
                const locals = symbolTableFn(ancestor);
                if (!locals) {
                    continue;
                }

                if (locals.has(symbol.name.toLowerCase())) {
                    this._diagnostics.addError(
                        `Cannot redeclare ${declaration.identifier.name}.`,
                        declaration.node.range
                    );
                } else {
                    locals.set(
                        declaration.identifier.name.toLowerCase(),
                        symbol
                    );
                }

                break;
            }
        }

        if (symbolFn) {
            symbolFn(symbol as DeclarableSymbol<TKind>);
        }

        return symbol;
    }
}
