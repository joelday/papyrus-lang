using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class GreenRedTreeTests
{
    [Fact]
    public void ToFullString_includes_trivia_when_green_tree_is_wrapped_as_red()
    {
        var space = SyntaxFactory.Whitespace(" ");
        var scriptName = SyntaxFactory.Token(SyntaxKind.ScriptNameKeyword);
        var identifier = SyntaxFactory.Identifier("Hello", leading: space);
        var name = SyntaxFactory.IdentifierName(identifier);
        var eol = SyntaxFactory.Token(SyntaxKind.EndOfLineToken, "\n");
        var declaration = SyntaxFactory.ScriptDeclaration(
            docComment: default,
            scriptNameKeyword: scriptName,
            name: name,
            extendsClause: null,
            nativeKeyword: default,
            hiddenKeyword: default,
            conditionalKeyword: default,
            endOfLineToken: eol);

        var tree = SyntaxTree.Create(declaration);

        Assert.Equal("ScriptName Hello\n", tree.GetRoot().ToFullString());
        Assert.Equal("ScriptName Hello\n", declaration.ToFullString());
        Assert.Equal(SyntaxKind.ScriptDeclaration, tree.GetRoot().Kind);
        Assert.Same(tree, tree.GetRoot().SyntaxTree);
    }

    [Fact]
    public void Missing_token_is_missing_and_has_zero_width()
    {
        var missing = SyntaxFactory.MissingToken(SyntaxKind.IdentifierToken);

        Assert.True(missing.IsMissing);
        Assert.Equal(0, missing.FullWidth);
        Assert.Equal(string.Empty, missing.ToFullString());
        Assert.Equal(string.Empty, missing.Text);
        Assert.Equal(SyntaxKind.IdentifierToken, missing.Kind);
    }

    [Fact]
    public void Generated_WithX_returns_updated_text_without_mutating_original()
    {
        var original = SyntaxFactory.IdentifierName(SyntaxFactory.Identifier("Foo"));
        var updated = original.WithIdentifier(SyntaxFactory.Identifier("Bar"));

        Assert.Equal("Foo", original.ToFullString());
        Assert.Equal("Bar", updated.ToFullString());
        Assert.Equal("Bar", updated.Identifier.Text);
    }

    [Fact]
    public void Qualified_name_round_trips_through_red_facade()
    {
        var left = SyntaxFactory.IdentifierName(SyntaxFactory.Identifier("A"));
        var colon = SyntaxFactory.Token(SyntaxKind.ColonToken);
        var right = SyntaxFactory.IdentifierName(SyntaxFactory.Identifier("B"));
        var name = SyntaxFactory.QualifiedName(left, colon, right);
        var tree = SyntaxTree.Create(name);

        Assert.Equal("A:B", tree.GetRoot().ToFullString());
        Assert.Equal("A:B", name.ToFullString());
    }
}
