using System;
using Papyrus.CodeAnalysis.Symbols;

namespace Papyrus.CodeAnalysis.Binding;

internal enum BoundKind
{
    Literal,
    Local,
    Parameter,
    Field,
    Property,
    Self,
    Parent,
    TypeExpression,
    Binary,
    Unary,
    Assignment,
    Conversion,
    Call,
    MemberAccess,
    Index,
    Is,
    NewArray,
    New,
    ArrayLength,
    Block,
    If,
    While,
    Return,
    ExpressionStatement,
    LocalDeclaration,
    Function,
    Script,
    Error,
}

internal abstract class BoundNode
{
    protected BoundNode(ITypeSymbol? type)
    {
        Type = type;
    }

    public ITypeSymbol? Type { get; }

    public abstract BoundKind Kind { get; }
}

internal abstract class BoundExpression : BoundNode
{
    protected BoundExpression(ITypeSymbol type)
        : base(type)
    {
        ArgumentNullException.ThrowIfNull(type);
    }

    public new ITypeSymbol Type => base.Type!;
}

internal abstract class BoundStatement : BoundNode
{
    protected BoundStatement()
        : base(type: null)
    {
    }
}
