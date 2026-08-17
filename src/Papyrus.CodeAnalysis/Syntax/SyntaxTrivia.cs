using System;

namespace Papyrus.CodeAnalysis.Syntax;

public readonly struct SyntaxTrivia : IEquatable<SyntaxTrivia>
{
    internal SyntaxTrivia(GreenNode? trivia)
    {
        Node = trivia;
    }

    internal GreenNode? Node { get; }

    public SyntaxKind Kind => Node?.Kind ?? SyntaxKind.None;

    public string Text => Node is InternalSyntax.GreenTrivia trivia ? trivia.Text : Node?.ToFullString() ?? "";

    public int FullWidth => Node?.FullWidth ?? 0;

    public string ToFullString() => Node?.ToFullString() ?? "";

    public override string ToString() => Text;

    public bool Equals(SyntaxTrivia other) => Node == other.Node;

    public override bool Equals(object? obj) => obj is SyntaxTrivia other && Equals(other);

    public override int GetHashCode() => Node?.GetHashCode() ?? 0;

    public static bool operator ==(SyntaxTrivia left, SyntaxTrivia right) => left.Equals(right);

    public static bool operator !=(SyntaxTrivia left, SyntaxTrivia right) => !left.Equals(right);
}
