using System;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Classification;

/// <summary>Refinements of a <see cref="ClassificationKind"/>; bit order is the LSP legend order.</summary>
[Flags]
public enum ClassificationModifiers
{
    None = 0,

    /// <summary>The name being declared (not a reference to it).</summary>
    Declaration = 1 << 0,

    /// <summary>A Const variable, or an AutoReadOnly / Const property.</summary>
    ReadOnly = 1 << 1,

    /// <summary>A Global function.</summary>
    Static = 1 << 2,

    /// <summary>A <c>{...}</c> doc comment.</summary>
    Documentation = 1 << 3,
}

public readonly struct ClassifiedSpan
{
    public ClassifiedSpan(TextSpan span, ClassificationKind kind, bool isDeclaration = false)
        : this(span, kind, isDeclaration ? ClassificationModifiers.Declaration : ClassificationModifiers.None)
    {
    }

    public ClassifiedSpan(TextSpan span, ClassificationKind kind, ClassificationModifiers modifiers)
    {
        Span = span;
        Kind = kind;
        Modifiers = modifiers;
    }

    public TextSpan Span { get; }

    public ClassificationKind Kind { get; }

    public ClassificationModifiers Modifiers { get; }

    public bool IsDeclaration => (Modifiers & ClassificationModifiers.Declaration) != 0;

    public override string ToString()
        => Modifiers == ClassificationModifiers.None ? $"{Span} {Kind}" : $"{Span} {Kind} ({Modifiers})";
}
