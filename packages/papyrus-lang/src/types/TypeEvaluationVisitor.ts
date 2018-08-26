import {
    ArrayIndexExpressionNode,
    BinaryOperationExpressionNode,
    BinaryOperatorType,
    BoolLiteralNode,
    CastExpressionNode,
    DeclareStatementNode,
    ExpressionNode,
    FloatLiteralNode,
    FunctionCallExpressionNode,
    FunctionCallExpressionParameterNode,
    FunctionDefinitionNode,
    FunctionParameterNode,
    HexLiteralNode,
    Identifiable,
    IdentifierExpressionNode,
    IntLiteralNode,
    LiteralExpressionNode,
    MemberAccessExpressionNode,
    NewStructExpressionNode,
    Node,
    NoneLiteralNode,
    PropertyDefinitionNode,
    ScriptNode,
    StringLiteralNode,
    UnaryOperationExpressionNode,
    UnaryOperatorType,
    VariableDefinitionNode,
} from '../parser/Node';
import { NodeVisitor, visitNode } from '../parser/NodeVisitor';
import { SymbolKind } from '../symbols/Symbol';
import { ArrayType, intrinsicTypes, Type, TypeKind } from './Type';
import { TypeChecker } from './TypeChecker';

export class TypeEvaluationVisitor extends NodeVisitor<Type, ExpressionNode> {
    private readonly _typeChecker: TypeChecker;

    constructor(typeChecker: TypeChecker) {
        super();
        this._typeChecker = typeChecker;
    }

    public visitVariableDefinition(node: VariableDefinitionNode) {
        return this._typeChecker.getTypeForTypeIdentifier(node.typeIdentifier);
    }

    public visitFunctionParameter(node: FunctionParameterNode) {
        return this._typeChecker.getTypeForTypeIdentifier(node.typeIdentifier);
    }

    public visitPropertyDefinition(node: PropertyDefinitionNode) {
        return this._typeChecker.getTypeForTypeIdentifier(node.typeIdentifier);
    }

    public visitScript(node: ScriptNode) {
        return this._typeChecker.getTypeOfSymbol(node.symbol);
    }

    public visitIdentifierExpression(
        node: IdentifierExpressionNode,
        baseExpression?: ExpressionNode
    ): Type {
        if (baseExpression) {
            return this.resolveTypeForAccessExpression(node, baseExpression);
        }

        const identifierSymbols = this._typeChecker.getMatchingSymbolsInScope(
            node.identifier,
            node.identifier.name
        );

        if (identifierSymbols.length > 0) {
            const identifierSymbol = identifierSymbols[0];

            if (identifierSymbol.kind !== SymbolKind.Intrinsic) {
                return visitNode<Type, ExpressionNode>(
                    identifierSymbol.declaration.node,
                    this
                );
            }
        }

        return null;
    }

    public visitFunctionDefinition(node: FunctionDefinitionNode) {
        if (node.header.returnTypeIdentifier) {
            return this._typeChecker.getTypeForTypeIdentifier(
                node.header.returnTypeIdentifier
            );
        }

        return intrinsicTypes.get('void');
    }

    public visitFunctionCallExpression(
        node: FunctionCallExpressionNode,
        baseExpression?: ExpressionNode
    ): Type {
        if (baseExpression) {
            return this.resolveTypeForAccessExpression(node, baseExpression);
        }

        const identifierSymbols = this._typeChecker.getMatchingSymbolsInScope(
            node,
            node.identifier.name
        );

        if (identifierSymbols.length > 0) {
            const identifierSymbol = identifierSymbols[0];

            if (identifierSymbol.kind !== SymbolKind.Intrinsic) {
                return visitNode<Type, ExpressionNode>(
                    identifierSymbol.declaration.node,
                    this
                );
            }
        }

        return null;
    }

    public visitDeclareStatement(node: DeclareStatementNode): Type {
        if (
            node.typeIdentifier &&
            (node.typeIdentifier.identifier.name.toLowerCase() !== 'var' ||
                node.typeIdentifier.isArray)
        ) {
            return this._typeChecker.getTypeForTypeIdentifier(
                node.typeIdentifier
            );
        }

        return visitNode<Type, ExpressionNode>(node.initialValue, this);
    }

    public visitNewStructExpression(node: NewStructExpressionNode) {
        return this._typeChecker.getTypeForTypeIdentifier(node.structType);
    }

    public visitMemberAccessExpression(node: MemberAccessExpressionNode): Type {
        return visitNode<Type, ExpressionNode>(
            node.accessExpression,
            this,
            node.baseExpression
        );
    }

    public visitNoneLiteral(node: NoneLiteralNode) {
        return intrinsicTypes.get('any');
    }

    public visitFloatLiteral(node: FloatLiteralNode) {
        return intrinsicTypes.get('float');
    }

    public visitIntLiteral(node: IntLiteralNode) {
        return intrinsicTypes.get('int');
    }

    public visitBoolLiteral(node: BoolLiteralNode) {
        return intrinsicTypes.get('bool');
    }

    public visitHexLiteral(node: HexLiteralNode) {
        return intrinsicTypes.get('int');
    }

    public visitStringLiteral(node: StringLiteralNode) {
        return intrinsicTypes.get('string');
    }

    public visitLiteralExpression(node: LiteralExpressionNode): Type {
        return visitNode<Type, ExpressionNode>(node.value, this);
    }

    public visitCastExpression(node: CastExpressionNode) {
        return this._typeChecker.getTypeForTypeIdentifier(node.typeIdentifier);
    }

    public visitFunctionCallExpressionParameter(
        node: FunctionCallExpressionParameterNode
    ) {
        return this._typeChecker.getTypeOfExpressionNode(node.value);
    }

    public visitIsExpression() {
        return intrinsicTypes.get('bool');
    }

    public visitUnaryOperationExpression(node: UnaryOperationExpressionNode) {
        switch (node.operator) {
            case UnaryOperatorType.Negate:
                return this._typeChecker.getTypeOfExpressionNode(
                    node.innerExpression
                );
            case UnaryOperatorType.Not:
                return intrinsicTypes.get('bool');
            default:
                return null;
        }
    }

    public visitBinaryOperationExpression(node: BinaryOperationExpressionNode) {
        switch (node.operator) {
            case BinaryOperatorType.BooleanAnd:
            case BinaryOperatorType.BooleanOr:
            case BinaryOperatorType.CompareGreaterThan:
            case BinaryOperatorType.CompareGreaterThanOrEqual:
            case BinaryOperatorType.CompareLessThan:
            case BinaryOperatorType.CompareLessThanOrEqual:
            case BinaryOperatorType.CompareEqual:
            case BinaryOperatorType.CompareNotEqual:
                return intrinsicTypes.get('bool');
            case BinaryOperatorType.Add:
                if (
                    visitNode<Type, ExpressionNode>(node.left, this) ===
                        intrinsicTypes.get('string') ||
                    visitNode<Type, ExpressionNode>(node.right, this) ===
                        intrinsicTypes.get('string')
                ) {
                    return intrinsicTypes.get('string');
                }
            case BinaryOperatorType.Divide:
            case BinaryOperatorType.Multiply:
            case BinaryOperatorType.Modulus:
            case BinaryOperatorType.Subtract:
                if (
                    visitNode<Type, ExpressionNode>(node.left, this) ===
                        intrinsicTypes.get('float') ||
                    visitNode<Type, ExpressionNode>(node.right, this) ===
                        intrinsicTypes.get('float')
                ) {
                    return intrinsicTypes.get('float');
                }

                return intrinsicTypes.get('int');
            default:
                return null;
        }
    }

    public visitArrayIndexExpression(node: ArrayIndexExpressionNode) {
        const arrayType = visitNode<Type, ExpressionNode>(
            node.baseExpression,
            this
        ) as ArrayType;

        return this._typeChecker.getElementTypeForArrayType(arrayType);
    }

    private resolveTypeForAccessExpression(
        node: Identifiable & Node,
        baseExpression: ExpressionNode
    ) {
        const baseType: Type = this._typeChecker.getTypeOfExpressionNode(
            baseExpression
        );

        if (!baseType) {
            return null;
        }

        if (
            baseType.kind === TypeKind.Script ||
            baseType.kind === TypeKind.Struct
        ) {
            const expressionTypeScriptNode =
                baseType.kind === TypeKind.Script
                    ? baseType.symbol.declaration.node.script
                    : baseType.symbol.declaration.node;

            const baseLocalSymbols = this._typeChecker.getMatchingSymbolsInScope(
                expressionTypeScriptNode,
                node.identifier.name
            );

            if (baseLocalSymbols.length > 0) {
                const baseLocal = baseLocalSymbols[0];

                if (baseLocal.kind !== SymbolKind.Intrinsic) {
                    return visitNode<Type, ExpressionNode>(
                        baseLocal.declaration.node,
                        this
                    );
                }
            }
        }

        return null;
    }
}
