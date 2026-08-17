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
    public void Default_and_omitted_optional_tokens_are_not_missing()
    {
        Assert.False(default(SyntaxToken).IsMissing);
        Assert.Equal(SyntaxKind.None, default(SyntaxToken).Kind);

        var declaration = CreateMinimalScript("Hello");

        Assert.False(declaration.DocComment.IsMissing);
        Assert.False(declaration.NativeKeyword.IsMissing);
        Assert.False(declaration.HiddenKeyword.IsMissing);
        Assert.False(declaration.ConditionalKeyword.IsMissing);
        Assert.Null(declaration.ExtendsClause);
        Assert.False(declaration.IsMissing);
    }

    [Fact]
    public void Identifier_name_is_missing_when_its_token_is_missing()
    {
        var name = SyntaxFactory.IdentifierName(SyntaxFactory.MissingToken(SyntaxKind.IdentifierToken));

        Assert.True(name.IsMissing);
        Assert.True(name.Identifier.IsMissing);
        Assert.False(SyntaxFactory.IdentifierName(SyntaxFactory.Identifier("Foo")).IsMissing);
    }

    [Fact]
    public void Node_is_missing_only_when_every_present_child_is_missing()
    {
        var allMissing = SyntaxFactory.ScriptDeclaration(
            docComment: default,
            scriptNameKeyword: SyntaxFactory.MissingToken(SyntaxKind.ScriptNameKeyword),
            name: SyntaxFactory.IdentifierName(SyntaxFactory.MissingToken(SyntaxKind.IdentifierToken)),
            extendsClause: null,
            nativeKeyword: default,
            hiddenKeyword: default,
            conditionalKeyword: default,
            endOfLineToken: SyntaxFactory.MissingToken(SyntaxKind.EndOfLineToken));

        Assert.True(allMissing.IsMissing);

        var mixed = CreateMinimalScript("Hello");
        mixed = mixed.WithName(
            SyntaxFactory.IdentifierName(SyntaxFactory.MissingToken(SyntaxKind.IdentifierToken)));

        Assert.False(mixed.IsMissing);
    }

    [Fact]
    public void Node_children_are_lazily_materialized_through_the_slot_cache()
    {
        var declaration = CreateMinimalScript("Hello");

        Assert.Null(declaration.GetCachedSlot(2));
        Assert.Null(declaration.GetCachedSlot(3));

        var name = declaration.Name;

        Assert.NotNull(name);
        Assert.Same(name, declaration.GetCachedSlot(2));
        Assert.Equal("Hello", name.Identifier.Text);
        Assert.Null(declaration.ExtendsClause);
        Assert.Null(declaration.GetCachedSlot(3));
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

    private static ScriptDeclarationSyntax CreateMinimalScript(string identifier)
    {
        var space = SyntaxFactory.Whitespace(" ");
        return SyntaxFactory.ScriptDeclaration(
            docComment: default,
            scriptNameKeyword: SyntaxFactory.Token(SyntaxKind.ScriptNameKeyword),
            name: SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(identifier, leading: space)),
            extendsClause: null,
            nativeKeyword: default,
            hiddenKeyword: default,
            conditionalKeyword: default,
            endOfLineToken: SyntaxFactory.Token(SyntaxKind.EndOfLineToken, "\n"));
    }
}
