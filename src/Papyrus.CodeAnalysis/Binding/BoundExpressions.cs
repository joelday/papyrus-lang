using System.Collections.Immutable;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;

namespace Papyrus.CodeAnalysis.Binding;

internal sealed class BoundLiteral : BoundExpression
{
    public BoundLiteral(object? value, ITypeSymbol type)
        : base(type)
    {
        Value = value;
    }

    public object? Value { get; }

    public override BoundKind Kind => BoundKind.Literal;
}

internal sealed class BoundLocal : BoundExpression
{
    public BoundLocal(IVariableSymbol local)
        : base(local.Type)
    {
        Local = local;
    }

    public IVariableSymbol Local { get; }

    public override BoundKind Kind => BoundKind.Local;
}

internal sealed class BoundParameter : BoundExpression
{
    public BoundParameter(IParameterSymbol parameter)
        : base(parameter.Type)
    {
        Parameter = parameter;
    }

    public IParameterSymbol Parameter { get; }

    public override BoundKind Kind => BoundKind.Parameter;
}

internal sealed class BoundField : BoundExpression
{
    public BoundField(IVariableSymbol field, BoundExpression? receiver = null)
        : base(field.Type)
    {
        Field = field;
        Receiver = receiver;
    }

    public IVariableSymbol Field { get; }

    public BoundExpression? Receiver { get; }

    public override BoundKind Kind => BoundKind.Field;
}

internal sealed class BoundProperty : BoundExpression
{
    public BoundProperty(IPropertySymbol property, BoundExpression? receiver = null)
        : base(property.Type)
    {
        Property = property;
        Receiver = receiver;
    }

    public IPropertySymbol Property { get; }

    public BoundExpression? Receiver { get; }

    public override BoundKind Kind => BoundKind.Property;
}

internal sealed class BoundSelf : BoundExpression
{
    public BoundSelf(IScriptSymbol script)
        : base(script)
    {
        Script = script;
    }

    public IScriptSymbol Script { get; }

    public override BoundKind Kind => BoundKind.Self;
}

internal sealed class BoundParent : BoundExpression
{
    public BoundParent(ITypeSymbol type)
        : base(type)
    {
    }

    public override BoundKind Kind => BoundKind.Parent;
}

internal sealed class BoundTypeExpression : BoundExpression
{
    public BoundTypeExpression(ITypeSymbol type)
        : base(type)
    {
    }

    public override BoundKind Kind => BoundKind.TypeExpression;
}

internal sealed class BoundBinary : BoundExpression
{
    public BoundBinary(BoundExpression left, SyntaxKind operatorKind, BoundExpression right, ITypeSymbol type)
        : base(type)
    {
        Left = left;
        OperatorKind = operatorKind;
        Right = right;
    }

    public BoundExpression Left { get; }

    public SyntaxKind OperatorKind { get; }

    public BoundExpression Right { get; }

    public override BoundKind Kind => BoundKind.Binary;
}

internal sealed class BoundUnary : BoundExpression
{
    public BoundUnary(SyntaxKind operatorKind, BoundExpression operand, ITypeSymbol type)
        : base(type)
    {
        OperatorKind = operatorKind;
        Operand = operand;
    }

    public SyntaxKind OperatorKind { get; }

    public BoundExpression Operand { get; }

    public override BoundKind Kind => BoundKind.Unary;
}

internal sealed class BoundConversion : BoundExpression
{
    public BoundConversion(BoundExpression operand, ConversionKind conversionKind, ITypeSymbol type)
        : base(type)
    {
        Operand = operand;
        ConversionKind = conversionKind;
    }

    public BoundExpression Operand { get; }

    public ConversionKind ConversionKind { get; }

    public override BoundKind Kind => BoundKind.Conversion;
}

internal sealed class BoundCall : BoundExpression
{
    public BoundCall(
        BoundExpression? receiver,
        IFunctionSymbol? method,
        ImmutableArray<BoundExpression> arguments,
        ITypeSymbol type)
        : base(type)
    {
        Receiver = receiver;
        Method = method;
        Arguments = arguments.IsDefault ? ImmutableArray<BoundExpression>.Empty : arguments;
    }

    public BoundExpression? Receiver { get; }

    public IFunctionSymbol? Method { get; }

    public ImmutableArray<BoundExpression> Arguments { get; }

    public override BoundKind Kind => BoundKind.Call;
}

internal sealed class BoundMemberAccess : BoundExpression
{
    public BoundMemberAccess(BoundExpression receiver, ISymbol member, ITypeSymbol type)
        : base(type)
    {
        Receiver = receiver;
        Member = member;
    }

    public BoundExpression Receiver { get; }

    public ISymbol Member { get; }

    public override BoundKind Kind => BoundKind.MemberAccess;
}

internal sealed class BoundIndex : BoundExpression
{
    public BoundIndex(BoundExpression expression, BoundExpression index, ITypeSymbol type)
        : base(type)
    {
        Expression = expression;
        Index = index;
    }

    public BoundExpression Expression { get; }

    public BoundExpression Index { get; }

    public override BoundKind Kind => BoundKind.Index;
}

internal sealed class BoundIs : BoundExpression
{
    public BoundIs(BoundExpression operand, ITypeSymbol testType, ITypeSymbol type)
        : base(type)
    {
        Operand = operand;
        TestType = testType;
    }

    public BoundExpression Operand { get; }

    public ITypeSymbol TestType { get; }

    public override BoundKind Kind => BoundKind.Is;
}

internal sealed class BoundNewArray : BoundExpression
{
    public BoundNewArray(ITypeSymbol elementType, BoundExpression size, ITypeSymbol type)
        : base(type)
    {
        ElementType = elementType;
        Size = size;
    }

    public ITypeSymbol ElementType { get; }

    public BoundExpression Size { get; }

    public override BoundKind Kind => BoundKind.NewArray;
}

internal sealed class BoundNew : BoundExpression
{
    public BoundNew(ITypeSymbol type)
        : base(type)
    {
    }

    public override BoundKind Kind => BoundKind.New;
}

internal sealed class BoundArrayLength : BoundExpression
{
    public BoundArrayLength(BoundExpression array, ITypeSymbol type)
        : base(type)
    {
        Array = array;
    }

    public BoundExpression Array { get; }

    public override BoundKind Kind => BoundKind.ArrayLength;
}

internal sealed class BoundErrorExpression : BoundExpression
{
    public BoundErrorExpression(ITypeSymbol type)
        : base(type)
    {
    }

    public override BoundKind Kind => BoundKind.Error;
}
