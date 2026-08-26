using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;

namespace Papyrus.CodeAnalysis.Binding;

internal static class Lowering
{
    public static BoundNode Lower(BoundNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new Rewriter().Visit(node);
    }

    public static BoundFunction LowerFunction(BoundFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        return (BoundFunction)Lower(function);
    }

    private sealed class Rewriter
    {
        private readonly Dictionary<IPropertySymbol, IVariableSymbol> _backingFields = new();
        private IScriptSymbol? _script;
        private NameTable? _names;

        public BoundNode Visit(BoundNode node)
        {
            BoundNode rewritten = node switch
            {
                BoundScript script => VisitScript(script),
                BoundFunction function => VisitFunction(function),
                BoundBlock block => VisitBlock(block),
                BoundIf @if => VisitIf(@if),
                BoundWhile @while => VisitWhile(@while),
                BoundReturn ret => VisitReturn(ret),
                BoundExpressionStatement statement => VisitExpressionStatement(statement),
                BoundLocalDeclaration declaration => VisitLocalDeclaration(declaration),
                BoundAssignment assignment => VisitAssignment(assignment),
                BoundExpression expression => VisitExpression(expression),
                _ => node,
            };

            if (!ReferenceEquals(rewritten, node))
            {
                rewritten.Syntax ??= node.Syntax;
            }

            return rewritten;
        }

        private BoundExpression VisitExpression(BoundExpression expression)
        {
            BoundExpression rewritten = expression switch
            {
                BoundProperty property => VisitProperty(property),
                BoundField field => VisitField(field),
                BoundBinary binary => VisitBinary(binary),
                BoundUnary unary => VisitUnary(unary),
                BoundConversion conversion => VisitConversion(conversion),
                BoundCall call => VisitCall(call),
                BoundMemberAccess member => VisitMemberAccess(member),
                BoundIndex index => VisitIndex(index),
                BoundIs @is => VisitIs(@is),
                BoundNewArray array => VisitNewArray(array),
                BoundArrayLength length => VisitArrayLength(length),
                _ => expression,
            };

            if (!ReferenceEquals(rewritten, expression))
            {
                rewritten.Syntax ??= expression.Syntax;
            }

            return rewritten;
        }

        private BoundScript VisitScript(BoundScript script)
        {
            var previous = _script;
            _script = script.Script;
            try
            {
                var functions = VisitArray(script.Functions, VisitFunction);
                return functions == script.Functions ? script : new BoundScript(script.Script, functions);
            }
            finally
            {
                _script = previous;
            }
        }

        private BoundFunction VisitFunction(BoundFunction function)
        {
            var previous = _script;
            _script = function.Function.ContainingScript ?? _script;
            try
            {
                var body = VisitBlock(function.Body);
                return body == function.Body ? function : new BoundFunction(function.Function, body);
            }
            finally
            {
                _script = previous;
            }
        }

        private BoundBlock VisitBlock(BoundBlock block)
        {
            var statements = VisitArray(block.Statements, VisitStatement);
            return statements == block.Statements ? block : new BoundBlock(statements);
        }

        private BoundStatement VisitStatement(BoundStatement statement)
        {
            return (BoundStatement)Visit(statement);
        }

        private BoundIf VisitIf(BoundIf @if)
        {
            var condition = VisitExpression(@if.Condition);
            var then = VisitBlock(@if.Then);
            var @else = @if.Else is null ? null : VisitStatement(@if.Else);
            if (condition == @if.Condition && then == @if.Then && @else == @if.Else)
            {
                return @if;
            }

            return new BoundIf(condition, then, @else);
        }

        private BoundWhile VisitWhile(BoundWhile @while)
        {
            var condition = VisitExpression(@while.Condition);
            var body = VisitBlock(@while.Body);
            if (condition == @while.Condition && body == @while.Body)
            {
                return @while;
            }

            return new BoundWhile(condition, body);
        }

        private BoundReturn VisitReturn(BoundReturn ret)
        {
            if (ret.Expression is null)
            {
                return ret;
            }

            var expression = VisitExpression(ret.Expression);
            return expression == ret.Expression ? ret : new BoundReturn(expression);
        }

        private BoundExpressionStatement VisitExpressionStatement(BoundExpressionStatement statement)
        {
            var expression = VisitExpression(statement.Expression);
            return expression == statement.Expression
                ? statement
                : new BoundExpressionStatement(expression);
        }

        private BoundLocalDeclaration VisitLocalDeclaration(BoundLocalDeclaration declaration)
        {
            if (declaration.Initializer is null)
            {
                return declaration;
            }

            var initializer = VisitExpression(declaration.Initializer);
            return initializer == declaration.Initializer
                ? declaration
                : new BoundLocalDeclaration(declaration.Local, initializer);
        }

        private BoundAssignment VisitAssignment(BoundAssignment assignment)
        {
            var left = VisitExpression(assignment.Left);
            var right = VisitExpression(assignment.Right);
            if (TryGetBinaryOperator(assignment.OperatorKind, out var binaryOperator))
            {
                // Compound assignment is not a VM op: x += y → x = x + y.
                right = MakeBinary(left, binaryOperator, right, assignment.Syntax);
                right = Convert(right, left.Type);
                var rewritten = new BoundAssignment(left, SyntaxKind.EqualsToken, right);
                rewritten.Syntax = assignment.Syntax;
                return rewritten;
            }

            if (left == assignment.Left && right == assignment.Right)
            {
                return assignment;
            }

            var result = new BoundAssignment(left, assignment.OperatorKind, right);
            result.Syntax = assignment.Syntax;
            return result;
        }

        private BoundExpression VisitProperty(BoundProperty property)
        {
            var receiver = property.Receiver is null ? null : VisitExpression(property.Receiver);
            if (property.Property.IsAuto
                && receiver is null or BoundSelf
                && property.Property.ContainingScript is { } owner
                && owner == _script)
            {
                return new BoundField(BackingField(property.Property), receiver: null);
            }

            return receiver == property.Receiver
                ? property
                : new BoundProperty(property.Property, receiver);
        }

        private BoundExpression VisitField(BoundField field)
        {
            if (field.Receiver is null)
            {
                return field;
            }

            var receiver = VisitExpression(field.Receiver);
            return receiver == field.Receiver ? field : new BoundField(field.Field, receiver);
        }

        private BoundExpression VisitBinary(BoundBinary binary)
        {
            var left = VisitExpression(binary.Left);
            var right = VisitExpression(binary.Right);
            if (left == binary.Left && right == binary.Right)
            {
                return binary;
            }

            return new BoundBinary(left, binary.OperatorKind, right, binary.Type);
        }

        private BoundExpression VisitUnary(BoundUnary unary)
        {
            var operand = VisitExpression(unary.Operand);
            return operand == unary.Operand
                ? unary
                : new BoundUnary(unary.OperatorKind, operand, unary.Type);
        }

        private BoundExpression VisitConversion(BoundConversion conversion)
        {
            var operand = VisitExpression(conversion.Operand);
            return operand == conversion.Operand
                ? conversion
                : new BoundConversion(operand, conversion.ConversionKind, conversion.Type);
        }

        private BoundExpression VisitCall(BoundCall call)
        {
            var receiver = call.Receiver is null ? null : VisitExpression(call.Receiver);
            var arguments = VisitArray(call.Arguments, VisitExpression);
            if (receiver == call.Receiver && arguments == call.Arguments)
            {
                return call;
            }

            return new BoundCall(receiver, call.Method, arguments, call.Type);
        }

        private BoundExpression VisitMemberAccess(BoundMemberAccess member)
        {
            var receiver = VisitExpression(member.Receiver);
            return receiver == member.Receiver
                ? member
                : new BoundMemberAccess(receiver, member.Member, member.Type);
        }

        private BoundExpression VisitIndex(BoundIndex index)
        {
            var expression = VisitExpression(index.Expression);
            var boundIndex = VisitExpression(index.Index);
            if (expression == index.Expression && boundIndex == index.Index)
            {
                return index;
            }

            return new BoundIndex(expression, boundIndex, index.Type);
        }

        private BoundExpression VisitIs(BoundIs @is)
        {
            var operand = VisitExpression(@is.Operand);
            return operand == @is.Operand ? @is : new BoundIs(operand, @is.TestType, @is.Type);
        }

        private BoundExpression VisitNewArray(BoundNewArray array)
        {
            var size = VisitExpression(array.Size);
            return size == array.Size
                ? array
                : new BoundNewArray(array.ElementType, size, array.Type);
        }

        private BoundExpression VisitArrayLength(BoundArrayLength length)
        {
            var array = VisitExpression(length.Array);
            return array == length.Array ? length : new BoundArrayLength(array, length.Type);
        }

        private BoundExpression MakeBinary(
            BoundExpression left,
            SyntaxKind operatorKind,
            BoundExpression right,
            SyntaxNode? syntax)
        {
            ITypeSymbol type;
            switch (operatorKind)
            {
                case SyntaxKind.PlusToken:
                    if (IsNamed(left.Type, "String") || IsNamed(right.Type, "String"))
                    {
                        type = IsNamed(left.Type, "String") ? left.Type : right.Type;
                        break;
                    }

                    type = NumericResultType(left.Type, right.Type);
                    left = Convert(left, type);
                    right = Convert(right, type);
                    break;

                case SyntaxKind.MinusToken:
                case SyntaxKind.AsteriskToken:
                case SyntaxKind.SlashToken:
                    type = NumericResultType(left.Type, right.Type);
                    left = Convert(left, type);
                    right = Convert(right, type);
                    break;

                case SyntaxKind.PercentToken:
                    type = NamedOrIntrinsic(left.Type, right.Type, "Int");
                    left = Convert(left, type);
                    right = Convert(right, type);
                    break;

                default:
                    type = left.Type;
                    break;
            }

            var binary = new BoundBinary(left, operatorKind, right, type);
            binary.Syntax = syntax ?? left.Syntax;
            return binary;
        }

        private static BoundExpression Convert(BoundExpression expression, ITypeSymbol destination)
        {
            if (expression.Type.TypeKind == TypeKind.Error || destination.TypeKind == TypeKind.Error)
            {
                return expression;
            }

            var isNoneLiteral = expression is BoundLiteral { Value: null };
            if (!TypeConversions.TryImplicit(expression.Type, destination, isNoneLiteral))
            {
                return expression;
            }

            var conversion = TypeConversions.ClassifyImplicit(expression.Type, destination, isNoneLiteral);
            if (conversion.Kind == ConversionKind.Identity)
            {
                return expression;
            }

            var converted = new BoundConversion(expression, ConversionKind.Implicit, conversion.ResultType);
            converted.Syntax = expression.Syntax;
            return converted;
        }

        private IVariableSymbol BackingField(IPropertySymbol property)
        {
            if (_backingFields.TryGetValue(property, out var field))
            {
                return field;
            }

            ISymbol containing = property.ContainingSymbol
                ?? property.ContainingScript
                ?? (ISymbol)property;
            field = new VariableSymbol(
                new PapyrusName("::" + property.Name.Text + "_var"),
                containing,
                property.ContainingScript,
                property.Type,
                isConst: false);
            _backingFields.Add(property, field);
            return field;
        }

        private ITypeSymbol NumericResultType(ITypeSymbol left, ITypeSymbol right)
        {
            if (IsNamed(left, "Float"))
            {
                return left;
            }

            if (IsNamed(right, "Float"))
            {
                return right;
            }

            return NamedOrIntrinsic(left, right, "Int");
        }

        private ITypeSymbol NamedOrIntrinsic(ITypeSymbol left, ITypeSymbol right, string name)
        {
            if (IsNamed(left, name))
            {
                return left;
            }

            if (IsNamed(right, name))
            {
                return right;
            }

            return IntrinsicTypeSymbol.Create(Names, name);
        }

        private NameTable Names => _names ??= new NameTable();

        private static bool TryGetBinaryOperator(SyntaxKind assignmentOperator, out SyntaxKind binaryOperator)
        {
            switch (assignmentOperator)
            {
                case SyntaxKind.PlusEqualsToken:
                    binaryOperator = SyntaxKind.PlusToken;
                    return true;
                case SyntaxKind.MinusEqualsToken:
                    binaryOperator = SyntaxKind.MinusToken;
                    return true;
                case SyntaxKind.AsteriskEqualsToken:
                    binaryOperator = SyntaxKind.AsteriskToken;
                    return true;
                case SyntaxKind.SlashEqualsToken:
                    binaryOperator = SyntaxKind.SlashToken;
                    return true;
                case SyntaxKind.PercentEqualsToken:
                    binaryOperator = SyntaxKind.PercentToken;
                    return true;
                default:
                    binaryOperator = default;
                    return false;
            }
        }

        private static ImmutableArray<T> VisitArray<T>(ImmutableArray<T> nodes, Func<T, T> visit)
            where T : BoundNode
        {
            if (nodes.IsDefaultOrEmpty)
            {
                return nodes;
            }

            ImmutableArray<T>.Builder? builder = null;
            for (var i = 0; i < nodes.Length; i++)
            {
                var rewritten = visit(nodes[i]);
                if (builder is null)
                {
                    if (rewritten == nodes[i])
                    {
                        continue;
                    }

                    builder = ImmutableArray.CreateBuilder<T>(nodes.Length);
                    for (var j = 0; j < i; j++)
                    {
                        builder.Add(nodes[j]);
                    }
                }

                builder.Add(rewritten);
            }

            return builder is null ? nodes : builder.MoveToImmutable();
        }

        private static bool IsNamed(ITypeSymbol type, string name)
            => type.TypeKind != TypeKind.Error
                && type.Name.Text.Equals(name, StringComparison.OrdinalIgnoreCase);
    }
}
