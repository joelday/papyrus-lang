namespace Papyrus.CodeAnalysis.Syntax;

internal static partial class SyntaxFactory
{
    public static SyntaxTrivia Trivia(SyntaxKind kind, string text)
    {
        return new SyntaxTrivia(new InternalSyntax.GreenTrivia(kind, text));
    }

    public static SyntaxTrivia Whitespace(string text)
        => Trivia(SyntaxKind.WhitespaceTrivia, text);

    public static SyntaxTrivia EndOfLine(string text = "\n")
        => Trivia(SyntaxKind.EndOfLineTrivia, text);

    public static SyntaxToken Token(
        SyntaxKind kind,
        string? text = null,
        SyntaxTriviaList leading = default,
        SyntaxTriviaList trailing = default)
    {
        return CreateToken(kind, text ?? SyntaxFacts.GetText(kind) ?? "", isMissing: false, leading, trailing);
    }

    public static SyntaxToken Identifier(
        string text,
        SyntaxTriviaList leading = default,
        SyntaxTriviaList trailing = default)
    {
        return Token(SyntaxKind.IdentifierToken, text, leading, trailing);
    }

    public static SyntaxToken MissingToken(
        SyntaxKind kind,
        SyntaxTriviaList leading = default,
        SyntaxTriviaList trailing = default)
    {
        return CreateToken(kind, text: "", isMissing: true, leading, trailing);
    }

    private static SyntaxToken CreateToken(
        SyntaxKind kind,
        string text,
        bool isMissing,
        SyntaxTriviaList leading,
        SyntaxTriviaList trailing)
    {
        var green = new InternalSyntax.GreenToken(kind, text, leading.Node, trailing.Node, isMissing);
        return new SyntaxToken(green, parent: null, position: 0);
    }
}
