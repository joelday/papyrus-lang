import { visitTree } from '../common/TreeNode';
import { flushIterator, mapIterable } from '../common/Utilities';
import {
    ArrayIndexExpressionNode,
    AssignmentStatementNode,
    BinaryOperationExpressionNode,
    BoolLiteralNode,
    CastExpressionNode,
    CustomEventDefinitionNode,
    DeclareStatementNode,
    DocCommentNode,
    EventDefinitionNode,
    EventHeaderNode,
    EventIdentifierNode,
    ExpressionStatementNode,
    FloatLiteralNode,
    FunctionCallExpressionNode,
    FunctionCallExpressionParameterNode,
    FunctionDefinitionNode,
    FunctionHeaderNode,
    FunctionParameterNode,
    FunctionParametersNode,
    GroupDefinitionNode,
    HexLiteralNode,
    IdentifierExpressionNode,
    IdentifierNode,
    IfStatementBodyNode,
    IfStatementNode,
    ImportNode,
    IntLiteralNode,
    IsExpressionNode,
    LanguageFlagNode,
    LiteralExpressionNode,
    MemberAccessExpressionNode,
    NewArrayExpressionNode,
    NewStructExpressionNode,
    Node,
    NodeKind,
    NoneLiteralNode,
    PropertyDefinitionNode,
    ReturnStatementNode,
    ScriptHeaderNode,
    ScriptNode,
    StateDefinitionNode,
    StringLiteralNode,
    StructDefinitionNode,
    TypeIdentifierNode,
    UnaryOperationExpressionNode,
    UserFlagNode,
    VariableDefinitionNode,
    WhileStatementNode,
} from './Node';

export function visitNode<T, U>(
    node: Node,
    visitor: NodeVisitor<T, U>,
    data?: U
): T {
    switch (node.kind) {
        case NodeKind.ArrayIndexExpression:
            return visitor.visitArrayIndexExpression(node, data);
        case NodeKind.AssignmentStatement:
            return visitor.visitAssignmentStatement(node, data);
        case NodeKind.BinaryOperationExpression:
            return visitor.visitBinaryOperationExpression(node, data);
        case NodeKind.BoolLiteral:
            return visitor.visitBoolLiteral(node, data);
        case NodeKind.CastExpression:
            return visitor.visitCastExpression(node, data);
        case NodeKind.CustomEventDefinition:
            return visitor.visitCustomEventDefinition(node, data);
        case NodeKind.DeclareStatement:
            return visitor.visitDeclareStatement(node, data);
        case NodeKind.DocComment:
            return visitor.visitDocComment(node, data);
        case NodeKind.EventDefinition:
            return visitor.visitEventDefinition(node, data);
        case NodeKind.EventHeader:
            return visitor.visitEventHeader(node, data);
        case NodeKind.EventIdentifier:
            return visitor.visitEventIdentifier(node, data);
        case NodeKind.ExpressionStatement:
            return visitor.visitExpressionStatement(node, data);
        case NodeKind.FloatLiteral:
            return visitor.visitFloatLiteral(node, data);
        case NodeKind.FunctionCallExpression:
            return visitor.visitFunctionCallExpression(node, data);
        case NodeKind.FunctionCallExpressionParameter:
            return visitor.visitFunctionCallExpressionParameter(node, data);
        case NodeKind.FunctionDefinition:
            return visitor.visitFunctionDefinition(node, data);
        case NodeKind.FunctionHeader:
            return visitor.visitFunctionHeader(node, data);
        case NodeKind.FunctionParameter:
            return visitor.visitFunctionParameter(node, data);
        case NodeKind.FunctionParameters:
            return visitor.visitFunctionParameters(node, data);
        case NodeKind.GroupDefinition:
            return visitor.visitGroupDefinition(node, data);
        case NodeKind.HexLiteral:
            return visitor.visitHexLiteral(node, data);
        case NodeKind.Identifier:
            return visitor.visitIdentifier(node, data);
        case NodeKind.IdentifierExpression:
            return visitor.visitIdentifierExpression(node, data);
        case NodeKind.IfStatement:
            return visitor.visitIfStatement(node, data);
        case NodeKind.IfStatementBody:
            return visitor.visitIfStatementBody(node, data);
        case NodeKind.Import:
            return visitor.visitImport(node, data);
        case NodeKind.IntLiteral:
            return visitor.visitIntLiteral(node, data);
        case NodeKind.IsExpression:
            return visitor.visitIsExpression(node, data);
        case NodeKind.LanguageFlag:
            return visitor.visitLanguageFlag(node, data);
        case NodeKind.LiteralExpression:
            return visitor.visitLiteralExpression(node, data);
        case NodeKind.MemberAccessExpression:
            return visitor.visitMemberAccessExpression(node, data);
        case NodeKind.NewArrayExpression:
            return visitor.visitNewArrayExpression(node, data);
        case NodeKind.NewStructExpression:
            return visitor.visitNewStructExpression(node, data);
        case NodeKind.NoneLiteral:
            return visitor.visitNoneLiteral(node, data);
        case NodeKind.PropertyDefinition:
            return visitor.visitPropertyDefinition(node, data);
        case NodeKind.ReturnStatement:
            return visitor.visitReturnStatement(node, data);
        case NodeKind.Script:
            return visitor.visitScript(node, data);
        case NodeKind.ScriptHeader:
            return visitor.visitScriptHeader(node, data);
        case NodeKind.StateDefinition:
            return visitor.visitStateDefinition(node, data);
        case NodeKind.StringLiteral:
            return visitor.visitStringLiteral(node, data);
        case NodeKind.StructDefinition:
            return visitor.visitStructDefinition(node, data);
        case NodeKind.TypeIdentifier:
            return visitor.visitTypeIdentifier(node, data);
        case NodeKind.UnaryOperationExpression:
            return visitor.visitUnaryOperationExpression(node, data);
        case NodeKind.UserFlag:
            return visitor.visitUserFlag(node, data);
        case NodeKind.VariableDefinition:
            return visitor.visitVariableDefinition(node, data);
        case NodeKind.WhileStatement:
            return visitor.visitWhileStatement(node, data);
        default:
            throw new Error(`Unknown node kind.`);
    }
}

export function* visitNodes<T, U>(
    nodes: Iterable<Node>,
    visitor: NodeVisitor<T, U>,
    data?: U
) {
    for (const node of nodes) {
        yield visitNode(node, visitor, data);
    }
}

export function iterateAllNodes<T, U>(
    rootNode: Node,
    visitor: NodeVisitor<T, U>,
    data?: U
) {
    return visitNodes(
        mapIterable(visitTree(rootNode), (n) => n.node),
        visitor,
        data
    );
}

export function visitAll<T, U>(
    rootNode: Node,
    visitor: NodeVisitor<T, U>,
    data?: U
) {
    flushIterator(iterateAllNodes(rootNode, visitor, data));
}

export class NodeVisitor<T = void, U = void> {
    public defaultVisit(node: Node, data?: U): T {
        return null;
    }
    public visitArrayIndexExpression(
        node: ArrayIndexExpressionNode,
        data?: U
    ): T {
        return this.defaultVisit(node, data);
    }
    public visitAssignmentStatement(
        node: AssignmentStatementNode,
        data?: U
    ): T {
        return this.defaultVisit(node, data);
    }
    public visitBinaryOperationExpression(
        node: BinaryOperationExpressionNode,
        data?: U
    ): T {
        return this.defaultVisit(node, data);
    }
    public visitBoolLiteral(node: BoolLiteralNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitCastExpression(node: CastExpressionNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitCustomEventDefinition(
        node: CustomEventDefinitionNode,
        data?: U
    ): T {
        return this.defaultVisit(node, data);
    }
    public visitDeclareStatement(node: DeclareStatementNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitDocComment(node: DocCommentNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitEventDefinition(node: EventDefinitionNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitEventHeader(node: EventHeaderNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitEventIdentifier(node: EventIdentifierNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitExpressionStatement(
        node: ExpressionStatementNode,
        data?: U
    ): T {
        return this.defaultVisit(node, data);
    }
    public visitFloatLiteral(node: FloatLiteralNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitFunctionCallExpression(
        node: FunctionCallExpressionNode,
        data?: U
    ): T {
        return this.defaultVisit(node, data);
    }
    public visitFunctionCallExpressionParameter(
        node: FunctionCallExpressionParameterNode,
        data?: U
    ): T {
        return this.defaultVisit(node, data);
    }
    public visitFunctionDefinition(node: FunctionDefinitionNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitFunctionHeader(node: FunctionHeaderNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitFunctionParameter(node: FunctionParameterNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitFunctionParameters(node: FunctionParametersNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitGroupDefinition(node: GroupDefinitionNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitHexLiteral(node: HexLiteralNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitIdentifier(node: IdentifierNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitIdentifierExpression(
        node: IdentifierExpressionNode,
        data?: U
    ): T {
        return this.defaultVisit(node, data);
    }
    public visitIfStatement(node: IfStatementNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitIfStatementBody(node: IfStatementBodyNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitImport(node: ImportNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitIntLiteral(node: IntLiteralNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitIsExpression(node: IsExpressionNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitLanguageFlag(node: LanguageFlagNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitLiteralExpression(node: LiteralExpressionNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitMemberAccessExpression(
        node: MemberAccessExpressionNode,
        data?: U
    ): T {
        return this.defaultVisit(node, data);
    }
    public visitNewArrayExpression(node: NewArrayExpressionNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitNewStructExpression(
        node: NewStructExpressionNode,
        data?: U
    ): T {
        return this.defaultVisit(node, data);
    }
    public visitNoneLiteral(node: NoneLiteralNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitPropertyDefinition(node: PropertyDefinitionNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitReturnStatement(node: ReturnStatementNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitScript(node: ScriptNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitScriptHeader(node: ScriptHeaderNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitStateDefinition(node: StateDefinitionNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitStringLiteral(node: StringLiteralNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitStructDefinition(node: StructDefinitionNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitTypeIdentifier(node: TypeIdentifierNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitUnaryOperationExpression(
        node: UnaryOperationExpressionNode,
        data?: U
    ): T {
        return this.defaultVisit(node, data);
    }
    public visitUserFlag(node: UserFlagNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitVariableDefinition(node: VariableDefinitionNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
    public visitWhileStatement(node: WhileStatementNode, data?: U): T {
        return this.defaultVisit(node, data);
    }
}
