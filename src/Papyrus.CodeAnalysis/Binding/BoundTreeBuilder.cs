using System.Collections.Generic;
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

    private List<Diagnostic>? _diagnostics;

    /// <summary>
    /// When true, binding records type, argument, and read-only errors (see
    /// <see cref="Diagnostics"/>). Off for emit, which binds already-checked code.
    /// </summary>
    internal bool ReportDiagnostics
    {
        get => _diagnostics is not null;
        init => _diagnostics = value ? new List<Diagnostic>() : null;
    }

    internal ImmutableArray<Diagnostic> Diagnostics => _diagnostics is null ? ImmutableArray<Diagnostic>.Empty : [.. _diagnostics];

    // Nothing is reported on syntax that already has errors: the parser reported those.
    private void Report(DiagnosticDescriptor descriptor, SyntaxNode? node, params object?[] arguments)
    {
        if (_diagnostics is null || node is null || node.ContainsDiagnostics)
        {
            return;
        }

        _diagnostics.Add(new Diagnostic(new DiagnosticInfo(descriptor, arguments), node.Span, node.SyntaxTree));
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
            SyntaxList<StatementSyntax> statements = default;
            SyntaxNode? declaration = null;
            switch (function)
            {
                case FunctionSymbol { Declaration: { } syntax }:
                    statements = syntax.Statements;
                    declaration = syntax;
                    break;
                case EventSymbol { Declaration: { } syntax }:
                    statements = syntax.Statements;
                    declaration = syntax;
                    break;
            }

            return Attach(new BoundFunction(function, BindBlock(statements)), declaration);
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
        bound.Syntax ??= expression;
        if (expectedType is null)
        {
            return bound;
        }

        if (_diagnostics is not null
            && !TypeConversions.CanImplicitlyCoerce(bound.Type, expectedType, bound is BoundLiteral { Value: null }))
        {
            Report(
                DiagnosticCatalog.CannotConvert,
                expression,
                SymbolDisplay.TypeText(bound.Type),
                SymbolDisplay.TypeText(expectedType));
        }

        return Convert(bound, expectedType);
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
                return Attach(
                    new BoundExpressionStatement(BindExpression(expression.Expression)),
                    expression);
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

        return Attach(new BoundLocalDeclaration(local, initializer), declare);
    }

    private BoundAssignment BindAssignment(AssignmentStatementSyntax assignment)
    {
        var left = BindExpression(assignment.Left);
        if (ReadOnlyTarget(left) is { } readOnly)
        {
            Report(DiagnosticCatalog.ReadOnlyAssignment, assignment.Left, readOnly.Name.Text);
        }

        var expected = assignment.OperatorToken.Kind == SyntaxKind.EqualsToken ? left.Type : null;
        var right = BindExpression(assignment.Right, expected);
        return Attach(new BoundAssignment(left, assignment.OperatorToken.Kind, right), assignment);
    }

    private static ISymbol? ReadOnlyTarget(BoundExpression target)
    {
        return target switch
        {
            BoundLocal { Local: { IsConst: true } local } => local,
            BoundField { Field: { IsConst: true } field } => field,
            BoundProperty { Property: PropertySymbol property } when IsReadOnly(property) => property,
            _ => null,
        };
    }

    private static bool IsReadOnly(PropertySymbol property)
    {
        if (property.Declaration is { } declaration)
        {
            for (var i = 0; i < declaration.Flags.Count; i++)
            {
                if (declaration.Flags.Get(i).FlagToken.Kind is SyntaxKind.AutoReadOnlyKeyword or SyntaxKind.ConstKeyword)
                {
                    return true;
                }
            }
        }

        return !property.IsAuto && property.SetMethod is null;
    }

    private BoundReturn BindReturn(ReturnStatementSyntax statement)
    {
        if (statement.Expression is null)
        {
            return Attach(new BoundReturn(expression: null), statement);
        }

        var expected = _function?.ReturnType;
        if (expected is not null && IsNamed(expected, "None"))
        {
            expected = null;
        }

        return Attach(new BoundReturn(BindExpression(statement.Expression, expected)), statement);
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
            @else = Attach(
                new BoundIf(
                    BindExpression(clause.Condition, BoolType()),
                    BindBlock(clause.Statements),
                    @else),
                clause);
        }

        return Attach(new BoundIf(condition, then, @else), statement);
    }

    private BoundWhile BindWhile(WhileStatementSyntax statement)
    {
        return Attach(
            new BoundWhile(
                BindExpression(statement.Condition, BoolType()),
                BindBlock(statement.Statements)),
            statement);
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

            if (IsBuiltinStateField(name))
            {
                return new BoundField(
                    new VariableSymbol(_compilation.Names.Get("::State"), _script!, _script, StringType(), isConst: false),
                    receiver: null);
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
            var script = _compilation.GetScriptForType(qualified, _script?.QualifiedName);
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
            return new BoundNewArray(element, size, array, created.Span);
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
                // A call names a function even when a local, property, or state shares the
                // name (Caprica's tryResolveFunctionIdentifier only considers functions).
                method = ModelOf(name).GetSymbolInfo(name) as IFunctionSymbol ?? LookupFunction(name);
                if (method is { IsGlobal: false } && _script is not null)
                {
                    receiver = new BoundSelf(_script);
                }

                break;

            case MemberAccessExpressionSyntax member:
                receiver = BindExpression(member.Expression);
                method = TryGetSimpleName(member.Name, out var memberName)
                    ? FindFunction(receiver.Type, memberName)
                    : null;
                break;

            default:
                receiver = BindExpression(syntax.Expression);
                break;
        }

        var arguments = BindArguments(syntax.ArgumentList, method, receiver);
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

    private ImmutableArray<BoundExpression> BindArguments(
        ArgumentListSyntax list,
        IFunctionSymbol? method,
        BoundExpression? receiver)
    {
        if (method is not null)
        {
            CheckArguments(list, method);
        }

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

            if (IntrinsicTypeNames.IsEventName(expected, out var isCustomEvent))
            {
                // Event names are strings in PEX. Custom events are addressed by the
                // declaring script: the event is looked up on the receiver (argument 0)
                // or the preceding argument's type, and the literal becomes
                // "DeclaringScript_EventName" (Caprica's PapyrusFunctionCallExpression).
                var bound = BindExpression(argument.Expression, StringType());
                if (isCustomEvent)
                {
                    var owner = i == 0 ? receiver?.Type ?? _script : Unconverted(builder[i - 1]).Type;
                    bound = QualifyCustomEventName(bound, owner);
                }

                builder.Add(bound);
                continue;
            }

            builder.Add(BindExpression(argument.Expression, expected));
        }

        return builder.MoveToImmutable();
    }

    // The type an argument had before implicit conversion to its parameter type.
    private static BoundExpression Unconverted(BoundExpression expression)
    {
        while (expression is BoundConversion { ConversionKind: ConversionKind.Implicit } conversion)
        {
            expression = conversion.Operand;
        }

        return expression;
    }

    // Argument counts and names against the resolved function's parameters.
    private void CheckArguments(ArgumentListSyntax list, IFunctionSymbol method)
    {
        if (_diagnostics is null)
        {
            return;
        }

        var parameters = method.Parameters;
        var supplied = new bool[parameters.Length];
        var positional = 0;
        var unknownName = false;
        for (var i = 0; i < list.Arguments.Count; i++)
        {
            var argument = list.Arguments.Get(i);
            if (argument.Name is { } name && TryGetSimpleName(name, out var argumentName))
            {
                var index = -1;
                for (var p = 0; p < parameters.Length; p++)
                {
                    if (parameters[p].Name.Equals(argumentName))
                    {
                        index = p;
                        break;
                    }
                }

                if (index < 0)
                {
                    Report(DiagnosticCatalog.UnknownNamedArgument, name, method.Name.Text, argumentName.Text);
                    unknownName = true;
                }
                else
                {
                    supplied[index] = true;
                }

                continue;
            }

            if (positional < parameters.Length)
            {
                supplied[positional] = true;
            }
            else if (positional == parameters.Length)
            {
                Report(
                    DiagnosticCatalog.TooManyArguments,
                    argument,
                    method.Name.Text,
                    parameters.Length,
                    list.Arguments.Count);
            }

            positional++;
        }

        // A misspelled parameter name already explains the missing value.
        for (var p = 0; !unknownName && p < parameters.Length; p++)
        {
            if (!supplied[p] && !parameters[p].HasDefaultValue)
            {
                Report(DiagnosticCatalog.MissingArgument, list, parameters[p].Name.Text, method.Name.Text);
                break;
            }
        }
    }

    private BoundExpression QualifyCustomEventName(BoundExpression argument, ITypeSymbol? owner)
    {
        if (argument is not BoundLiteral { Value: string eventName }
            || owner is not IScriptSymbol script
            || string.IsNullOrEmpty(eventName))
        {
            return argument;
        }

        var customEvent = ScriptHierarchy.FindCustomEvent(script, _compilation.Names.Get(eventName));
        if (customEvent?.ContainingScript is not { } declaringScript)
        {
            return argument;
        }

        return Attach(
            new BoundLiteral(declaringScript.QualifiedName + "_" + eventName, argument.Type),
            argument.Syntax);
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

        return Attach(
            new BoundConversion(expression, ConversionKind.Implicit, conversion.ResultType),
            expression.Syntax);
    }

    // `__state` is the VM's current-state variable, `::State`. ScriptObject's GetState and
    // GotoState are the only functions that may name it (Caprica's BuiltinStateField).
    private bool IsBuiltinStateField(PapyrusName name)
    {
        return _script is not null
            && _function is not null
            && name.Text.Equals("__state", StringComparison.OrdinalIgnoreCase)
            && (_function.Name.Text.Equals("GetState", StringComparison.OrdinalIgnoreCase)
                || _function.Name.Text.Equals("GotoState", StringComparison.OrdinalIgnoreCase));
    }

    private ISymbol? Lookup(NameExpressionSyntax syntax)
    {
        if (!TryGetSimpleName(syntax.Name, out var name))
        {
            return null;
        }

        if (_function is not null)
        {
            if (LocalScopes.Find(_function.Locals, name, syntax.Span.Start) is { } local)
            {
                return local;
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

        if (_script is null)
        {
            return null;
        }

        // Own and inherited members first, then imported scripts' globals.
        return FindMember(_script, name) ?? SourceScriptSymbol.FindImportedGlobalFunction(_script, name);
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

        return LocalScopes.FindDeclared(_function.Locals, declare)
            ?? LocalScopes.Find(_function.Locals, name, declare.Span.Start);
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

    private IFunctionSymbol? LookupFunction(NameExpressionSyntax syntax)
    {
        if (_script is null || !TryGetSimpleName(syntax.Name, out var name))
        {
            return null;
        }

        return FindFunction(_script, name) ?? SourceScriptSymbol.FindImportedGlobalFunction(_script, name);
    }

    private static IFunctionSymbol? FindFunction(ITypeSymbol? type, PapyrusName name)
    {
        // Extends cycles are reported (PAP1002) elsewhere; just don't loop on them.
        var visited = new System.Collections.Generic.HashSet<ITypeSymbol>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        for (var current = type; current is not null && current.TypeKind != TypeKind.Error && visited.Add(current);)
        {
            foreach (var member in current.GetMembers(name))
            {
                if (member is IFunctionSymbol function)
                {
                    return function;
                }
            }

            current = current is IScriptSymbol script ? script.BaseType : null;
        }

        return null;
    }

    private ISymbol? FindMember(ITypeSymbol? type, IdentifierNameSyntax syntax)
    {
        return TryGetSimpleName(syntax, out var name) ? FindMember(type, name) : null;
    }

    private static ISymbol? FindMember(ITypeSymbol? type, PapyrusName name)
        => ScriptHierarchy.FindMember(type, name);

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

    private static T Attach<T>(T node, SyntaxNode? syntax) where T : BoundNode
    {
        if (syntax is not null)
        {
            node.Syntax = syntax;
        }

        return node;
    }
}
