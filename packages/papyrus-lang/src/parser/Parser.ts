import { Range } from '../common/Range';
import { createIterator, lastOfIterable } from '../common/Utilities';
import { Diagnostics } from '../Diagnostics';
import { Token } from '../tokenizer/Token';
import { TokenKind } from '../tokenizer/TokenKind';
import { TokenScanner } from '../tokenizer/TokenScanner';
import {
    BinaryOperationExpressionNode,
    BinaryOperatorType,
    DocCommentNode,
    DocumentableNode,
    EventIdentifierNode,
    ExpressionNode,
    FlagNode,
    Flags,
    FunctionCallExpressionParameterNode,
    IdentifierExpressionNode,
    IdentifierNode,
    MemberAccessExpressionNode,
    Node,
    NodeInterface,
    NodeKind,
    NodeObject,
    ScriptNode,
    tokenKindToAssignmentOperator,
    tokenKindToComparisonOperator,
    tokenKindToLanguageFlag,
    UnaryOperatorType,
} from './Node';
import { checkSyntax } from './SyntaxChecker';

import { visitTree, visitTreeNodesWhere } from '../common/TreeNode';
import { MemberTypes } from '../types/TypeChecker';

export class Parser {
    private _diagnostics: Diagnostics;
    private _tokens: TokenScanner;
    private _script: ScriptNode;

    public parse(tokens: Iterable<Token>, diagnostics: Diagnostics) {
        this._diagnostics = diagnostics;

        this._tokens = new TokenScanner(createIterator(tokens));

        this.parseScript();
        checkSyntax(this._script, this._diagnostics);

        return this._script;
    }

    private addError(
        message: string,
        range: Range = this._tokens.current.value.range
    ) {
        this._diagnostics.addError(message, range);
    }

    private parseNode<T extends NodeKind>(
        node: Node<T>,
        parse: (node: Node<T>) => void,
        parent: Node
    ) {
        const startToken = this._tokens.current.value;

        node.range = {
            start: startToken.range.start,
            end: null,
        };

        try {
            try {
                parse(node);
            } finally {
                node.range.end = this._tokens.current.value.range.end;
            }
        } catch (e) {
            this.addError(`${e.toString()}\r\n${e.stack}`, node.range);
        }

        node.parent = parent;

        return node;
    }

    private createAndParse<T extends NodeKind>(
        kind: T,
        parse: (node: Node<T>) => void,
        parent: Node
    ) {
        return this.parseNode(this.createNode(kind), parse, parent);
    }

    private createAndParseBlock<T extends NodeKind>(
        kind: T,
        blockEndTokenKind: TokenKind,
        parse: (node: Node<T>) => void,
        parent: Node
    ) {
        const startToken = this._tokens.current.value;

        const node = this.createNode(kind);
        node.parent = parent;

        node.range = {
            start: startToken.range.start,
            end: null,
        };

        try {
            try {
                parse(node);

                if (this._tokens.current.value.kind !== blockEndTokenKind) {
                    this._tokens.skipWhile((t) => t.kind !== blockEndTokenKind);
                }

                this._tokens.next();

                if (this._tokens.current.done) {
                    this.addError(
                        'Expected end block.',
                        this._tokens.peekPrevious().value.range
                    );
                }
            } finally {
                node.range.end = this._tokens.current.value.range.end;
            }
        } catch (e) {
            this.addError(`${e.toString()}\r\n${e.stack}`, node.range);
        }

        return node;
    }

    private createAndParseHeaderedBlock<T extends NodeKind>(
        kind: T,
        blockEndTokenKind: TokenKind,
        parseHeader: (node: Node<T>) => boolean,
        parseBody: (node: Node<T>) => void,
        parent: Node
    ) {
        const startToken = this._tokens.current.value;

        const node = this.createNode(kind);
        node.parent = parent;

        try {
            try {
                if (!parseHeader(node)) {
                    return node;
                }

                this._tokens.nextNonTriviaMultiline();

                parseBody(node);

                if (this._tokens.current.value.kind !== blockEndTokenKind) {
                    this._tokens.skipWhile((t) => t.kind !== blockEndTokenKind);
                }

                this._tokens.next();

                if (this._tokens.current.done) {
                    this.addError(
                        'Expected end block.',
                        this._tokens.peekPrevious().value.range
                    );
                }
            } finally {
                node.range = {
                    start: startToken.range.start,
                    end: this._tokens.current.value.range.end,
                };
            }
        } catch (e) {
            this.addError(`${e.toString()}\r\n${e.stack}`, node.range);
        }

        return node;
    }

    private createNode<TKind extends NodeKind>(kind: TKind): Node<TKind> {
        return (new NodeObject<TKind>(kind, this._script) as any) as Node<
            TKind
        >;
    }

    private parseScript() {
        this._tokens.nextNonTriviaMultiline();

        return this.createAndParse(
            NodeKind.Script,
            (node) => {
                node.script = node;
                this._script = node;

                node.header = this.parseScriptHeader(node);
                node.imports = [];
                node.definitions = [];

                node.customEvents = new Map();
                node.states = new Map();

                while (!this._tokens.done) {
                    this._tokens.nextNonTriviaMultiline();
                    const nextNodeKind = this.getNodeKindOfNextDefinition();

                    if (nextNodeKind == null) {
                        break;
                    }

                    switch (nextNodeKind) {
                        case NodeKind.Import:
                            node.imports.push(this.parseImport(node));
                            break;
                        case NodeKind.VariableDefinition:
                            node.definitions.push(
                                this.parseVariableDefinition(node)
                            );
                            break;
                        case NodeKind.PropertyDefinition:
                            node.definitions.push(
                                this.parsePropertyDefinition(node)
                            );
                            break;
                        case NodeKind.StructDefinition:
                            node.definitions.push(
                                this.parseStructDefinition(node)
                            );
                            break;
                        case NodeKind.GroupDefinition:
                            node.definitions.push(
                                this.parseGroupDefinition(node)
                            );
                            break;
                        case NodeKind.FunctionDefinition:
                            node.definitions.push(
                                this.parseFunctionDefinition(node)
                            );
                            break;
                        case NodeKind.StateDefinition:
                            node.definitions.push(
                                this.parseStateDefinition(node)
                            );
                            break;
                        case NodeKind.EventDefinition:
                            node.definitions.push(
                                this.parseEventDefinition(node)
                            );
                            break;
                        case NodeKind.CustomEventDefinition:
                            node.definitions.push(
                                this.parseCustomEventDefinition(node)
                            );
                            break;
                        default:
                    }
                }

                // Adding all referenced type names.
                node.identifiers = new Set<string>();
                for (const childNode of visitTree<Node>(node)) {
                    if (childNode.node.kind === NodeKind.Identifier) {
                        node.identifiers.add(childNode.node.name.toLowerCase());
                    }
                }
            },
            null
        );
    }

    private parseImport(parent: Node) {
        return this.createAndParse(
            NodeKind.Import,
            (node) => {
                this._tokens.nextNonTrivia();
                node.identifier = this.parseIdentifier(node);
            },
            parent
        );
    }

    private parsePropertyDefinition(parent: Node) {
        return this.createAndParseHeaderedBlock(
            NodeKind.PropertyDefinition,
            TokenKind.EndPropertyKeyword,
            (node) => {
                node.leadingDocComment = this.maybeParseLeadingDocComment(node);
                node.functions = [];

                node.typeIdentifier = this.parseTypeIdentifier(node);

                this._tokens.nextNonTrivia();
                this._tokens.nextNonTrivia();

                node.identifier = this.parseIdentifier(node);
                node.identifier.scopeMemberTypes = MemberTypes.Property;

                if (
                    this._tokens.peekNonTrivia().value.kind ===
                    TokenKind.EqualsToken
                ) {
                    this._tokens.nextNonTrivia();
                    this._tokens.nextNonTrivia();

                    node.initialValue = this.parseLiteral(node);
                }

                this._tokens.nextNonTrivia();

                node.flags = this.parseFlags(node);

                node.docComment = this.maybeParseDocComment(node);

                return !node.flags.some(
                    (f) =>
                        f.kind === NodeKind.LanguageFlag &&
                        (f.flag === Flags.Auto || f.flag === Flags.AutoReadOnly)
                );
            },
            (node) => {
                while (
                    !this._tokens.done &&
                    this._tokens.current.value.kind !==
                        TokenKind.EndPropertyKeyword
                ) {
                    const nextNodeKind = this.getNodeKindOfNextDefinition(
                        false
                    );
                    if (nextNodeKind === NodeKind.FunctionDefinition) {
                        node.functions.push(this.parseFunctionDefinition(node));
                    }
                    this._tokens.nextNonTriviaMultiline();
                }
            },
            parent
        );
    }

    private parseScriptHeader(parent: Node) {
        return this.createAndParse(
            NodeKind.ScriptHeader,
            (node) => {
                this.expectOneOfTokenKinds(
                    'Expected ScriptName declaration.',
                    TokenKind.ScriptNameKeyword
                );

                this._tokens.nextNonTrivia();

                this.expectOneOfTokenKinds(
                    'Expected script name.',
                    TokenKind.Identifier
                );

                node.identifier = this.parseIdentifier(node);

                if (!this._tokens.peekNonTrivia().value.isEndOfLogicalLine) {
                    this._tokens.nextNonTrivia();
                    if (
                        this._tokens.current.value.kind ===
                        TokenKind.ExtendsKeyword
                    ) {
                        this._tokens.nextNonTrivia();

                        this.expectOneOfTokenKinds(
                            'Expected extended script name.',
                            TokenKind.Identifier
                        );

                        node.extendedIdentifier = this.parseTypeIdentifier(
                            node,
                            false
                        );
                    }
                }

                if (!this._tokens.peekNonTrivia().value.isEndOfLogicalLine) {
                    node.flags = this.parseFlags(node);
                } else {
                    node.flags = [];
                }

                if (
                    this._tokens.peekNonTrivia().value.kind ===
                    TokenKind.NewLineTrivia
                ) {
                    this._tokens.next();
                }

                node.docComment = this.maybeParseDocComment(node);
            },
            parent
        );
    }

    private parseIdentifier(parent: Node) {
        return this.createAndParse(
            NodeKind.Identifier,
            (node) => {
                this.expectOneOfTokenKinds(
                    'Expected identifier.',
                    TokenKind.Identifier
                );

                node.name = this._tokens.current.value.text;
            },
            parent
        );
    }

    private parseFlags(parent: Node) {
        const flags: FlagNode[] = [];

        while (!this._tokens.current.value.isEndOfLogicalLine) {
            this.expectToken(
                'Expected flag name.',
                (t) => t.isFlag || t.kind === TokenKind.Identifier
            );

            const flag = this.createAndParse(
                this._tokens.current.value.isFlag
                    ? NodeKind.LanguageFlag
                    : NodeKind.UserFlag,
                (node) => {
                    if (node.kind === NodeKind.UserFlag) {
                        node.name = this._tokens.current.value.text;
                    } else {
                        node.flag = tokenKindToLanguageFlag.get(
                            this._tokens.current.value.kind
                        );
                    }
                },
                parent
            );

            this._tokens.nextNonTrivia();

            flags.push(flag);
        }

        return flags;
    }

    private parseVariableDefinition(parent: Node) {
        return this.createAndParse(
            NodeKind.VariableDefinition,
            (node) => {
                node.typeIdentifier = this.parseTypeIdentifier(node);

                this._tokens.nextNonTrivia();

                node.identifier = this.parseIdentifier(node);

                if (
                    this._tokens.peekNonTrivia().value.kind ===
                    TokenKind.EqualsToken
                ) {
                    this._tokens.nextNonTrivia();
                    this._tokens.nextNonTrivia();

                    node.initialValue = this.parseLiteral(node);
                }

                this._tokens.nextNonTrivia();

                node.flags = this.parseFlags(node);
            },
            parent
        );
    }

    private parseFunctionDefinition(parent: Node) {
        return this.createAndParseHeaderedBlock(
            NodeKind.FunctionDefinition,
            TokenKind.EndFunctionKeyword,
            (node) => {
                node.header = this.parseFunctionHeader(node);

                return !node.header.flags.some(
                    (f) =>
                        f.kind === NodeKind.LanguageFlag &&
                        f.flag === Flags.Native
                );
            },
            (node) => {
                node.statements = [];

                let statement = this.parseStatement(node);
                while (statement) {
                    node.statements.push(statement);
                    this._tokens.nextNonTriviaMultiline();
                    statement = this.parseStatement(node);
                }
            },
            parent
        );
    }

    private parseFunctionHeader(parent: Node) {
        return this.createAndParse(
            NodeKind.FunctionHeader,
            (node) => {
                node.leadingDocComment = this.maybeParseLeadingDocComment(node);

                if (this._tokens.current.value.kind === TokenKind.Identifier) {
                    node.returnTypeIdentifier = this.parseTypeIdentifier(node);
                    this._tokens.nextNonTrivia();
                }

                this.expectOneOfTokenKinds(
                    "Expected 'function' keyword",
                    TokenKind.FunctionKeyword
                );

                this._tokens.nextNonTrivia();
                this.expectOneOfTokenKinds(
                    'Expected function identifier',
                    TokenKind.Identifier
                );

                node.identifier = this.parseIdentifier(node);
                node.identifier.scopeMemberTypes = MemberTypes.Function;

                this._tokens.nextNonTrivia();
                node.parameters = this.parseFunctionParameters(node);

                this._tokens.nextNonTrivia();
                node.flags = this.parseFlags(node);

                node.docComment = this.maybeParseDocComment(node);
            },
            parent
        );
    }

    private parseEventHeader(parent: Node) {
        return this.createAndParse(
            NodeKind.EventHeader,
            (node) => {
                node.leadingDocComment = this.maybeParseLeadingDocComment(node);

                this.expectOneOfTokenKinds(
                    "Expected 'event' keyword",
                    TokenKind.EventKeyword
                );

                this._tokens.nextNonTrivia();

                node.identifier = this.createAndParse(
                    NodeKind.EventIdentifier,
                    (identifierNode) => {
                        this.createAndParse(
                            NodeKind.Identifier,
                            (compositeIdentifier) => {
                                let typeIdentifier: IdentifierNode = null;

                                if (
                                    this._tokens.peekNonTrivia().value.kind ===
                                    TokenKind.DotToken
                                ) {
                                    typeIdentifier = this.parseIdentifier(
                                        identifierNode
                                    );

                                    this._tokens.nextNonTrivia();
                                    this._tokens.nextNonTrivia();
                                }

                                this.expectOneOfTokenKinds(
                                    'Expected event identifier',
                                    TokenKind.Identifier
                                );

                                const identifier = this.parseIdentifier(
                                    identifierNode
                                );

                                let expression:
                                    | IdentifierExpressionNode
                                    | MemberAccessExpressionNode = null;

                                if (typeIdentifier) {
                                    expression = this.createAndParse(
                                        NodeKind.MemberAccessExpression,
                                        (expressionNode) => {
                                            expressionNode.baseExpression = this.createAndParse(
                                                NodeKind.IdentifierExpression,
                                                (baseExpression) => {
                                                    typeIdentifier.parent = baseExpression;
                                                    baseExpression.identifier = typeIdentifier;
                                                },
                                                expressionNode
                                            );

                                            expressionNode.accessExpression = this.createAndParse(
                                                NodeKind.IdentifierExpression,
                                                (accessExpression) => {
                                                    identifier.parent = accessExpression;
                                                    accessExpression.identifier = identifier;

                                                    accessExpression.scopeMemberTypes =
                                                        MemberTypes.CustomEvent;
                                                },
                                                expressionNode
                                            );
                                        },
                                        compositeIdentifier
                                    );
                                } else {
                                    expression = this.createAndParse(
                                        NodeKind.IdentifierExpression,
                                        (accessExpression) => {
                                            identifier.parent = accessExpression;
                                            accessExpression.identifier = identifier;
                                        },
                                        compositeIdentifier
                                    );
                                }

                                identifierNode.eventIdentifierExpression = expression;

                                compositeIdentifier.name = Array.from(
                                    visitTreeNodesWhere<NodeInterface>(
                                        expression,
                                        (child) =>
                                            child.node.kind ===
                                            NodeKind.Identifier
                                    )
                                )
                                    .map(
                                        (id) => (id.node as IdentifierNode).name
                                    )
                                    .join('.');

                                identifierNode.identifier = compositeIdentifier;
                            },
                            identifierNode
                        );
                    },
                    node
                );

                this._tokens.nextNonTrivia();
                node.parameters = this.parseFunctionParameters(node);

                node.docComment = this.maybeParseDocComment(node);
            },
            parent
        );
    }

    private parseTypeIdentifier(parent: Node, allowArray: boolean = true) {
        return this.createAndParse(
            NodeKind.TypeIdentifier,
            (node) => {
                node.isArray = false;
                node.identifier = this.parseIdentifier(node);
                node.scopeMemberTypes = MemberTypes.Struct;

                const following = this._tokens.peekNonTrivia();
                if (following.value.kind === TokenKind.ArrayToken) {
                    node.isArray = true;
                    this._tokens.nextNonTrivia();
                }

                if (node.isArray && !allowArray) {
                    this.addError('Invalid array definition.');
                }
            },
            parent
        );
    }

    private parseFunctionParameters(parent: Node) {
        return this.createAndParse(
            NodeKind.FunctionParameters,
            (node) => {
                node.parameters = [];

                this.expectOneOfTokenKinds(
                    "Expected '('.",
                    TokenKind.OpenParenToken
                );

                this._tokens.nextNonTrivia();
                while (
                    this._tokens.current.value.kind !==
                    TokenKind.CloseParenToken
                ) {
                    node.parameters.push(this.parseFunctionParameter(node));

                    if (
                        this._tokens.nextNonTrivia().value.kind !==
                        TokenKind.CommaToken
                    ) {
                        break;
                    }

                    this._tokens.nextNonTrivia();
                }

                this.expectOneOfTokenKinds(
                    "Expected ')'.",
                    TokenKind.CloseParenToken
                );
            },
            parent
        );
    }

    private parseFunctionParameter(parent: Node) {
        return this.createAndParse(
            NodeKind.FunctionParameter,
            (node) => {
                const typeIdentifierToken = this._tokens.current;
                if (typeIdentifierToken.value.kind !== TokenKind.Identifier) {
                    this.addError('Expected parameter type identifier.');
                    return;
                }

                node.typeIdentifier = this.parseTypeIdentifier(node);

                this._tokens.nextNonTrivia();
                this.expectOneOfTokenKinds(
                    'Expected parameter identifier.',
                    TokenKind.Identifier
                );
                node.identifier = this.parseIdentifier(node);

                if (
                    this._tokens.peekNonTrivia().value.kind ===
                    TokenKind.EqualsToken
                ) {
                    this._tokens.nextNonTrivia();
                    this._tokens.nextNonTrivia();
                    node.defaultValue = this.parseLiteral(node);
                    node.isOptional = true;
                }
            },
            parent
        );
    }

    private parseLiteral(parent: Node) {
        const literalToken = this._tokens.current;

        switch (literalToken.value.kind) {
            case TokenKind.MinusToken:
                switch (this._tokens.peekNonTrivia().value.kind) {
                    case TokenKind.IntLiteral:
                        return this.parseIntLiteral(parent);
                    case TokenKind.FloatLiteral:
                        return this.parseFloatLiteral(parent);
                    default:
                        this.addError(
                            'Expected an integer or floating point literal.'
                        );
                }
                break;
            case TokenKind.IntLiteral:
            case TokenKind.HexLiteral:
                return this.parseIntLiteral(parent);
            case TokenKind.TrueKeyword:
            case TokenKind.FalseKeyword:
                return this.parseBoolLiteral(parent);
            case TokenKind.FloatLiteral:
                return this.parseFloatLiteral(parent);
            case TokenKind.DoubleQuoteToken:
                return this.parseStringLiteral(parent);
            case TokenKind.NoneKeyword:
                return this.createAndParse(
                    NodeKind.NoneLiteral,
                    () => {},
                    parent
                );
            default:
                this.addError('Expected a literal constant value.');
        }

        return null;
    }

    private parseIntLiteral(parent: Node) {
        return this.createAndParse(
            NodeKind.IntLiteral,
            (node) => {
                let isNegative = false;

                if (this._tokens.current.value.kind === TokenKind.MinusToken) {
                    this._tokens.nextNonTrivia();
                    isNegative = true;
                }

                const value = parseInt(this._tokens.current.value.text);

                node.value = isNegative ? -value : value;
            },
            parent
        );
    }

    private parseFloatLiteral(parent: Node) {
        return this.createAndParse(
            NodeKind.FloatLiteral,
            (node) => {
                let isNegative = false;

                if (this._tokens.current.value.kind === TokenKind.MinusToken) {
                    this._tokens.nextNonTrivia();
                    isNegative = true;
                }

                const value = parseFloat(this._tokens.current.value.text);

                node.value = isNegative ? -value : value;
            },
            parent
        );
    }

    private parseBoolLiteral(parent: Node) {
        return this.createAndParse(
            NodeKind.BoolLiteral,
            (node) => {
                node.value =
                    this._tokens.current.value.text.toLowerCase() === 'true';
            },
            parent
        );
    }

    private parseStringLiteral(parent: Node) {
        return this.createAndParse(
            NodeKind.StringLiteral,
            (node) => {
                node.value = '';

                if (
                    this._tokens.peekNonTrivia().value.kind ===
                    TokenKind.DoubleQuoteToken
                ) {
                    this._tokens.nextNonTrivia();
                    return;
                }

                while (
                    this._tokens.peekNonTrivia().value.kind ===
                    TokenKind.StringLiteralContent
                ) {
                    node.value += this._tokens.nextNonTrivia().value.text;
                }

                this._tokens.nextNonTrivia();
            },
            parent
        );
    }

    private parseGroupDefinition(parent: Node) {
        return this.createAndParseBlock(
            NodeKind.GroupDefinition,
            TokenKind.EndGroupKeyword,
            (node) => {
                this._tokens.nextNonTrivia();
                node.identifier = this.parseIdentifier(node);

                if (!this._tokens.peekNonTrivia().value.isEndOfLogicalLine) {
                    node.flags = this.parseFlags(node);
                } else {
                    node.flags = [];
                }

                node.properties = [];

                while (
                    !this._tokens.done &&
                    this._tokens.current.value.kind !==
                        TokenKind.EndGroupKeyword
                ) {
                    this._tokens.nextNonTriviaMultiline();
                    const nextNodeKind = this.getNodeKindOfNextDefinition(
                        false
                    );

                    if (nextNodeKind === NodeKind.PropertyDefinition) {
                        node.properties.push(
                            this.parsePropertyDefinition(node)
                        );
                    }
                }
            },
            parent
        );
    }

    private parseStateDefinition(parent: Node) {
        return this.createAndParseBlock(
            NodeKind.StateDefinition,
            TokenKind.EndStateKeyword,
            (node) => {
                if (this._tokens.current.value.kind === TokenKind.AutoKeyword) {
                    this._tokens.nextNonTrivia();
                    node.isAuto = true;
                }

                this._tokens.nextNonTrivia();
                node.identifier = this.parseIdentifier(node);

                node.definitions = [];

                while (
                    !this._tokens.done &&
                    this._tokens.current.value.kind !==
                        TokenKind.EndStateKeyword
                ) {
                    this._tokens.nextNonTriviaMultiline();
                    const nextNodeKind = this.getNodeKindOfNextDefinition(
                        false
                    );

                    if (nextNodeKind === NodeKind.FunctionDefinition) {
                        node.definitions.push(
                            this.parseFunctionDefinition(node)
                        );
                    } else if (nextNodeKind === NodeKind.EventDefinition) {
                        node.definitions.push(this.parseEventDefinition(node));
                    }
                }
            },
            parent
        );
    }

    private parseStructDefinition(parent: Node) {
        return this.createAndParseBlock(
            NodeKind.StructDefinition,
            TokenKind.EndStructKeyword,
            (node) => {
                this._tokens.nextNonTrivia();
                node.identifier = this.parseIdentifier(node);

                node.members = [];

                while (
                    !this._tokens.done &&
                    this._tokens.current.value.kind !==
                        TokenKind.EndStructKeyword
                ) {
                    this._tokens.nextNonTriviaMultiline();
                    const nextNodeKind = this.getNodeKindOfNextDefinition(
                        false
                    );

                    if (nextNodeKind === NodeKind.VariableDefinition) {
                        node.members.push(this.parseVariableDefinition(node));
                    }
                }
            },
            parent
        );
    }

    private parseEventDefinition(parent: Node) {
        return this.createAndParseBlock(
            NodeKind.EventDefinition,
            TokenKind.EndEventKeyword,
            (node) => {
                node.header = this.parseEventHeader(node);

                this._tokens.nextNonTriviaMultiline();

                node.statements = [];

                let statement = this.parseStatement(node);
                while (statement) {
                    node.statements.push(statement);
                    this._tokens.nextNonTriviaMultiline();
                    statement = this.parseStatement(node);
                }
            },
            parent
        );
    }

    private parseCustomEventDefinition(parent: Node) {
        return this.createAndParse(
            NodeKind.CustomEventDefinition,
            (node) => {
                this._tokens.nextNonTrivia();
                node.identifier = this.parseIdentifier(node);
            },
            parent
        );
    }

    private parseStatement(parent: Node) {
        if (this._tokens.done) {
            return null;
        }

        switch (this._tokens.current.value.kind) {
            case TokenKind.ReturnKeyword:
                return this.parseReturnStatement(parent);
            case TokenKind.WhileKeyword:
                return this.parseWhileStatement(parent);
            case TokenKind.IfKeyword:
                return this.parseIfStatement(parent);
            case TokenKind.Identifier:
                if (
                    this._tokens.current.value.kind === TokenKind.Identifier &&
                    this._tokens.doLookahead(() => {
                        if (
                            this._tokens.peekNonTrivia().value.kind ===
                                TokenKind.Identifier ||
                            this._tokens.peekNonTrivia().value.kind ===
                                TokenKind.ArrayToken
                        ) {
                            return true;
                        }

                        return false;
                    })
                ) {
                    return this.parseDeclareStatement(parent);
                }
            case TokenKind.OpenParenToken:
                const expression = this.parseExpression(parent);

                if (this._tokens.peekNonTrivia().value.isAssignmentOperator) {
                    this._tokens.nextNonTrivia();

                    return this.createAndParse(
                        NodeKind.AssignmentStatement,
                        (node) => {
                            node.leftValue = expression;
                            node.leftValue.scopeMemberTypes =
                                MemberTypes.Variable | MemberTypes.Property;

                            node.leftValue.parent = node;
                            if (node.range.start > node.leftValue.range.start) {
                                node.range.start = node.leftValue.range.start;
                            }

                            node.operation = tokenKindToAssignmentOperator.get(
                                this._tokens.current.value.kind
                            );

                            this._tokens.nextNonTrivia();
                            node.rightValue = this.parseExpression(node);
                        },
                        parent
                    );
                } else {
                    return this.createAndParse(
                        NodeKind.ExpressionStatement,
                        (node) => {
                            node.expression = expression;
                            node.expression.parent = node;
                            if (
                                node.range.start > node.expression.range.start
                            ) {
                                node.range.start = node.expression.range.start;
                            }
                        },
                        parent
                    );
                }
            default:
                return null;
        }
    }

    private parseReturnStatement(parent: Node) {
        return this.createAndParse(
            NodeKind.ReturnStatement,
            (node) => {
                this._tokens.nextNonTrivia();

                if (this._tokens.current.value.isEndOfLogicalLine) {
                    return;
                }

                node.returnValue = this.parseExpression(node);
            },
            parent
        );
    }

    private parseWhileStatement(parent: Node) {
        return this.createAndParseBlock(
            NodeKind.WhileStatement,
            TokenKind.EndWhileKeyword,
            (node) => {
                this._tokens.nextNonTrivia();

                node.expression = this.parseExpression(node);

                this._tokens.nextNonTriviaMultiline();

                node.statements = [];

                let statement = this.parseStatement(node);
                while (statement) {
                    node.statements.push(statement);
                    this._tokens.nextNonTriviaMultiline();
                    statement = this.parseStatement(node);
                }
            },
            parent
        );
    }

    private parseIfStatement(parent: Node) {
        return this.createAndParseBlock(
            NodeKind.IfStatement,
            TokenKind.EndIfKeyword,
            (node) => {
                node.bodies = [];

                while (
                    !this._tokens.current.done &&
                    this._tokens.current.value.kind !== TokenKind.EndIfKeyword
                ) {
                    node.bodies.push(
                        this.createAndParse(
                            NodeKind.IfStatementBody,
                            (bodyNode) => {
                                if (
                                    this._tokens.current.value.kind !==
                                    TokenKind.ElseKeyword
                                ) {
                                    this._tokens.nextNonTrivia();

                                    bodyNode.condition = this.parseExpression(
                                        bodyNode
                                    );
                                }

                                this._tokens.nextNonTriviaMultiline();

                                bodyNode.statements = [];

                                let statement = this.parseStatement(bodyNode);
                                while (statement) {
                                    bodyNode.statements.push(statement);
                                    this._tokens.nextNonTriviaMultiline();
                                    statement = this.parseStatement(bodyNode);
                                }
                            },
                            node
                        )
                    );
                }
            },
            parent
        );
    }

    private parseDeclareStatement(parent: Node) {
        return this.createAndParse(
            NodeKind.DeclareStatement,
            (node) => {
                node.typeIdentifier = this.parseTypeIdentifier(node);
                this._tokens.nextNonTrivia();
                node.identifier = this.parseIdentifier(node);
                this._tokens.nextNonTrivia();
                if (this._tokens.current.value.kind === TokenKind.EqualsToken) {
                    this._tokens.nextNonTrivia();
                    node.initialValue = this.parseExpression(node);
                }
            },
            parent
        );
    }

    private parseExpression(parent: Node): ExpressionNode {
        let expression = this.parseAndExpression(parent);
        while (
            this._tokens.peekNonTrivia().value.kind === TokenKind.BarBarToken
        ) {
            this._tokens.nextNonTrivia();
            this._tokens.nextNonTrivia();

            expression = this.parseBinaryOperationExpression(
                BinaryOperatorType.BooleanOr,
                (p) => {
                    expression.parent = p;
                    return expression;
                },
                (p) => this.parseAndExpression(p),
                parent
            );
        }
        return expression;
    }

    private parseAndExpression(parent: Node): ExpressionNode {
        let expression = this.parseCompareExpression(parent);
        while (
            this._tokens.peekNonTrivia().value.kind ===
            TokenKind.AmpersandAmpersandToken
        ) {
            this._tokens.nextNonTrivia();
            this._tokens.nextNonTrivia();

            expression = this.parseBinaryOperationExpression(
                BinaryOperatorType.BooleanAnd,
                (p) => {
                    expression.parent = p;
                    return expression;
                },
                (p) => this.parseCompareExpression(p),
                parent
            );
        }
        return expression;
    }

    private parseCompareExpression(parent: Node) {
        let expression = this.parseAddExpression(parent);

        while (this._tokens.peekNonTrivia().value.isComparisonOperator) {
            this._tokens.nextNonTrivia();

            const op = tokenKindToComparisonOperator.get(
                this._tokens.current.value.kind
            );

            this._tokens.nextNonTrivia();

            expression = this.parseBinaryOperationExpression(
                op,
                (p) => {
                    expression.parent = p;
                    return expression;
                },
                (p) => this.parseAddExpression(p),
                parent
            );
        }

        return expression;
    }

    private parseAddExpression(parent: Node) {
        let expression = this.parseMultiplyExpression(parent);

        while (true) {
            let op = BinaryOperatorType.None;

            if (
                this._tokens.peekNonTrivia().value.kind === TokenKind.PlusToken
            ) {
                op = BinaryOperatorType.Add;
            } else if (
                this._tokens.peekNonTrivia().value.kind === TokenKind.MinusToken
            ) {
                op = BinaryOperatorType.Subtract;
            } else {
                break;
            }

            this._tokens.nextNonTrivia();
            this._tokens.nextNonTrivia();

            expression = this.parseBinaryOperationExpression(
                op,
                (p) => {
                    expression.parent = p;
                    return expression;
                },
                (p) => this.parseMultiplyExpression(p),
                parent
            );
        }

        return expression;
    }

    private parseMultiplyExpression(parent: Node) {
        let expression = this.parseUnaryExpression(parent);

        while (true) {
            let op = BinaryOperatorType.None;

            if (
                this._tokens.peekNonTrivia().value.kind ===
                TokenKind.AsteriskToken
            ) {
                op = BinaryOperatorType.Multiply;
            } else if (
                this._tokens.peekNonTrivia().value.kind === TokenKind.SlashToken
            ) {
                op = BinaryOperatorType.Divide;
            } else if (
                this._tokens.peekNonTrivia().value.kind ===
                TokenKind.PercentToken
            ) {
                op = BinaryOperatorType.Modulus;
            } else {
                break;
            }

            this._tokens.nextNonTrivia();
            this._tokens.nextNonTrivia();

            expression = this.parseBinaryOperationExpression(
                op,
                (p) => {
                    expression.parent = p;
                    return expression;
                },
                (p) => this.parseUnaryExpression(p),
                parent
            );
        }

        return expression;
    }

    private parseUnaryExpression(parent: Node) {
        let op = UnaryOperatorType.None;

        if (this._tokens.current.value.kind === TokenKind.ExclamationToken) {
            op = UnaryOperatorType.Not;
        } else if (this._tokens.current.value.kind === TokenKind.MinusToken) {
            op = UnaryOperatorType.Negate;
        }

        if (op === UnaryOperatorType.None) {
            return this.parseCastExpression(parent);
        } else {
            return this.createAndParse(
                NodeKind.UnaryOperationExpression,
                (node) => {
                    this._tokens.nextNonTrivia();
                    node.operator = op;
                    node.innerExpression = this.parseCastExpression(node);
                },
                parent
            );
        }
    }

    private parseCastExpression(parent: Node) {
        let expression = this.parseDotExpression(parent);

        const nodeKind =
            this._tokens.peekNonTrivia().value.kind === TokenKind.IsKeyword
                ? NodeKind.IsExpression
                : this._tokens.peekNonTrivia().value.kind ===
                  TokenKind.AsKeyword
                    ? NodeKind.CastExpression
                    : null;

        if (nodeKind !== null) {
            expression = this.createAndParse(
                nodeKind,
                (node) => {
                    this._tokens.nextNonTrivia();
                    this._tokens.nextNonTrivia();

                    node.innerExpression = expression;
                    node.innerExpression.parent = node;
                    if (node.range.start > node.innerExpression.range.start) {
                        node.range.start = node.innerExpression.range.start;
                    }

                    node.typeIdentifier = this.parseTypeIdentifier(node);
                },
                parent
            );
        }

        return expression;
    }

    private parseDotExpression(parent: Node) {
        if (this._tokens.current.value.isPossibleLiteral) {
            return this.createAndParse(
                NodeKind.LiteralExpression,
                (node) => {
                    node.value = this.parseLiteral(node);
                },
                parent
            );
        }

        let expression = this.parseArrayExpression(parent);
        while (this._tokens.peekNonTrivia().value.kind === TokenKind.DotToken) {
            this._tokens.nextNonTrivia();

            const memberAccessExpression = this.createAndParse(
                NodeKind.MemberAccessExpression,
                (node) => {
                    this._tokens.nextNonTrivia();

                    node.baseExpression = expression;
                    node.baseExpression.parent = node;
                    if (node.range.start > node.baseExpression.range.start) {
                        node.range.start = node.baseExpression.range.start;
                    }

                    node.accessExpression = this.parseFunctionOrIdentifierExpression(
                        node
                    );
                },
                parent
            );

            if (
                this._tokens.peekNonTrivia().value.kind ===
                TokenKind.OpenBracketToken
            ) {
                this._tokens.nextNonTrivia();

                expression = this.createAndParse(
                    NodeKind.ArrayIndexExpression,
                    (node) => {
                        node.baseExpression = memberAccessExpression;
                        node.baseExpression.parent = node;
                        if (
                            node.range.start > node.baseExpression.range.start
                        ) {
                            node.range.start = node.baseExpression.range.start;
                        }

                        this._tokens.nextNonTrivia();
                        node.indexExpression = this.parseExpression(node);
                        this._tokens.nextNonTrivia();
                        this.expectOneOfTokenKinds(
                            "Expected ']'.",
                            TokenKind.CloseBracketToken
                        );
                    },
                    parent
                );
            } else {
                expression = memberAccessExpression;
            }
        }
        return expression;
    }

    private parseArrayExpression(parent: Node) {
        let expression = this.parseAtomExpression(parent);

        if (
            this._tokens.peekNonTrivia().value.kind ===
            TokenKind.OpenBracketToken
        ) {
            this._tokens.nextNonTrivia();

            expression = this.createAndParse(
                NodeKind.ArrayIndexExpression,
                (node) => {
                    node.baseExpression = expression;
                    node.baseExpression.parent = node;
                    if (node.range.start > node.baseExpression.range.start) {
                        node.range.start = node.baseExpression.range.start;
                    }

                    this._tokens.nextNonTrivia();
                    node.indexExpression = this.parseExpression(node);
                    this._tokens.nextNonTrivia();
                    this.expectOneOfTokenKinds(
                        "Expected ']'.",
                        TokenKind.CloseBracketToken
                    );
                },
                parent
            );
        }

        return expression;
    }

    private parseFunctionOrIdentifierExpression(parent: Node) {
        if (
            !this.expectOneOfTokenKinds(
                'Identifier expected.',
                TokenKind.Identifier
            )
        ) {
            return null;
        }

        if (
            this._tokens.peekNonTrivia().value.kind === TokenKind.OpenParenToken
        ) {
            return this.createAndParse(
                NodeKind.FunctionCallExpression,
                (node) => {
                    node.identifier = this.parseIdentifier(node);
                    node.identifier.scopeMemberTypes =
                        MemberTypes.Function | MemberTypes.Event;
                    this._tokens.nextNonTrivia();

                    node.parameters = this.parseFunctionCallParameters(node);
                },
                parent
            );
        } else {
            return this.createAndParse(
                NodeKind.IdentifierExpression,
                (node) => {
                    node.identifier = this.parseIdentifier(node);
                    node.identifier.scopeMemberTypes =
                        MemberTypes.Property | MemberTypes.Variable;
                },
                parent
            );
        }
    }

    private parseFunctionCallParameters(parent: Node) {
        const parameters: FunctionCallExpressionParameterNode[] = [];
        this._tokens.nextNonTrivia();

        while (this._tokens.current.value.kind !== TokenKind.CloseParenToken) {
            parameters.push(this.parseFunctionCallParameter(parent));

            this._tokens.nextNonTrivia();

            if (this._tokens.current.value.kind !== TokenKind.CommaToken) {
                break;
            }

            this._tokens.nextNonTrivia();
        }

        this.expectOneOfTokenKinds("Expected ')'.", TokenKind.CloseParenToken);

        return parameters;
    }

    private parseFunctionCallParameter(parent: Node) {
        return this.createAndParse(
            NodeKind.FunctionCallExpressionParameter,
            (node) => {
                if (
                    this._tokens.peekNonTrivia().value.kind ===
                    TokenKind.EqualsToken
                ) {
                    node.identifier = this.parseIdentifier(node);
                    this._tokens.nextNonTrivia();
                    this._tokens.nextNonTrivia();
                }

                node.value = this.parseExpression(node);
            },
            parent
        );
    }

    private parseAtomExpression(parent: Node) {
        if (this._tokens.current.value.kind === TokenKind.OpenParenToken) {
            this._tokens.nextNonTrivia();
            const expression = this.parseExpression(parent);
            this._tokens.nextNonTrivia();
            this.expectOneOfTokenKinds(
                "Expected ')'.",
                TokenKind.CloseParenToken
            );
            return expression;
        }

        if (this._tokens.current.value.kind === TokenKind.NewKeyword) {
            this._tokens.nextNonTrivia();

            const nextToken = this._tokens.peekNonTrivia();

            const isArray = nextToken.value.kind === TokenKind.OpenBracketToken;

            if (nextToken.value.kind === TokenKind.ArrayToken) {
                this.addError(
                    'Expected array length expression.',
                    nextToken.value.range
                );
            }

            if (isArray) {
                return this.parseNewArrayExpression(parent);
            }

            return this.parseNewStructExpression(parent);
        }

        return this.parseFunctionOrIdentifierExpression(parent);
    }

    private parseNewArrayExpression(parent: Node) {
        return this.createAndParse(
            NodeKind.NewArrayExpression,
            (node) => {
                node.arrayType = this.parseTypeIdentifier(node);
                this._tokens.nextNonTrivia();
                this._tokens.nextNonTrivia();

                node.lengthExpression = this.parseExpression(node);

                if (!node.lengthExpression) {
                    this.addError(
                        'Expected array length expression.',
                        node.range
                    );
                }

                this._tokens.nextNonTrivia();
                this.expectOneOfTokenKinds(
                    "Expected ']'.",
                    TokenKind.CloseBracketToken
                );
            },
            parent
        );
    }

    private parseNewStructExpression(parent: Node) {
        return this.createAndParse(
            NodeKind.NewStructExpression,
            (node) => {
                node.structType = this.parseTypeIdentifier(node);
            },
            parent
        );
    }

    private parseBinaryOperationExpression(
        operator: BinaryOperatorType,
        left: (parent: BinaryOperationExpressionNode) => ExpressionNode,
        right: (parent: BinaryOperationExpressionNode) => ExpressionNode,
        parent: Node
    ) {
        return this.createAndParse(
            NodeKind.BinaryOperationExpression,
            (node) => {
                node.operator = operator;
                node.left = left(node);
                if (node.range.start > node.left.range.start) {
                    node.range.start = node.left.range.start;
                }

                node.right = right(node);
            },
            parent
        );
    }

    private getNodeKindOfNextDefinition(reportMissingAsError: boolean = true) {
        return this._tokens.doLookahead(() => {
            const definition = this._tokens.current;
            if (definition.done) {
                return null;
            }

            switch (definition.value.kind) {
                case TokenKind.Identifier:
                    let following = this._tokens.nextNonTrivia();
                    if (following.value.kind === TokenKind.ArrayToken) {
                        following = this._tokens.nextNonTrivia();
                    }

                    switch (following.value.kind) {
                        case TokenKind.Identifier:
                            return NodeKind.VariableDefinition;
                        case TokenKind.PropertyKeyword:
                            return NodeKind.PropertyDefinition;
                        case TokenKind.FunctionKeyword:
                            return NodeKind.FunctionDefinition;
                        default:
                            this.addError(
                                'Expected an identifier, property or function definition.'
                            );
                            return null;
                    }
                case TokenKind.GroupKeyword:
                    return NodeKind.GroupDefinition;
                case TokenKind.FunctionKeyword:
                    return NodeKind.FunctionDefinition;
                case TokenKind.AutoKeyword:
                case TokenKind.StateKeyword:
                    return NodeKind.StateDefinition;
                case TokenKind.EventKeyword:
                    return NodeKind.EventDefinition;
                case TokenKind.ImportKeyword:
                    return NodeKind.Import;
                case TokenKind.StructKeyword:
                    return NodeKind.StructDefinition;
                case TokenKind.CustomEventKeyword:
                    return NodeKind.CustomEventDefinition;
                default:
                    if (reportMissingAsError) {
                        this.addError('Expected definition.');
                    }
                    return null;
            }
        });
    }

    private expectToken(message: string, expectFn: (token: Token) => boolean) {
        if (this._tokens.done || !expectFn(this._tokens.current.value)) {
            this.addError(message);
            return false;
        }

        return true;
    }

    private expectOneOfTokenKinds(message: string, ...kinds: TokenKind[]) {
        if (!kinds.some((k) => this._tokens.current.value.kind === k)) {
            this.addError(message);
            return false;
        }

        return true;
    }

    private maybeParseDocComment(parent: DocumentableNode) {
        return this._tokens.doLookahead(() => {
            this._tokens.nextNonTriviaMultiline(true);
            let docComment: DocCommentNode = null;

            if (this._tokens.current.value.kind === TokenKind.OpenBraceToken) {
                docComment = this.createAndParse(
                    NodeKind.DocComment,
                    (node) => {
                        this._tokens.next();

                        node.text = Array.from(
                            this._tokens.nextWhile(
                                (t) =>
                                    t.kind ===
                                    TokenKind.DocumentationCommentTrivia
                            )
                        )
                            .map((t) => t.value.text)
                            .join('');
                    },
                    parent
                );
            }

            return docComment;
        });
    }

    private maybeParseLeadingDocComment(
        parent: DocumentableNode
    ): DocCommentNode {
        const commentNode = this._tokens.doLookahead(() => {
            let leadingText = '';

            this._tokens.previous();

            while (
                (this._tokens.current.value.isWhitespace ||
                    this._tokens.current.value.isComment) &&
                this._tokens.current.value.kind !==
                    TokenKind.DocumentationCommentTrivia &&
                this._tokens.current.value.kind !== TokenKind.CloseBraceToken
            ) {
                if (
                    this._tokens.current.value.kind === TokenKind.SemicolonToken
                ) {
                    this._tokens.previous();
                    continue;
                }

                if (
                    this._tokens.current.value.kind === TokenKind.NewLineTrivia
                ) {
                    leadingText = `\r\n${leadingText}`;
                } else {
                    leadingText = this._tokens.current.value.text + leadingText;
                }

                this._tokens.previous();
            }

            leadingText = lastOfIterable(leadingText.split('\r\n\r\n'));

            if (leadingText.trim().length === 0) {
                return null;
            }

            return this.createAndParse(
                NodeKind.DocComment,
                (node) => {
                    node.text = leadingText.trimRight();
                },
                parent
            );
        });

        if (commentNode) {
            commentNode.range.end = this._tokens.current.value.range.end;
            return commentNode;
        }

        return null;
    }
}
