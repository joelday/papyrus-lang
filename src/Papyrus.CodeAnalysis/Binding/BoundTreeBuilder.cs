using System;
using System.Collections.Immutable;
using System.Globalization;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;

namespace Papyrus.CodeAnalysis.Binding;

internal sealed class BoundTreeBuilder
{
    private readonly PapyrusCompilation _compilation;
    private readonly IScriptSymbol? _script;
    private readonly ExpressionTypeBinder _types;
    private IFunctionSymbol? _function;

    public BoundTreeBuilder(
        PapyrusCompilation compilation,
        IScriptSymbol? containingScript = null,
        IFunctionSymbol? function = null)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        _compilation = compilation;
        _script = containingScript;
        _function = function;
        _types = new ExpressionTypeBinder(compilation, containingScript);
    }

    public BoundScript BindScript(IScriptSymbol script)
    {
        ArgumentNullException.ThrowIfNull(script);

        var builder = ImmutableArray.CreateBuilder<BoundFunction>();
        AddFunctions(script.GetMembers(), builder);
        return new BoundScript(script, builder.ToImmutable());
    }

    public BoundFunction BindFunction(IFunctionSymbol function)
    {
        ArgumentNullException.ThrowIfNull(function);

        var previous = _function;
        _function = function;
        try
        {
            var statements = function switch
            {
                FunctionSymbol { Declaration: { } syntax } => syntax.Statements,
                EventSymbol { Declaration: { } syntax } => syntax.Statements,
                _ => default,
            };

            return new BoundFunction(function, BindBlock(statements));
        }
        finally
        {
            _function = previous;
        }
    }

    public BoundExpression BindExpression(ExpressionSyntax expression, ITypeSymbol? expectedType = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var bound = BindExpressionCore(expression);
        return expectedType is null ? bound : Convert(bound, expectedType);
    }

    private void AddFunctions(ImmutableArray<ISymbol> members, ImmutableArray<BoundFunction>.Builder builder)
    {
        for (var i = 0; i < members.Length; i++)
        {
            switch (members[i])
            {
                case IFunctionSymbol function:
                    builder.Add(BindFunction(function));
                    break;
                case IPropertySymbol property:
                    if (property.GetMethod is { } getter)
                    {
                        builder.Add(BindFunction(getter));
                    }

                    if (property.SetMethod is { } setter)
                    {
                        builder.Add(BindFunction(setter));
                    }

                    break;
                case IStateSymbol state:
                    AddFunctions(state.GetMembers(), builder);
                    break;
            }
        }
    }

    private BoundBlock BindBlock(SyntaxList<StatementSyntax> statements)
    {
        var count = statements.Count;
        if (count == 0)
        {
            return new BoundBlock(ImmutableArray<BoundStatement>.Empty);
        }

        var builder = ImmutableArray.CreateBuilder<BoundStatement>(count);
        for (var i = 0; i < count; i++)
        {
            var bound = BindStatement(statements.Get(i));
            if (bound is not null)
            {
                builder.Add(bound);
            }
        }

        return new BoundBlock(builder.ToImmutable());
    }

    private BoundStatement? BindStatement(StatementSyntax statement)
    {
        switch (statement)
        {
            case DeclareStatementSyntax declare:
                return BindLocalDeclaration(declare);
            case AssignmentStatementSyntax assignment:
                return BindAssignment(assignment);
            case ReturnStatementSyntax ret:
                return BindReturn(ret);
            case IfStatementSyntax ifStatement:
                return BindIf(ifStatement);
            case WhileStatementSyntax whileStatement:
                return BindWhile(whileStatement);
            case ExpressionStatementSyntax expression:
                return new BoundExpressionStatement(BindExpression(expression.Expression));
            case GuardStatementSyntax guard:
                return BindBlock(guard.Statements);
            case TryGuardStatementSyntax tryGuard:
                return BindBlock(tryGuard.Statements);
            default:
                return null;
        }
    }

    private BoundLocalDeclaration BindLocalDeclaration(DeclareStatementSyntax declare)
    {
        var local = FindLocal(declare) ?? CreateErrorLocal(declare);
        BoundExpression? initializer = null;
        if (declare.EqualsValue is { } equals)
        {
            initializer = BindExpression(equals.Value, local.Type);
        }

        return new BoundLocalDeclaration(local, initializer);
    }

    private BoundAssignment BindAssignment(AssignmentStatementSyntax assignment)
    {
        var left = BindExpression(assignment.Left);
        var expected = assignment.OperatorToken.Kind == SyntaxKind.EqualsToken ? left.Type : null;
        var right = BindExpression(assignment.Right, expected);
        return new BoundAssignment(left, assignment.OperatorToken.Kind, right);
    }

    private BoundReturn BindReturn(ReturnStatementSyntax statement)
    {
        if (statement.Expression is null)
        {
            return new BoundReturn(expression: null);
        }

        var expected = _function?.ReturnType;
        if (expected is not null && IsNamed(expected, "None"))
        {
            expected = null;
        }

        return new BoundReturn(BindExpression(statement.Expression, expected));
    }

    private BoundIf BindIf(IfStatementSyntax statement)
    {
        var condition = BindExpression(statement.Condition, BoolType());
        var then = BindBlock(statement.Statements);
        BoundStatement? @else = null;

        if (statement.ElseClause is { } elseClause)
        {
            @else = BindBlock(elseClause.Statements);
        }

        var elseIfs = statement.ElseIfClauses;
        for (var i = elseIfs.Count - 1; i >= 0; i--)
        {
            var clause = elseIfs.Get(i);
            @else = new BoundIf(
                BindExpression(clause.Condition, BoolType()),
                BindBlock(clause.Statements),
                @else);
        }

        return new BoundIf(condition, then, @else);
    }

    private BoundWhile BindWhile(WhileStatementSyntax statement)
    {
        return new BoundWhile(
            BindExpression(statement.Condition, BoolType()),
            BindBlock(statement.Statements));
    }

    private BoundExpression BindExpressionCore(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return BindExpressionCore(parenthesized.Expression);
            case LiteralExpressionSyntax literal:
                return BindLiteral(literal);
            case NameExpressionSyntax name:
                return BindName(name);
            case PrefixUnaryExpressionSyntax unary:
                return BindUnary(unary);
            case BinaryExpressionSyntax binary:
                return BindBinary(binary);
            case CastExpressionSyntax cast:
                return BindCast(cast);
            case NewExpressionSyntax created:
                return BindNew(created);
            case ElementAccessExpressionSyntax index:
                return BindIndex(index);
            case InvocationExpressionSyntax invocation:
                return BindCall(invocation);
            case MemberAccessExpressionSyntax member:
                return BindMemberAccess(member);
            default:
                return new BoundErrorExpression(ErrorType());
        }
    }

    private BoundLiteral BindLiteral(LiteralExpressionSyntax literal)
    {
        var type = literal.Token.Kind switch
        {
            SyntaxKind.IntegerLiteralToken or SyntaxKind.HexLiteralToken => IntType(),
            SyntaxKind.FloatLiteralToken => FloatType(),
            SyntaxKind.StringLiteralToken => StringType(),
            SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword => BoolType(),
            SyntaxKind.NoneKeyword => NoneType(),
            _ => ErrorType(),
        };

        return new BoundLiteral(ParseLiteral(literal.Token), type);
    }

    private BoundExpression BindName(NameExpressionSyntax syntax)
    {
        if (TryGetSimpleName(syntax.Name, out var name))
        {
            if (name.Equals(_compilation.Names.Get("Self")) && _script is not null)
            {
                return new BoundSelf(_script);
            }

            if (name.Equals(_compilation.Names.Get("Parent")))
            {
                var parent = _script?.BaseType;
                return new BoundParent(parent is { TypeKind: not TypeKind.Error } ? parent : ErrorType());
            }
        }

        var symbol = ModelOf(syntax).GetSymbolInfo(syntax) ?? Lookup(syntax);
        var bound = BindSymbolReference(symbol, receiver: null);
        if (bound is not BoundErrorExpression)
        {
            return bound;
        }

        if (TryGetQualifiedName(syntax.Name, out var qualified))
        {
            var script = _compilation.GetScriptForType(qualified);
            if (script.TypeKind != TypeKind.Error)
            {
                return new BoundTypeExpression(script);
            }
        }

        return bound;
    }

    private BoundExpression BindSymbolReference(ISymbol? symbol, BoundExpression? receiver)
    {
        switch (symbol)
        {
            case IParameterSymbol parameter:
                return new BoundParameter(parameter);
            case IVariableSymbol variable when variable.ContainingSymbol is IFunctionSymbol:
                return new BoundLocal(variable);
            case IVariableSymbol field:
                return new BoundField(field, receiver);
            case IPropertySymbol property:
                return new BoundProperty(property, receiver);
            case IFunctionSymbol:
                return receiver ?? (_script is null ? new BoundErrorExpression(ErrorType()) : new BoundSelf(_script));
            case ITypeSymbol type:
                return new BoundTypeExpression(type);
            default:
                return new BoundErrorExpression(ErrorType());
        }
    }

    private BoundUnary BindUnary(PrefixUnaryExpressionSyntax unary)
    {
        if (unary.OperatorToken.Kind == SyntaxKind.ExclamationToken)
        {
            return new BoundUnary(
                unary.OperatorToken.Kind,
                BindExpression(unary.Operand, BoolType()),
                BoolType());
        }

        var operand = BindExpression(unary.Operand);
        return new BoundUnary(unary.OperatorToken.Kind, operand, operand.Type);
    }

    private BoundBinary BindBinary(BinaryExpressionSyntax binary)
    {
        var left = BindExpression(binary.Left);
        var right = BindExpression(binary.Right);
        var names = _compilation.Names;
        ITypeSymbol type;
        switch (binary.OperatorToken.Kind)
        {
            case SyntaxKind.EqualsEqualsToken:
            case SyntaxKind.ExclamationEqualsToken:
            case SyntaxKind.LessThanToken:
            case SyntaxKind.LessThanEqualsToken:
            case SyntaxKind.GreaterThanToken:
            case SyntaxKind.GreaterThanEqualsToken:
            case SyntaxKind.AmpersandAmpersandToken:
            case SyntaxKind.BarBarToken:
                type = BoolType();
                if (binary.OperatorToken.Kind is SyntaxKind.AmpersandAmpersandToken or SyntaxKind.BarBarToken)
                {
                    left = Convert(left, type);
                    right = Convert(right, type);
                }
                else if (IsNamed(left.Type, "Float") || IsNamed(right.Type, "Float"))
                {
                    left = Convert(left, FloatType());
                    right = Convert(right, FloatType());
                }

                break;

            case SyntaxKind.PlusToken:
                if (IsNamed(left.Type, "String") || IsNamed(right.Type, "String"))
                {
                    type = StringType();
                    break;
                }

                type = ExpressionTypeBinder.NumericResultType(left.Type, right.Type, names);
                left = Convert(left, type);
                right = Convert(right, type);
                break;

            case SyntaxKind.MinusToken:
            case SyntaxKind.AsteriskToken:
            case SyntaxKind.SlashToken:
                type = ExpressionTypeBinder.NumericResultType(left.Type, right.Type, names);
                left = Convert(left, type);
                right = Convert(right, type);
                break;

            case SyntaxKind.PercentToken:
                type = IntType();
                left = Convert(left, type);
                right = Convert(right, type);
                break;

            default:
                type = ErrorType();
                break;
        }

        return new BoundBinary(left, binary.OperatorToken.Kind, right, type);
    }

    private BoundExpression BindCast(CastExpressionSyntax cast)
    {
        var operand = BindExpression(cast.Expression);
        var target = _types.BindTypeName(cast.Type);
        if (cast.OperatorToken.Kind == SyntaxKind.IsKeyword)
        {
            var result = TypeConversions.ClassifyIs(
                operand.Type,
                target,
                _compilation.Dialect,
                _compilation.Names);
            return new BoundIs(operand, target, result.ResultType);
        }

        var conversion = TypeConversions.ClassifyAs(operand.Type, target, _compilation.Names);
        var kind = conversion.Kind is ConversionKind.Identity
            or ConversionKind.Implicit
            or ConversionKind.Explicit
            ? ConversionKind.Explicit
            : conversion.Kind;
        return new BoundConversion(operand, kind, conversion.ResultType);
    }

    private BoundExpression BindNew(NewExpressionSyntax created)
    {
        var element = _types.BindTypeName(created.TypeName);
        if (created.OpenBracketToken.Kind == SyntaxKind.OpenBracketToken)
        {
            var array = TypeConversions.CreateArrayType(element, _compilation.Names, _compilation.Dialect);
            var size = created.Size is null
                ? new BoundLiteral(0, IntType())
                : BindExpression(created.Size, IntType());
            return new BoundNewArray(element, size, array);
        }

        return new BoundNew(element);
    }

    private BoundExpression BindIndex(ElementAccessExpressionSyntax syntax)
    {
        var array = BindExpression(syntax.Expression);
        var index = BindExpression(syntax.Argument, IntType());
        var element = array.Type.GetArrayElementType();
        return new BoundIndex(array, index, element ?? ErrorType());
    }

    private BoundCall BindCall(InvocationExpressionSyntax syntax)
    {
        BoundExpression? receiver = null;
        IFunctionSymbol? method = null;
        switch (syntax.Expression)
        {
            case NameExpressionSyntax name:
                method = (ModelOf(name).GetSymbolInfo(name) ?? Lookup(name)) as IFunctionSymbol;
                if (method is { IsGlobal: false } && _script is not null)
                {
                    receiver = new BoundSelf(_script);
                }

                break;

            case MemberAccessExpressionSyntax member:
                receiver = BindExpression(member.Expression);
                method = FindMember(receiver.Type, member.Name) as IFunctionSymbol;
                break;

            default:
                receiver = BindExpression(syntax.Expression);
                break;
        }

        var arguments = BindArguments(syntax.ArgumentList, method);
        var type = method?.ReturnType ?? ErrorType();
        return new BoundCall(receiver, method, arguments, type);
    }

    private BoundExpression BindMemberAccess(MemberAccessExpressionSyntax syntax)
    {
        var receiver = BindExpression(syntax.Expression);
        if (IsLength(syntax.Name) && receiver.Type.TypeKind == TypeKind.Array)
        {
            return new BoundArrayLength(receiver, IntType());
        }

        var member = FindMember(receiver.Type, syntax.Name);
        var bound = BindSymbolReference(member, receiver);
        if (bound is BoundErrorExpression && member is not null)
        {
            var type = TypeOf(member) ?? ErrorType();
            return new BoundMemberAccess(receiver, member, type);
        }

        return bound;
    }

    private ImmutableArray<BoundExpression> BindArguments(ArgumentListSyntax list, IFunctionSymbol? method)
    {
        var arguments = list.Arguments;
        var count = arguments.Count;
        if (count == 0)
        {
            return ImmutableArray<BoundExpression>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<BoundExpression>(count);
        var parameters = method?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty;
        for (var i = 0; i < count; i++)
        {
            var argument = arguments.Get(i);
            ITypeSymbol? expected = null;
            if (argument.Name is { } name && TryGetSimpleName(name, out var argumentName))
            {
                for (var p = 0; p < parameters.Length; p++)
                {
                    if (parameters[p].Name.Equals(argumentName))
                    {
                        expected = parameters[p].Type;
                        break;
                    }
                }
            }
            else if ((uint)i < (uint)parameters.Length)
            {
                expected = parameters[i].Type;
            }

            builder.Add(BindExpression(argument.Expression, expected));
        }

        return builder.MoveToImmutable();
    }

    private BoundExpression Convert(BoundExpression expression, ITypeSymbol destination)
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

        return new BoundConversion(expression, ConversionKind.Implicit, conversion.ResultType);
    }

    private ISymbol? Lookup(NameExpressionSyntax syntax)
    {
        if (!TryGetSimpleName(syntax.Name, out var name))
        {
            return null;
        }

        if (_function is not null)
        {
            var locals = _function.Locals;
            for (var i = 0; i < locals.Length; i++)
            {
                if (locals[i].Name.Equals(name))
                {
                    return locals[i];
                }
            }

            var parameters = _function.Parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].Name.Equals(name))
                {
                    return parameters[i];
                }
            }
        }

        return _script is null ? null : FindMember(_script, name);
    }

    private IVariableSymbol? FindLocal(DeclareStatementSyntax declare)
    {
        if (ModelOf(declare).GetDeclaredSymbol(declare) is IVariableSymbol declared)
        {
            return declared;
        }

        if (_function is null || !TryGetSimpleName(declare.Name, out var name))
        {
            return null;
        }

        var locals = _function.Locals;
        for (var i = 0; i < locals.Length; i++)
        {
            if (locals[i].Name.Equals(name))
            {
                return locals[i];
            }
        }

        return null;
    }

    private SemanticModel ModelOf(SyntaxNode node) => _compilation.GetSemanticModel(node.SyntaxTree);

    private IVariableSymbol CreateErrorLocal(DeclareStatementSyntax declare)
    {
        TryGetSimpleName(declare.Name, out var name);
        return new VariableSymbol(
            name,
            _function ?? (ISymbol?)_script ?? new ScriptSymbol(PapyrusName.Empty, default, new TypeName(default), isError: true),
            _script,
            _types.BindTypeName(declare.TypeName),
            isConst: false);
    }

    private ISymbol? FindMember(ITypeSymbol? type, IdentifierNameSyntax syntax)
    {
        return TryGetSimpleName(syntax, out var name) ? FindMember(type, name) : null;
    }

    private static ISymbol? FindMember(ITypeSymbol? type, PapyrusName name)
    {
        if (type is null)
        {
            return null;
        }

        var members = type.GetMembers(name);
        if (!members.IsEmpty)
        {
            return members[0];
        }

        if (type is IScriptSymbol script && script.BaseType is { TypeKind: not TypeKind.Error } baseType)
        {
            return FindMember(baseType, name);
        }

        return null;
    }

    private bool TryGetSimpleName(NameSyntax syntax, out PapyrusName name)
    {
        if (syntax is IdentifierNameSyntax identifier)
        {
            return TryGetSimpleName(identifier, out name);
        }

        name = default;
        return false;
    }

    private bool TryGetSimpleName(IdentifierNameSyntax syntax, out PapyrusName name)
    {
        if (syntax.IsMissing || syntax.Identifier.IsMissing || syntax.Identifier.Text.Length == 0)
        {
            name = default;
            return false;
        }

        name = _compilation.Names.Get(syntax.Identifier.Text);
        return true;
    }

    private bool TryGetQualifiedName(NameSyntax syntax, out QualifiedName name)
    {
        if (syntax.IsMissing)
        {
            name = default;
            return false;
        }

        name = _compilation.GetInternedQualifiedName(syntax);
        return !name.Parts.IsDefaultOrEmpty;
    }

    private static ITypeSymbol? TypeOf(ISymbol symbol)
    {
        return symbol switch
        {
            ITypeSymbol type => type,
            IFunctionSymbol function => function.ReturnType,
            IPropertySymbol property => property.Type,
            IVariableSymbol variable => variable.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null,
        };
    }

    private static bool IsLength(IdentifierNameSyntax syntax)
        => syntax.Identifier.Kind == SyntaxKind.LengthKeyword
            || syntax.Identifier.Text.Equals("Length", StringComparison.OrdinalIgnoreCase);

    private static bool IsNamed(ITypeSymbol type, string name)
        => type.TypeKind != TypeKind.Error
            && type.Name.Text.Equals(name, StringComparison.OrdinalIgnoreCase);

    private static object? ParseLiteral(SyntaxToken token)
    {
        switch (token.Kind)
        {
            case SyntaxKind.IntegerLiteralToken:
                return int.TryParse(token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
                    ? integer
                    : 0;
            case SyntaxKind.HexLiteralToken:
                var hex = token.Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? token.Text[2..]
                    : token.Text;
                return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexValue)
                    ? hexValue
                    : 0;
            case SyntaxKind.FloatLiteralToken:
                return float.TryParse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating)
                    ? floating
                    : 0f;
            case SyntaxKind.StringLiteralToken:
                var text = token.Text;
                if (text.Length >= 2 && text[0] == '"')
                {
                    return text[^1] == '"' ? text[1..^1] : text[1..];
                }

                return text;
            case SyntaxKind.TrueKeyword:
                return true;
            case SyntaxKind.FalseKeyword:
                return false;
            default:
                return null;
        }
    }

    private ITypeSymbol IntType() => IntrinsicTypeSymbol.Create(_compilation.Names, "Int");

    private ITypeSymbol FloatType() => IntrinsicTypeSymbol.Create(_compilation.Names, "Float");

    private ITypeSymbol BoolType() => IntrinsicTypeSymbol.Create(_compilation.Names, "Bool");

    private ITypeSymbol StringType() => IntrinsicTypeSymbol.Create(_compilation.Names, "String");

    private ITypeSymbol NoneType() => IntrinsicTypeSymbol.Create(_compilation.Names, "None");

    private static ITypeSymbol ErrorType()
        => new ScriptSymbol(PapyrusName.Empty, default, new TypeName(default), isError: true);
}
