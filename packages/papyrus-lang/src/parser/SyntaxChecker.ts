import { visitAncestors, visitTree } from '../common/TreeNode';
import { Diagnostics } from '../Diagnostics';
import {
    EventDefinitionNode,
    EventHeaderNode,
    ExpressionNode,
    FlaggableNode,
    FlagNode,
    Flags,
    FunctionDefinitionNode,
    isFlaggable,
    MemberAccessExpressionNode,
    Node,
    NodeKind,
    PropertyDefinitionNode,
    ScriptHeaderNode,
    ScriptNode,
    StateDefinitionNode,
    TypeIdentifierNode,
} from '../parser/Node';
import { NodeVisitor, visitAll } from './NodeVisitor';

class SyntaxCheckerVisitor extends NodeVisitor {
    private readonly _diagnostics: Diagnostics;

    constructor(diagnostics: Diagnostics) {
        super();
        this._diagnostics = diagnostics;
    }

    public visitScript(node: ScriptNode) {
        this.validateSingleAutoState(node);
    }

    public visitPropertyDefinition(node: PropertyDefinitionNode) {
        this.validatePropertyFunctions(node);
    }

    public defaultVisit(node: Node) {
        if (isFlaggable(node) && node.flags) {
            this.validateNoDuplicateFlags(node);
        }
    }

    public visitFunctionDefinition(node: FunctionDefinitionNode) {
        this.validateMemberAccessInGlobalFunction(node);
    }

    private validateMemberAccessInGlobalFunction(node: FunctionDefinitionNode) {
        if (
            !node.header.flags.some(
                (f) =>
                    f.kind === NodeKind.LanguageFlag && f.flag === Flags.Global
            )
        ) {
            return;
        }

        for (const child of visitTree<Node>(node)) {
            if (
                child.node.kind === NodeKind.Identifier &&
                (child.node.name.toLowerCase() === 'self' ||
                    child.node.name.toLowerCase() === 'parent')
            ) {
                this.addError(
                    "'self' and 'parent' cannot be referenced within a global function.",
                    child.node
                );
            }
        }
    }

    private addError(message: string, node: Node) {
        this._diagnostics.addError(message, node.range);
    }

    private validateSingleAutoState(node: ScriptNode) {
        const autoStates = node.definitions.filter(
            (d) => d.kind === NodeKind.StateDefinition && d.isAuto
        ) as StateDefinitionNode[];

        if (autoStates.length > 1) {
            for (const state of autoStates.slice(1)) {
                this.addError(
                    'Only one state can be marked as auto.',
                    state.identifier
                );
            }
        }
    }

    private validateNoDuplicateFlags(node: FlaggableNode) {
        const duplicateFlags: FlagNode[] = [];

        let languageFlags = Flags.None;
        const userFlags = new Set<string>();

        for (const flag of node.flags) {
            if (flag.kind === NodeKind.LanguageFlag) {
                if (languageFlags & flag.flag) {
                    duplicateFlags.push(flag);
                } else {
                    languageFlags |= flag.flag;
                }
            } else if (flag.kind === NodeKind.UserFlag) {
                if (userFlags.has(flag.name)) {
                    duplicateFlags.push(flag);
                } else {
                    userFlags.add(flag.name);
                }
            }
        }

        for (const flag of duplicateFlags) {
            this.addError('Duplicate flags are not allowed.', flag);
        }
    }

    private validatePropertyFunctions(node: PropertyDefinitionNode) {
        if (
            node.flags.some(
                (f) =>
                    f.kind === NodeKind.LanguageFlag &&
                    (f.flag === Flags.Auto || f.flag === Flags.AutoReadOnly)
            )
        ) {
            if (
                node.flags.some(
                    (f) =>
                        f.kind === NodeKind.LanguageFlag &&
                        f.flag === Flags.Auto
                ) &&
                node.flags.some(
                    (f) =>
                        f.kind === NodeKind.LanguageFlag &&
                        f.flag === Flags.AutoReadOnly
                )
            ) {
                this.addError(
                    "A property cannot be marked as both 'auto' and 'autoReadOnly'",
                    node.identifier
                );
            }

            return;
        }

        if (node.functions.length === 0) {
            this.addError(
                "A non-auto property must have a 'get' function declared.",
                node.identifier
            );
        }

        const nonGetSetFunctions = node.functions.filter(
            (n) =>
                n.header.identifier.name.toLowerCase() !== 'get' &&
                n.header.identifier.name.toLowerCase() !== 'set'
        );

        for (const nonGetSet of nonGetSetFunctions) {
            this.addError(
                "Only functions named 'get' or 'set' are allowed within property definitions.",
                nonGetSet.header
            );
        }

        const getters = node.functions.filter(
            (n) => n.header.identifier.name.toLowerCase() === 'get'
        );

        for (const duplicateGetter of getters.slice(1)) {
            this.addError(
                "Only a single 'get' function is allowed in a property definition.",
                duplicateGetter.header
            );
        }

        const setters = node.functions.filter(
            (n) => n.header.identifier.name.toLowerCase() === 'set'
        );

        for (const duplicateSetter of setters.slice(1)) {
            this.addError(
                "Only a single 'set' function is allowed in a property definition.",
                duplicateSetter.header
            );
        }

        const propertyType = node.typeIdentifier;

        if (getters.length > 0) {
            const getter = getters[0];

            const getterReturnType = getter.header.returnTypeIdentifier;

            if (
                !getterReturnType ||
                (propertyType.identifier.name.toLowerCase() !==
                    getterReturnType.identifier.name.toLowerCase() ||
                    propertyType.isArray !== getterReturnType.isArray)
            ) {
                this.addError(
                    "A 'get' function must return a value that is the same type as the property.",
                    getter.header
                );
            }
        }

        if (setters.length > 0) {
            const setter = setters[0];

            if (setter.header.returnTypeIdentifier) {
                this.addError(
                    "A 'set' function cannot return a value.",
                    setter.header.returnTypeIdentifier
                );
            }

            if (setter.header.parameters.parameters.length > 1) {
                this.addError(
                    "A 'set' function can only take a single parameter.",
                    setter.header
                );
            } else {
                const parameter = setter.header.parameters.parameters[0];

                if (
                    propertyType.identifier.name.toLowerCase() !==
                        parameter.typeIdentifier.identifier.name.toLowerCase() ||
                    propertyType.isArray !== parameter.typeIdentifier.isArray
                ) {
                    this.addError(
                        "A 'set' function must take a single parameter that is the same type as the property.",
                        setter.header
                    );
                }
            }
        }
    }
}

export function checkSyntax(node: Node, diagnostics: Diagnostics) {
    const visitor = new SyntaxCheckerVisitor(diagnostics);
    visitAll(node, visitor);
}
