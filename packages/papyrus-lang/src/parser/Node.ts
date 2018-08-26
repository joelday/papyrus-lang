import { Range } from '../common/Range';
import { visitTreeNodesWhere } from '../common/TreeNode';
import { lastOfIterable } from '../common/Utilities';
import { ScriptFile } from '../program/ScriptFile';
import {
    CustomEventSymbol,
    EventSymbol,
    FunctionSymbol,
    GroupSymbol,
    ImportSymbol,
    ParameterSymbol,
    PropertySymbol,
    ScriptSymbol,
    StateSymbol,
    StructSymbol,
    SymbolInterface,
    SymbolKind,
    SymbolTable,
    VariableSymbol,
} from '../symbols/Symbol';
import { TokenKind } from '../tokenizer/TokenKind';
import { MemberTypes } from '../types/TypeChecker';

export enum NodeKind {
    ArrayIndexExpression = 'ArrayIndexExpression',
    AssignmentStatement = 'AssignmentStatement',
    BinaryOperationExpression = 'BinaryOperationExpression',
    BoolLiteral = 'BoolLiteral',
    CastExpression = 'CastExpression',
    CustomEventDefinition = 'CustomEventDefinition',
    DeclareStatement = 'DeclareStatement',
    DocComment = 'DocComment',
    EventDefinition = 'EventDefinition',
    EventIdentifier = 'EventIdentifier',
    EventHeader = 'EventHeader',
    ExpressionStatement = 'ExpressionStatement',
    FloatLiteral = 'FloatLiteral',
    FunctionCallExpression = 'FunctionCallExpression',
    FunctionCallExpressionParameter = 'FunctionCallExpressionParameter',
    FunctionDefinition = 'FunctionDefinition',
    FunctionHeader = 'FunctionHeader',
    FunctionParameter = 'FunctionParameter',
    FunctionParameters = 'FunctionParameters',
    GroupDefinition = 'GroupDefinition',
    HexLiteral = 'HexLiteral',
    Identifier = 'Identifier',
    IdentifierExpression = 'IdentifierExpression',
    IfStatement = 'IfStatement',
    IfStatementBody = 'IfStatementBody',
    Import = 'Import',
    IntLiteral = 'IntLiteral',
    IsExpression = 'IsExpression',
    LanguageFlag = 'LanguageFlag',
    LiteralExpression = 'LiteralExpression',
    MemberAccessExpression = 'MemberAccessExpression',
    NewArrayExpression = 'NewArrayExpression',
    NewStructExpression = 'NewStructExpression',
    NoneLiteral = 'NoneLiteral',
    PropertyDefinition = 'PropertyDefinition',
    ReturnStatement = 'ReturnStatement',
    Script = 'Script',
    ScriptHeader = 'ScriptHeader',
    StateDefinition = 'StateDefinition',
    StringLiteral = 'StringLiteral',
    StructDefinition = 'StructDefinition',
    TypeIdentifier = 'TypeIdentifier',
    UnaryOperationExpression = 'UnaryOperationExpression',
    UserFlag = 'UserFlag',
    VariableDefinition = 'VariableDefinition',
    WhileStatement = 'WhileStatement',
}

export type SymbolicNodeKinds =
    | NodeKind.CustomEventDefinition
    | NodeKind.DeclareStatement
    | NodeKind.EventDefinition
    | NodeKind.FunctionDefinition
    | NodeKind.FunctionParameter
    | NodeKind.GroupDefinition
    | NodeKind.Import
    | NodeKind.PropertyDefinition
    | NodeKind.Script
    | NodeKind.StateDefinition
    | NodeKind.StructDefinition
    | NodeKind.VariableDefinition;

export interface NodeInterface<TKind extends NodeKind = NodeKind> {
    kind: TKind;
    range: Range;
    parent: Node;
    children: Node[];
    script: ScriptNode;
    scopeMemberTypes: MemberTypes;
}

export interface Flaggable {
    flags: FlagNode[];
}

export interface Container {
    locals: SymbolTable;
}

export interface Identifiable {
    identifier: IdentifierNode;
}

export interface Documentable {
    leadingDocComment: DocCommentNode;
    docComment: DocCommentNode;
}

export interface LiteralNodeBase<TValue, TKind extends NodeKind>
    extends NodeInterface<TKind> {
    value: TValue;
}

export interface StringLiteralNode
    extends LiteralNodeBase<string, NodeKind.StringLiteral> {}

export interface IntLiteralNode
    extends LiteralNodeBase<number, NodeKind.IntLiteral> {}

export interface FloatLiteralNode
    extends LiteralNodeBase<number, NodeKind.FloatLiteral> {}

export interface HexLiteralNode
    extends LiteralNodeBase<number, NodeKind.HexLiteral> {}

export interface BoolLiteralNode
    extends LiteralNodeBase<boolean, NodeKind.BoolLiteral> {}

export interface IdentifierNode extends NodeInterface<NodeKind.Identifier> {
    name: string;
}

export interface ScriptNode extends NodeInterface<NodeKind.Script>, Container {
    header: ScriptHeaderNode;
    imports: ImportNode[];
    definitions: DefinitionNode[];
    scriptFile: ScriptFile;
    states: SymbolTable;
    customEvents: SymbolTable;
    symbol: ScriptSymbol;
    identifiers: Set<string>;
}

export interface ScriptHeaderNode
    extends Flaggable,
        Identifiable,
        Documentable,
        NodeInterface<NodeKind.ScriptHeader> {
    extendedIdentifier: TypeIdentifierNode;
}

export interface DocCommentNode extends NodeInterface<NodeKind.DocComment> {
    text: string;
}

export interface LanguageFlagNode extends NodeInterface<NodeKind.LanguageFlag> {
    flag: Flags;
}

export interface UserFlagNode extends NodeInterface<NodeKind.UserFlag> {
    name: string;
}

export interface CustomEventDefinitionNode
    extends NodeInterface<NodeKind.CustomEventDefinition> {
    identifier: IdentifierNode;
    symbol: CustomEventSymbol;
}

export interface TypeIdentifierNode
    extends NodeInterface<NodeKind.TypeIdentifier>,
        Identifiable {
    isArray: boolean;
}

export interface VariableDefinitionNode
    extends Flaggable,
        Identifiable,
        NodeInterface<NodeKind.VariableDefinition> {
    initialValue: LiteralNode;
    typeIdentifier: TypeIdentifierNode;
    symbol: VariableSymbol;
}

export interface StructDefinitionNode
    extends NodeInterface<NodeKind.StructDefinition>,
        Container,
        Identifiable {
    members: VariableDefinitionNode[];
    symbol: StructSymbol;
}

export interface StateDefinitionNode
    extends NodeInterface<NodeKind.StateDefinition>,
        Container,
        Identifiable {
    isAuto: boolean;
    definitions: (EventDefinitionNode | FunctionDefinitionNode)[];
    symbol: StateSymbol;
}

export interface EventDefinitionNode
    extends NodeInterface<NodeKind.EventDefinition>,
        Container {
    header: EventHeaderNode;
    statements: StatementNode[];
    symbol: EventSymbol;
}

export interface EventIdentifierNode
    extends NodeInterface<NodeKind.EventIdentifier>,
        Identifiable {
    identifier: IdentifierNode;
    eventIdentifierExpression:
        | IdentifierExpressionNode
        | MemberAccessExpressionNode;
}

export interface EventHeaderNode
    extends NodeInterface<NodeKind.EventHeader>,
        Documentable {
    identifier: EventIdentifierNode;
    parameters: FunctionParametersNode;
}

export interface FunctionDefinitionNode
    extends NodeInterface<NodeKind.FunctionDefinition>,
        Container {
    header: FunctionHeaderNode;
    statements: StatementNode[];
    symbol: FunctionSymbol;
}

export interface ReturnStatementNode
    extends NodeInterface<NodeKind.ReturnStatement> {
    returnValue: ExpressionNode;
}

export interface IfStatementNode extends NodeInterface<NodeKind.IfStatement> {
    bodies: IfStatementBodyNode[];
}

export interface IfStatementBodyNode
    extends NodeInterface<NodeKind.IfStatementBody>,
        Container {
    condition: ExpressionNode;
    statements: StatementNode[];
}

export interface AssignmentStatementNode
    extends NodeInterface<NodeKind.AssignmentStatement> {
    leftValue: ExpressionNode;
    operation: AssignmentOperatorType;
    rightValue: ExpressionNode;
}

export interface DeclareStatementNode
    extends NodeInterface<NodeKind.DeclareStatement>,
        Identifiable {
    typeIdentifier: TypeIdentifierNode;
    initialValue: ExpressionNode;
    symbol: VariableSymbol;
}

export interface ExpressionStatementNode
    extends NodeInterface<NodeKind.ExpressionStatement> {
    expression: ExpressionNode;
}

export interface WhileStatementNode
    extends NodeInterface<NodeKind.WhileStatement>,
        Container {
    expression: ExpressionNode;
    statements: StatementNode[];
}

export interface ArrayIndexExpressionNode
    extends NodeInterface<NodeKind.ArrayIndexExpression> {
    baseExpression: ExpressionNode;
    indexExpression: ExpressionNode;
}

export interface BinaryOperationExpressionNode
    extends NodeInterface<NodeKind.BinaryOperationExpression> {
    left: ExpressionNode;
    operator: BinaryOperatorType;
    right: ExpressionNode;
}

export interface CastExpressionNode
    extends NodeInterface<NodeKind.CastExpression> {
    innerExpression: ExpressionNode;
    typeIdentifier: TypeIdentifierNode;
}

export interface FunctionCallExpressionNode
    extends NodeInterface<NodeKind.FunctionCallExpression>,
        Identifiable {
    parameters: FunctionCallExpressionParameterNode[];
}

export interface FunctionCallExpressionParameterNode
    extends NodeInterface<NodeKind.FunctionCallExpressionParameter>,
        Identifiable {
    value: ExpressionNode;
}

export interface IdentifierExpressionNode
    extends NodeInterface<NodeKind.IdentifierExpression>,
        Identifiable {
    identifier: IdentifierNode;
}

export interface IsExpressionNode extends NodeInterface<NodeKind.IsExpression> {
    innerExpression: ExpressionNode;
    typeIdentifier: TypeIdentifierNode;
}

export interface LiteralExpressionNode
    extends NodeInterface<NodeKind.LiteralExpression> {
    value: LiteralNode;
}

export interface MemberAccessExpressionNode
    extends NodeInterface<NodeKind.MemberAccessExpression> {
    baseExpression: ExpressionNode;
    accessExpression: ExpressionNode;
}
export interface NewArrayExpressionNode
    extends NodeInterface<NodeKind.NewArrayExpression> {
    lengthExpression: ExpressionNode;
    arrayType: TypeIdentifierNode;
}

export interface NewStructExpressionNode
    extends NodeInterface<NodeKind.NewStructExpression> {
    structType: TypeIdentifierNode;
}

export interface UnaryOperationExpressionNode
    extends NodeInterface<NodeKind.UnaryOperationExpression> {
    operator: UnaryOperatorType;
    innerExpression: ExpressionNode;
}

export interface FunctionHeaderNode
    extends Flaggable,
        Identifiable,
        Documentable,
        NodeInterface<NodeKind.FunctionHeader> {
    returnTypeIdentifier: TypeIdentifierNode;
    parameters: FunctionParametersNode;
}

export interface FunctionParameterNode
    extends NodeInterface<NodeKind.FunctionParameter>,
        Identifiable {
    typeIdentifier: TypeIdentifierNode;
    defaultValue: LiteralNode;
    isOptional: boolean;
    symbol: ParameterSymbol;
}

export interface FunctionParametersNode
    extends NodeInterface<NodeKind.FunctionParameters> {
    parameters: FunctionParameterNode[];
}

export interface PropertyDefinitionNode
    extends Flaggable,
        NodeInterface<NodeKind.PropertyDefinition>,
        Container,
        Identifiable,
        Documentable {
    initialValue: LiteralNode;
    typeIdentifier: TypeIdentifierNode;
    functions: FunctionDefinitionNode[];
    symbol: PropertySymbol;
}

export interface GroupDefinitionNode
    extends Flaggable,
        Identifiable,
        NodeInterface<NodeKind.GroupDefinition> {
    properties: PropertyDefinitionNode[];
    symbol: GroupSymbol;
}

export interface ImportNode
    extends NodeInterface<NodeKind.Import>,
        Identifiable {
    symbol: ImportSymbol;
}

export interface NoneLiteralNode
    extends LiteralNodeBase<void, NodeKind.NoneLiteral> {}

export type DefinitionNode =
    | CustomEventDefinitionNode
    | EventDefinitionNode
    | FunctionDefinitionNode
    | GroupDefinitionNode
    | PropertyDefinitionNode
    | StateDefinitionNode
    | StructDefinitionNode
    | VariableDefinitionNode;

export type LiteralNode =
    | BoolLiteralNode
    | FloatLiteralNode
    | HexLiteralNode
    | IntLiteralNode
    | NoneLiteralNode
    | StringLiteralNode;

export type StatementNode =
    | AssignmentStatementNode
    | DeclareStatementNode
    | ExpressionStatementNode
    | IfStatementNode
    | ReturnStatementNode
    | WhileStatementNode;

export type ExpressionNode =
    | ArrayIndexExpressionNode
    | BinaryOperationExpressionNode
    | CastExpressionNode
    | FunctionCallExpressionNode
    | IdentifierExpressionNode
    | IsExpressionNode
    | LiteralExpressionNode
    | MemberAccessExpressionNode
    | NewArrayExpressionNode
    | NewStructExpressionNode
    | UnaryOperationExpressionNode;

export type FlagNode = LanguageFlagNode | UserFlagNode;

export enum BinaryOperatorType {
    None,
    Add,
    BooleanAnd,
    BooleanOr,
    CompareEqual,
    CompareGreaterThan,
    CompareGreaterThanOrEqual,
    CompareLessThan,
    CompareLessThanOrEqual,
    CompareNotEqual,
    Divide,
    Modulus,
    Multiply,
    Subtract,
}

export enum UnaryOperatorType {
    None,
    Negate,
    Not,
}

export enum AssignmentOperatorType {
    None,
    Assign,
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulus,
}

export enum Flags {
    None = 0,
    Auto = 1,
    AutoReadOnly = 2,
    BetaOnly = 4,
    Const = 8,
    DebugOnly = 16,
    Global = 32,
    Native = 64,
}

export const tokenKindToComparisonOperator = new Map([
    [TokenKind.EqualsEqualsToken, BinaryOperatorType.CompareEqual],
    [TokenKind.ExclamationEqualsToken, BinaryOperatorType.CompareNotEqual],
    [TokenKind.GreaterThanToken, BinaryOperatorType.CompareGreaterThan],
    [
        TokenKind.GreaterThanEqualsToken,
        BinaryOperatorType.CompareGreaterThanOrEqual,
    ],
    [TokenKind.LessThanToken, BinaryOperatorType.CompareLessThan],
    [TokenKind.LessThanEqualsToken, BinaryOperatorType.CompareLessThanOrEqual],
]) as ReadonlyMap<TokenKind, BinaryOperatorType>;

export const tokenKindToAssignmentOperator = new Map([
    [TokenKind.EqualsToken, AssignmentOperatorType.Assign],
    [TokenKind.PlusEqualsToken, AssignmentOperatorType.Add],
    [TokenKind.MinusEqualsToken, AssignmentOperatorType.Subtract],
    [TokenKind.SlashEqualsToken, AssignmentOperatorType.Divide],
    [TokenKind.AsteriskEqualsToken, AssignmentOperatorType.Multiply],
    [TokenKind.PercentEqualsToken, AssignmentOperatorType.Modulus],
]) as ReadonlyMap<TokenKind, AssignmentOperatorType>;

export const tokenKindToLanguageFlag = new Map([
    [TokenKind.AutoKeyword, Flags.Auto],
    [TokenKind.AutoReadOnlyKeyword, Flags.AutoReadOnly],
    [TokenKind.BetaOnlyKeyword, Flags.BetaOnly],
    [TokenKind.ConstKeyword, Flags.Const],
    [TokenKind.DebugOnlyKeyword, Flags.DebugOnly],
    [TokenKind.GlobalKeyword, Flags.Global],
    [TokenKind.NativeKeyword, Flags.Native],
]) as ReadonlyMap<TokenKind, Flags>;

// prettier-ignore
export type Node<T extends NodeKind = NodeKind> =
    T extends NodeKind.ArrayIndexExpression ? ArrayIndexExpressionNode :
    T extends NodeKind.AssignmentStatement ? AssignmentStatementNode :
    T extends NodeKind.BinaryOperationExpression ? BinaryOperationExpressionNode :
    T extends NodeKind.BoolLiteral ? BoolLiteralNode :
    T extends NodeKind.CastExpression ? CastExpressionNode :
    T extends NodeKind.CustomEventDefinition ? CustomEventDefinitionNode :
    T extends NodeKind.DeclareStatement ? DeclareStatementNode :
    T extends NodeKind.DocComment ? DocCommentNode :
    T extends NodeKind.EventDefinition ? EventDefinitionNode :
    T extends NodeKind.EventIdentifier ? EventIdentifierNode :
    T extends NodeKind.EventHeader ? EventHeaderNode :
    T extends NodeKind.ExpressionStatement ? ExpressionStatementNode :
    T extends NodeKind.FloatLiteral ? FloatLiteralNode :
    T extends NodeKind.FunctionCallExpression ? FunctionCallExpressionNode :
    T extends NodeKind.FunctionCallExpressionParameter ? FunctionCallExpressionParameterNode :
    T extends NodeKind.FunctionDefinition ? FunctionDefinitionNode :
    T extends NodeKind.FunctionHeader ? FunctionHeaderNode :
    T extends NodeKind.FunctionParameter ? FunctionParameterNode :
    T extends NodeKind.FunctionParameters ? FunctionParametersNode :
    T extends NodeKind.GroupDefinition ? GroupDefinitionNode :
    T extends NodeKind.HexLiteral ? HexLiteralNode :
    T extends NodeKind.Identifier ? IdentifierNode :
    T extends NodeKind.IdentifierExpression ? IdentifierExpressionNode :
    T extends NodeKind.IfStatement ? IfStatementNode :
    T extends NodeKind.IfStatementBody ? IfStatementBodyNode :
    T extends NodeKind.Import ? ImportNode :
    T extends NodeKind.IntLiteral ? IntLiteralNode :
    T extends NodeKind.IsExpression ? IsExpressionNode :
    T extends NodeKind.LanguageFlag ? LanguageFlagNode :
    T extends NodeKind.LiteralExpression ? LiteralExpressionNode :
    T extends NodeKind.MemberAccessExpression ? MemberAccessExpressionNode :
    T extends NodeKind.NewArrayExpression ? NewArrayExpressionNode :
    T extends NodeKind.NewStructExpression ? NewStructExpressionNode :
    T extends NodeKind.NoneLiteral ? NoneLiteralNode :
    T extends NodeKind.PropertyDefinition ? PropertyDefinitionNode :
    T extends NodeKind.ReturnStatement ? ReturnStatementNode :
    T extends NodeKind.Script ? ScriptNode :
    T extends NodeKind.ScriptHeader ? ScriptHeaderNode :
    T extends NodeKind.StateDefinition ? StateDefinitionNode :
    T extends NodeKind.StringLiteral ? StringLiteralNode :
    T extends NodeKind.StructDefinition ? StructDefinitionNode :
    T extends NodeKind.TypeIdentifier ? TypeIdentifierNode :
    T extends NodeKind.UnaryOperationExpression ? UnaryOperationExpressionNode :
    T extends NodeKind.UserFlag ? UserFlagNode :
    T extends NodeKind.VariableDefinition ? VariableDefinitionNode :
    T extends NodeKind.WhileStatement ? WhileStatementNode :
    never;

// prettier-ignore
export type SymbolNode<T extends NodeKind = NodeKind> =
    T extends NodeKind.CustomEventDefinition ? CustomEventDefinitionNode :
    T extends NodeKind.DeclareStatement ? DeclareStatementNode :
    T extends NodeKind.EventDefinition ? EventDefinitionNode :
    T extends NodeKind.FunctionDefinition ? FunctionDefinitionNode :
    T extends NodeKind.FunctionParameter ? FunctionParameterNode :
    T extends NodeKind.GroupDefinition ? GroupDefinitionNode :
    T extends NodeKind.Import ? ImportNode :
    T extends NodeKind.PropertyDefinition ? PropertyDefinitionNode :
    T extends NodeKind.Script ? ScriptNode :
    T extends NodeKind.StateDefinition ? StateDefinitionNode :
    T extends NodeKind.StructDefinition ? StructDefinitionNode :
    T extends NodeKind.VariableDefinition ? VariableDefinitionNode : never;

export class NodeObject<TKind extends NodeKind = NodeKind>
    implements NodeInterface<TKind> {
    public range: Range;
    public script: ScriptNode;
    public scopeMemberTypes: MemberTypes;

    private readonly _kind: TKind;

    private readonly _children: NodeInterface[] = [];
    private _parent: NodeObject;

    get children(): Node[] {
        return this._children as any;
    }

    get parent(): Node {
        return this._parent as any;
    }

    set parent(value: Node) {
        if (this._parent === (value as any)) {
            return;
        }

        if (this._parent) {
            this._parent._children.splice(
                this._parent._children.indexOf(this),
                1
            );
        }

        this._parent = value as any;

        if (this._parent) {
            this._parent._children.push(this);
        }
    }

    get kind() {
        return this._kind;
    }

    constructor(kind: TKind, script?: ScriptNode) {
        this._kind = kind;
        this.script = script;

        if (isContainer(this)) {
            this.locals = new Map();
        }
    }

    get sourceText() {
        return this.script && this.script.scriptFile
            ? this.script.scriptFile.text.slice(
                  this.range.start,
                  this.range.end
              )
            : '';
    }

    public toString() {
        return `${this.kind} ${this.range.start},${this.range.end}`;
    }
}

export type FlaggableNode = Flaggable & NodeInterface;

export function isFlaggable<T extends Node>(
    node: T
): node is FlaggableNode & T {
    switch (node.kind) {
        case NodeKind.PropertyDefinition:
        case NodeKind.ScriptHeader:
        case NodeKind.VariableDefinition:
        case NodeKind.FunctionHeader:
        case NodeKind.GroupDefinition:
            return true;
        default:
            return false;
    }
}

export type ContainerNode = Container & NodeInterface;

export function isContainer<T extends NodeInterface>(
    node: T
): node is ContainerNode & T {
    switch (node.kind) {
        case NodeKind.PropertyDefinition:
        case NodeKind.EventDefinition:
        case NodeKind.FunctionDefinition:
        case NodeKind.IfStatementBody:
        case NodeKind.Script:
        case NodeKind.StateDefinition:
        case NodeKind.StructDefinition:
        case NodeKind.WhileStatement:
            return true;
        default:
            return false;
    }
}

export function isLiteral<T extends NodeInterface>(
    node: T
): node is LiteralNode & T {
    switch (node.kind) {
        case NodeKind.BoolLiteral:
        case NodeKind.FloatLiteral:
        case NodeKind.HexLiteral:
        case NodeKind.IntLiteral:
        case NodeKind.NoneLiteral:
        case NodeKind.StringLiteral:
            return true;
        default:
            return false;
    }
}

export type DocumentableNode = Documentable & Node;

export function isDocumentableNode<T extends NodeInterface>(
    node: T
): node is DocumentableNode & T {
    switch (node.kind) {
        case NodeKind.ScriptHeader:
        case NodeKind.PropertyDefinition:
        case NodeKind.FunctionHeader:
            return true;
        default:
            return false;
    }
}

export function* iterateNodesAroundPosition(rootNode: Node, position: number) {
    for (const node of visitTreeNodesWhere(rootNode, (checkedNode) => {
        const withinNode =
            checkedNode.node.range.start <= position &&
            checkedNode.node.range.end >= position;

        if (!withinNode) {
            checkedNode.skipChildren = true;
        }

        return withinNode;
    })) {
        yield node.node;
    }
}

export function findNodeAtPosition(rootNode: Node, position: number) {
    return lastOfIterable(iterateNodesAroundPosition(rootNode, position));
}

export function isExpressionNode(node: Node): node is ExpressionNode {
    switch (node.kind) {
        case NodeKind.ArrayIndexExpression:
        case NodeKind.BinaryOperationExpression:
        case NodeKind.CastExpression:
        case NodeKind.FunctionCallExpression:
        case NodeKind.IdentifierExpression:
        case NodeKind.IsExpression:
        case NodeKind.LiteralExpression:
        case NodeKind.MemberAccessExpression:
        case NodeKind.NewArrayExpression:
        case NodeKind.NewStructExpression:
        case NodeKind.UnaryOperationExpression:
            return true;
        default:
            return false;
    }
}
