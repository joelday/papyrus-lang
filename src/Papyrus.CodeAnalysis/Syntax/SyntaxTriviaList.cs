namespace Papyrus.CodeAnalysis.Syntax;

public readonly struct SyntaxTriviaList
{
    internal SyntaxTriviaList(GreenNode? node)
    {
        Node = node;
    }

    internal GreenNode? Node { get; }

    public int FullWidth => Node?.FullWidth ?? 0;

    public bool IsEmpty => Node is null || Node.FullWidth == 0;

    public string ToFullString() => Node?.ToFullString() ?? "";

    public static implicit operator SyntaxTriviaList(SyntaxTrivia trivia) => new(trivia.Node);
}
