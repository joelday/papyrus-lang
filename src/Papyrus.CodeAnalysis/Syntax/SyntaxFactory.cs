using System.Collections.Generic;
using Papyrus.CodeAnalysis.Syntax.InternalSyntax;

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
        var green = SyntaxTokenCache.Get(kind, text, leading.Node, trailing.Node, isMissing);
        return new SyntaxToken(green, parent: null, position: 0);
    }

    public static ScriptDeclarationSyntax ScriptDeclaration(
        SyntaxToken docComment,
        SyntaxToken scriptNameKeyword,
        IdentifierNameSyntax name,
        ExtendsClauseSyntax? extendsClause,
        SyntaxToken nativeKeyword,
        SyntaxToken hiddenKeyword,
        SyntaxToken conditionalKeyword,
        SyntaxToken endOfLineToken)
    {
        return ScriptDeclaration(
            docComment,
            scriptNameKeyword,
            name,
            extendsClause,
            Flags(nativeKeyword, hiddenKeyword, conditionalKeyword),
            endOfLineToken,
            imports: default,
            members: default,
            endOfFileToken: default);
    }

    private static SyntaxList<FlagSyntax> Flags(
        SyntaxToken nativeKeyword,
        SyntaxToken hiddenKeyword,
        SyntaxToken conditionalKeyword)
    {
        var flags = new List<FlagSyntax>();
        AddFlag(flags, nativeKeyword);
        AddFlag(flags, hiddenKeyword);
        AddFlag(flags, conditionalKeyword);
        return List(flags);
    }

    private static void AddFlag(List<FlagSyntax> flags, SyntaxToken token)
    {
        if (token.Kind != SyntaxKind.None)
        {
            flags.Add(Flag(token));
        }
    }

    public static SyntaxList<T> List<T>(IReadOnlyList<T> items) where T : SyntaxNode
    {
        if (items.Count == 0)
        {
            return default;
        }

        if (items.Count == 1)
        {
            return new SyntaxList<T>(items[0].Green);
        }

        var greens = new GreenNode[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            greens[i] = items[i].Green;
        }

        return new SyntaxList<T>(new GreenList(greens));
    }
}
