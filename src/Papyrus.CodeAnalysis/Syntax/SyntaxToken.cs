using System;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Syntax;

public readonly struct SyntaxToken : IEquatable<SyntaxToken>
{
    internal SyntaxToken(GreenNode? token, SyntaxNode? parent, int position)
    {
        Node = token;
        Parent = parent;
        Position = position;
    }

    internal GreenNode? Node { get; }

    public SyntaxNode? Parent { get; }

    public int Position { get; }

    public SyntaxKind Kind => Node?.Kind ?? SyntaxKind.None;

    public bool IsMissing => Node is null || Node.IsMissing;

    public int FullWidth => Node?.FullWidth ?? 0;

    public string Text => Node is InternalSyntax.GreenToken token ? token.Text : "";

    public TextSpan FullSpan => new(Position, FullWidth);

    public TextSpan Span
    {
        get
        {
            var leading = Node?.GetLeadingTriviaWidth() ?? 0;
            var trailing = Node?.GetTrailingTriviaWidth() ?? 0;
            return new TextSpan(Position + leading, FullWidth - leading - trailing);
        }
    }

    public SyntaxTriviaList LeadingTrivia => new(Node is InternalSyntax.GreenToken token ? token.Leading : null);

    public SyntaxTriviaList TrailingTrivia => new(Node is InternalSyntax.GreenToken token ? token.Trailing : null);

    public bool ContainsDiagnostics => Node is not null && Node.ContainsDiagnostics;

    public string ToFullString() => Node?.ToFullString() ?? "";

    public override string ToString() => Text;

    public bool Equals(SyntaxToken other)
        => Node == other.Node && Parent == other.Parent && Position == other.Position;

    public override bool Equals(object? obj) => obj is SyntaxToken other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Node, Parent, Position);

    public static bool operator ==(SyntaxToken left, SyntaxToken right) => left.Equals(right);

    public static bool operator !=(SyntaxToken left, SyntaxToken right) => !left.Equals(right);
}
