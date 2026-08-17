using System;
using System.Collections.Immutable;
using Papyrus.CodeAnalysis.Syntax;

namespace Papyrus.CodeAnalysis.Symbols;

internal sealed class ExpressionTypeBinder
{
    private readonly PapyrusCompilation _compilation;
    private readonly IScriptSymbol? _containingScript;

    public ExpressionTypeBinder(PapyrusCompilation compilation, IScriptSymbol? containingScript = null)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        _compilation = compilation;
        _containingScript = containingScript;
    }

    internal ITypeSymbol BindTypeName(TypeNameSyntax syntax) => ResolveType(syntax);

    internal static ITypeSymbol NumericResultType(ITypeSymbol left, ITypeSymbol right, NameTable names)
    {
        if (IsNamed(left, "Float") || IsNamed(right, "Float"))
        {
            return IntrinsicTypeSymbol.Create(names, "Float");
        }

        return IntrinsicTypeSymbol.Create(names, "Int");
    }

    public Conversion Bind(ExpressionSyntax expression, ITypeSymbol? expectedType = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var inferred = Infer(expression);
        if (expectedType is null || inferred.IsError)
        {
            return inferred;
        }

        var converted = TypeConversions.ClassifyImplicit(
            inferred.ResultType,
            expectedType,
            sourceIsNoneLiteral: IsNoneLiteral(expression));
        return Relocate(converted, expression);
    }

    private Conversion Infer(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return Infer(parenthesized.Expression);

            case LiteralExpressionSyntax literal:
                return BindLiteral(literal);

            case CastExpressionSyntax cast:
                return BindCast(cast);

            case NewExpressionSyntax created:
                return BindNew(created);

            case ElementAccessExpressionSyntax elementAccess:
                return BindElementAccess(elementAccess);

            case BinaryExpressionSyntax binary:
                return BindBinary(binary);

            case PrefixUnaryExpressionSyntax unary:
                return BindUnary(unary);

            default:
                return new Conversion(ConversionKind.Identity, ErrorType());
        }
    }

    private Conversion BindLiteral(LiteralExpressionSyntax literal)
    {
        var names = _compilation.Names;
        var type = literal.Token.Kind switch
        {
            SyntaxKind.IntegerLiteralToken or SyntaxKind.HexLiteralToken
                => IntrinsicTypeSymbol.Create(names, "Int"),
            SyntaxKind.FloatLiteralToken
                => IntrinsicTypeSymbol.Create(names, "Float"),
            SyntaxKind.StringLiteralToken
                => IntrinsicTypeSymbol.Create(names, "String"),
            SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword
                => IntrinsicTypeSymbol.Create(names, "Bool"),
            SyntaxKind.NoneKeyword
                => IntrinsicTypeSymbol.Create(names, "None"),
            _ => ErrorType(),
        };

        return new Conversion(ConversionKind.Identity, type);
    }

    private Conversion BindCast(CastExpressionSyntax cast)
    {
        var operand = Infer(cast.Expression);
        var target = ResolveType(cast.Type);
        Conversion result;
        if (cast.OperatorToken.Kind == SyntaxKind.IsKeyword)
        {
            result = TypeConversions.ClassifyIs(
                operand.ResultType,
                target,
                _compilation.Dialect,
                _compilation.Names);
        }
        else
        {
            result = TypeConversions.ClassifyAs(operand.ResultType, target, _compilation.Names);
        }

        if (operand.IsError)
        {
            return Relocate(operand, cast);
        }

        return Relocate(result, cast);
    }

    private Conversion BindNew(NewExpressionSyntax created)
    {
        var element = ResolveType(created.TypeName);
        if (created.OpenBracketToken.Kind == SyntaxKind.OpenBracketToken)
        {
            var array = TypeConversions.CreateArrayType(element, _compilation.Names, _compilation.Dialect);
            return new Conversion(ConversionKind.Identity, array);
        }

        return new Conversion(ConversionKind.Identity, element);
    }

    private Conversion BindBinary(BinaryExpressionSyntax binary)
    {
        var left = Infer(binary.Left);
        var right = Infer(binary.Right);
        var names = _compilation.Names;
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
                return new Conversion(ConversionKind.Identity, IntrinsicTypeSymbol.Create(names, "Bool"));

            case SyntaxKind.PlusToken:
                if (IsNamed(left.ResultType, "String") || IsNamed(right.ResultType, "String"))
                {
                    return new Conversion(ConversionKind.Identity, IntrinsicTypeSymbol.Create(names, "String"));
                }

                return new Conversion(ConversionKind.Identity, NumericResult(left.ResultType, right.ResultType));

            case SyntaxKind.MinusToken:
            case SyntaxKind.AsteriskToken:
            case SyntaxKind.SlashToken:
                return new Conversion(ConversionKind.Identity, NumericResult(left.ResultType, right.ResultType));

            case SyntaxKind.PercentToken:
                return new Conversion(ConversionKind.Identity, IntrinsicTypeSymbol.Create(names, "Int"));

            default:
                return new Conversion(ConversionKind.Identity, ErrorType());
        }
    }

    private Conversion BindUnary(PrefixUnaryExpressionSyntax unary)
    {
        if (unary.OperatorToken.Kind == SyntaxKind.ExclamationToken)
        {
            return new Conversion(ConversionKind.Identity, IntrinsicTypeSymbol.Create(_compilation.Names, "Bool"));
        }

        return Infer(unary.Operand);
    }

    private ITypeSymbol NumericResult(ITypeSymbol left, ITypeSymbol right)
    {
        if (IsNamed(left, "Float") || IsNamed(right, "Float"))
        {
            return IntrinsicTypeSymbol.Create(_compilation.Names, "Float");
        }

        return IntrinsicTypeSymbol.Create(_compilation.Names, "Int");
    }

    private static bool IsNamed(ITypeSymbol type, string name)
        => type.TypeKind != TypeKind.Error
            && type.Name.Text.Equals(name, StringComparison.OrdinalIgnoreCase);

    private Conversion BindElementAccess(ElementAccessExpressionSyntax elementAccess)
    {
        var array = Infer(elementAccess.Expression);
        var element = array.ResultType.GetArrayElementType();
        if (element is null)
        {
            return new Conversion(ConversionKind.Identity, ErrorType());
        }

        return new Conversion(ConversionKind.Identity, element);
    }

    private ITypeSymbol ResolveType(TypeNameSyntax syntax)
    {
        if (syntax.Name.IsMissing)
        {
            return ErrorType();
        }

        return ResolveType(_compilation.GetInternedTypeName(syntax));
    }

    private ITypeSymbol ResolveType(TypeName typeName)
    {
        if (typeName.IsArray)
        {
            var element = ResolveType(new TypeName(typeName.Name, isArray: false, typeName.StructName));
            return TypeConversions.CreateArrayType(element, _compilation.Names, _compilation.Dialect);
        }

        if (TryGetIntrinsic(typeName, _compilation.Names, out var intrinsic))
        {
            return intrinsic;
        }

        if (typeName.StructName is { } structName)
        {
            return ResolveStruct(typeName.Name, structName);
        }

        if (typeName.Name.Parts.Length == 1 && _containingScript is not null)
        {
            var local = FindStruct(_containingScript, typeName.Name.Parts[0]);
            if (local is not null)
            {
                return local;
            }
        }

        if (typeName.Name.Parts.IsDefaultOrEmpty)
        {
            return ErrorType();
        }

        return _compilation.GetScriptForType(typeName.Name);
    }

    private ITypeSymbol ResolveStruct(QualifiedName scriptName, PapyrusName structName)
    {
        IScriptSymbol? script;
        if (scriptName.Parts.IsDefaultOrEmpty
            || (_containingScript is not null && scriptName.Equals(_containingScript.QualifiedName)))
        {
            script = _containingScript;
        }
        else
        {
            script = _compilation.GetScriptForType(scriptName);
        }

        if (script is not null && script.TypeKind != TypeKind.Error)
        {
            var struc = FindStruct(script, structName);
            if (struc is not null)
            {
                return struc;
            }
        }

        return ErrorType(scriptName, structName);
    }

    private static IStructSymbol? FindStruct(IScriptSymbol script, PapyrusName structName)
    {
        var members = script.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IStructSymbol struc && struc.Name.Equals(structName))
            {
                return struc;
            }
        }

        return null;
    }

    private static bool TryGetIntrinsic(TypeName typeName, NameTable names, out ITypeSymbol intrinsic)
    {
        intrinsic = null!;
        if (typeName.IsArray || typeName.StructName is not null || typeName.Name.Parts.Length != 1)
        {
            return false;
        }

        var text = typeName.Name.Parts[0].Text;
        if (!IsIntrinsicName(text))
        {
            return false;
        }

        intrinsic = IntrinsicTypeSymbol.Create(names, text);
        return true;
    }

    private static bool IsIntrinsicName(string text)
    {
        return text.Equals("Int", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Float", StringComparison.OrdinalIgnoreCase)
            || text.Equals("String", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Bool", StringComparison.OrdinalIgnoreCase)
            || text.Equals("None", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Var", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNoneLiteral(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression is LiteralExpressionSyntax literal
            && literal.Token.Kind == SyntaxKind.NoneKeyword;
    }

    private Conversion Relocate(Conversion conversion, ExpressionSyntax expression)
    {
        if (conversion.Diagnostic is null)
        {
            return conversion;
        }

        return new Conversion(
            conversion.Kind,
            conversion.ResultType,
            new Diagnostic(conversion.Diagnostic.Info, expression.Span, expression.SyntaxTree));
    }

    private ITypeSymbol ErrorType() => ErrorType(default);

    private static ITypeSymbol ErrorType(QualifiedName name, PapyrusName? structName = null)
    {
        var parts = name.Parts.IsDefault ? ImmutableArray<PapyrusName>.Empty : name.Parts;
        var simple = structName ?? (parts.IsEmpty ? PapyrusName.Empty : parts[^1]);
        return new ScriptSymbol(simple, name, new TypeName(name, isArray: false, structName), isError: true);
    }
}
