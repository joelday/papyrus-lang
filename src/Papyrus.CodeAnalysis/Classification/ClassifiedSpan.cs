using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Classification;

public readonly struct ClassifiedSpan
{
    public ClassifiedSpan(TextSpan span, ClassificationKind kind, bool isDeclaration = false)
    {
        Span = span;
        Kind = kind;
        IsDeclaration = isDeclaration;
    }

    public TextSpan Span { get; }

    public ClassificationKind Kind { get; }

    public bool IsDeclaration { get; }

    public override string ToString()
        => IsDeclaration ? $"{Span} {Kind} (declaration)" : $"{Span} {Kind}";
}
