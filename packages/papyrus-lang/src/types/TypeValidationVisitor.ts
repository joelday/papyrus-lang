import { Range } from '../common/Range';
import { TreeVisitationState, visitTree } from '../common/TreeNode';
import { Diagnostics } from '../Diagnostics';
import {
    AssignmentOperatorType,
    AssignmentStatementNode,
    BinaryOperationExpressionNode,
    BinaryOperatorType,
    DeclareStatementNode,
    FunctionCallExpressionNode,
    IdentifierNode,
    Node,
    NodeKind,
    TypeIdentifierNode,
} from '../parser/Node';
import { NodeVisitor, visitNode } from '../parser/NodeVisitor';
import { FunctionSymbol, ParameterSymbol, SymbolKind } from '../symbols/Symbol';
import { intrinsicTypes, Type } from './Type';
import { TypeChecker } from './TypeChecker';

export interface TypeValidationContext {
    diagnostics: Diagnostics;
    treeVisitation: TreeVisitationState<Node>;
}

export function validateTypes(
    validationVisitor: TypeValidationVisitor,
    diagnostics: Diagnostics,
    rootNode: Node
) {
    for (const node of visitTree(rootNode)) {
        try {
            visitNode(node.node, validationVisitor, {
                diagnostics,
                treeVisitation: node,
            });
        } catch (e) {
            diagnostics.addError(
                `${e.toString()}\r\n${e.stack}`,
                node.node.range
            );
        }
    }
}

function addParameterTypeError(
    typeA: Type,
    typeB: Type,
    diagnostics: Diagnostics,
    range: Range
) {
    if (!typeA || !typeB) {
        return;
    }

    diagnostics.addError(
        `Argument of type '${
            typeA.name
        }' is not assignable to parameter of type '${typeB.name}'.`,
        range
    );
}

function addTypeAssignmentError(
    typeA: Type,
    typeB: Type,
    diagnostics: Diagnostics,
    range: Range
) {
    if (!typeA || !typeB) {
        return;
    }

    diagnostics.addError(
        `Type '${typeA.name}' is not assignable to type '${typeB.name}'.`,
        range
    );
}

function addBinaryOperatorTypesError(
    operator: BinaryOperatorType,
    typeA: Type,
    typeB: Type,
    diagnostics: Diagnostics,
    range: Range
) {
    if (!typeA || !typeB) {
        return;
    }

    diagnostics.addError(
        `Operator '${
            BinaryOperatorType[operator]
        }' cannot be applied to types '${typeA.name}' and '${typeB.name}'.`,
        range
    );
}

function addAssignmentOperatorTypesError(
    operator: AssignmentOperatorType,
    typeA: Type,
    typeB: Type,
    diagnostics: Diagnostics,
    range: Range
) {
    if (!typeA || !typeB) {
        return;
    }

    diagnostics.addError(
        `Operator '${
            AssignmentOperatorType[operator]
        }' cannot be applied to types '${typeA.name}' and '${typeB.name}'.`,
        range
    );
}

export class TypeValidationVisitor extends NodeVisitor<
    void,
    TypeValidationContext
> {
    private readonly _typeChecker: TypeChecker;

    constructor(typeChecker: TypeChecker) {
        super();
        this._typeChecker = typeChecker;
    }

    public visitDeclareStatement(
        node: DeclareStatementNode,
        context: TypeValidationContext
    ) {
        if (node.initialValue) {
            const variableType = this._typeChecker.getTypeForTypeIdentifier(
                node.typeIdentifier
            );

            const initialValueType = this._typeChecker.getTypeOfExpressionNode(
                node.initialValue
            );

            if (
                !this._typeChecker.getTypeIsAssignableFrom(
                    variableType,
                    initialValueType
                )
            ) {
                addTypeAssignmentError(
                    initialValueType,
                    variableType,
                    context.diagnostics,
                    node.initialValue.range
                );
            }
        }
    }

    public visitTypeIdentifier(
        node: TypeIdentifierNode,
        context: TypeValidationContext
    ) {
        const type = this._typeChecker.getTypeForTypeIdentifier(node);
        if (!type) {
            context.diagnostics.addError(
                `Cannot find name '${node.identifier.name}'`,
                node.range
            );
        }

        context.treeVisitation.skipChildren = true;
    }

    public visitIdentifier(
        node: IdentifierNode,
        context: TypeValidationContext
    ) {
        if (
            node.parent.kind === NodeKind.GroupDefinition ||
            node.parent.kind === NodeKind.StateDefinition
        ) {
            return;
        }

        const { symbols } = this._typeChecker.getSymbolsForIdentifier(node);
        if (symbols.length === 0) {
            if (
                node.parent.kind === NodeKind.Import &&
                node.script.scriptFile.program.scriptNames.some((n) =>
                    n.toLowerCase().startsWith(`${node.name}:`.toLowerCase())
                )
            ) {
                return;
            }

            context.diagnostics.addError(
                `Cannot find name '${node.name}'`,
                node.range
            );
        } else {
            const symbol = symbols[0];

            if (
                symbol.kind === SymbolKind.Variable &&
                this._typeChecker.getVariableIsBlockScoped(symbol)
            ) {
                if (symbol.declaration.node.range.start >= node.range.end) {
                    context.diagnostics.addError(
                        `Block-scoped variable '${
                            node.name
                        }' used before its declaration.`,
                        node.range
                    );
                }
            }
        }
    }

    public visitBinaryOperationExpression(
        node: BinaryOperationExpressionNode,
        context: TypeValidationContext
    ) {
        const leftType = this._typeChecker.getTypeOfExpressionNode(node.left);
        const rightType = this._typeChecker.getTypeOfExpressionNode(node.right);

        let invalidTypes = false;

        switch (node.operator) {
            case BinaryOperatorType.CompareGreaterThan:
            case BinaryOperatorType.CompareGreaterThanOrEqual:
            case BinaryOperatorType.CompareLessThan:
            case BinaryOperatorType.CompareLessThanOrEqual:
                if (
                    !(
                        leftType === intrinsicTypes.get('float') ||
                        leftType === intrinsicTypes.get('int')
                    ) ||
                    !(
                        rightType === intrinsicTypes.get('float') ||
                        rightType === intrinsicTypes.get('int')
                    )
                ) {
                    invalidTypes = true;
                }
                break;
            case BinaryOperatorType.Add:
                if (
                    leftType === intrinsicTypes.get('string') ||
                    rightType === intrinsicTypes.get('string')
                ) {
                    break;
                }
            case BinaryOperatorType.Divide:
            case BinaryOperatorType.Multiply:
            case BinaryOperatorType.Modulus:
            case BinaryOperatorType.Subtract:
                if (
                    !(
                        leftType === intrinsicTypes.get('float') ||
                        leftType === intrinsicTypes.get('int')
                    ) ||
                    !(
                        rightType === intrinsicTypes.get('float') ||
                        rightType === intrinsicTypes.get('int')
                    )
                ) {
                    invalidTypes = true;
                }
                break;
            default:
        }

        if (invalidTypes) {
            addBinaryOperatorTypesError(
                node.operator,
                leftType,
                rightType,
                context.diagnostics,
                node.range
            );
        }
    }

    public visitAssignmentStatement(
        node: AssignmentStatementNode,
        context: TypeValidationContext
    ) {
        if (!node.leftValue) {
            context.diagnostics.addError(
                'Expected left-hand side of assignment expression',
                node.range
            );

            context.treeVisitation.skipChildren = true;
            return;
        }

        if (!this._typeChecker.getExpressionIsAssignable(node.leftValue)) {
            context.diagnostics.addError(
                'Left-hand side cannot be assigned to',
                node.range
            );

            context.treeVisitation.skipChildren = true;
            return;
        }

        const assignmentIdentifier = this._typeChecker.getIdentifierForExpression(
            node.leftValue
        );

        if (
            assignmentIdentifier &&
            !this._typeChecker.getIdentifierIsWritable(assignmentIdentifier)
        ) {
            context.diagnostics.addError(
                'Left-hand side is const or read-only',
                node.range
            );

            context.treeVisitation.skipChildren = true;
            return;
        }

        if (!node.rightValue) {
            context.diagnostics.addError(
                'Expected right-hand side of assignment expression',
                node.range
            );

            context.treeVisitation.skipChildren = true;
            return;
        }

        const leftType = this._typeChecker.getTypeOfExpressionNode(
            node.leftValue
        );

        const rightType = this._typeChecker.getTypeOfExpressionNode(
            node.rightValue
        );

        switch (node.operation) {
            case AssignmentOperatorType.Add:
                if (leftType === intrinsicTypes.get('string')) {
                    break;
                }
            case AssignmentOperatorType.Divide:
            case AssignmentOperatorType.Modulus:
            case AssignmentOperatorType.Multiply:
            case AssignmentOperatorType.Subtract:
                if (
                    !(
                        leftType === intrinsicTypes.get('float') ||
                        leftType === intrinsicTypes.get('int')
                    ) ||
                    !(
                        rightType === intrinsicTypes.get('float') ||
                        rightType === intrinsicTypes.get('int')
                    )
                ) {
                    addAssignmentOperatorTypesError(
                        node.operation,
                        leftType,
                        rightType,
                        context.diagnostics,
                        node.range
                    );
                }
                break;
            case AssignmentOperatorType.Assign:
                if (
                    !this._typeChecker.getTypeIsAssignableFrom(
                        leftType,
                        rightType
                    )
                ) {
                    addTypeAssignmentError(
                        rightType,
                        leftType,
                        context.diagnostics,
                        node.rightValue.range
                    );
                }
                break;
            default:
        }
    }

    public visitFunctionCallExpression(
        node: FunctionCallExpressionNode,
        context: TypeValidationContext
    ) {
        const { symbols } = this._typeChecker.getSymbolsForIdentifier(
            node.identifier
        );

        if (symbols.length > 0) {
            const functionSymbol = symbols[0] as FunctionSymbol;
            const params = functionSymbol.parameters;

            const optionalParamMap = new Map(
                functionSymbol.parameters
                    .filter((p) => p.isOptional)
                    .map<[string, ParameterSymbol]>((parameter) => [
                        parameter.name.toLowerCase(),
                        parameter,
                    ])
            );

            const usedOptionalParams = new Set<string>();

            const requiredParams = functionSymbol.parameters.filter(
                (p) => !p.isOptional
            );

            const optionalParams = functionSymbol.parameters.filter(
                (p) => p.isOptional
            );

            const callParams = [...node.parameters];

            if (callParams.length > params.length) {
                context.diagnostics.addError(
                    'Expected N parameters, got N',
                    node.range
                );
                context.treeVisitation.skipChildren = true;
                return;
            }

            for (const param of requiredParams) {
                const callParam = callParams.shift();

                if (!callParam) {
                    context.diagnostics.addError(
                        'Expected N parameters, got N',
                        node.range
                    );
                    continue;
                }

                if (!param && callParam.identifier) {
                    context.diagnostics.addError(
                        'No optional parameter named X',
                        callParam.range
                    );
                    continue;
                }

                this._typeChecker.checkTypesAndReferences(
                    callParam.value,
                    context.diagnostics
                );

                const paramType = this._typeChecker.getTypeForTypeIdentifier(
                    param.declaration.node.typeIdentifier
                );

                const callParamType = this._typeChecker.getTypeOfExpressionNode(
                    callParam.value
                );

                if (
                    !this._typeChecker.getTypeIsAssignableFrom(
                        paramType,
                        callParamType
                    )
                ) {
                    addParameterTypeError(
                        callParamType,
                        paramType,
                        context.diagnostics,
                        callParam.range
                    );
                }
            }

            for (const callParam of callParams) {
                const param = callParam.identifier
                    ? optionalParamMap.get(
                          callParam.identifier.name.toLowerCase()
                      )
                    : optionalParams.shift();

                if (!param && callParam.identifier) {
                    context.diagnostics.addError(
                        'No optional parameter named X',
                        callParam.range
                    );
                    continue;
                }

                if (
                    callParam.identifier &&
                    usedOptionalParams.has(
                        callParam.identifier.name.toLowerCase()
                    )
                ) {
                    context.diagnostics.addError(
                        'Optional name already used',
                        callParam.range
                    );
                    continue;
                }

                usedOptionalParams.add(
                    callParam.identifier
                        ? callParam.identifier.name.toLowerCase()
                        : param.name.toLowerCase()
                );

                this._typeChecker.checkTypesAndReferences(
                    callParam.value,
                    context.diagnostics
                );

                const paramType = this._typeChecker.getTypeForTypeIdentifier(
                    param.declaration.node.typeIdentifier
                );

                const callParamType = this._typeChecker.getTypeOfExpressionNode(
                    callParam.value
                );

                if (
                    !this._typeChecker.getTypeIsAssignableFrom(
                        paramType,
                        callParamType
                    )
                ) {
                    addParameterTypeError(
                        callParamType,
                        paramType,
                        context.diagnostics,
                        callParam.range
                    );
                }
            }

            context.treeVisitation.skipChildren = true;
        }
    }
}
