using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class ParserTests
{
    [Fact]
    public void Parse_script_name_round_trips()
    {
        const string source = "ScriptName Foo\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(SyntaxKind.ScriptDeclaration, root.Kind);
        Assert.False(root.ScriptNameKeyword.IsMissing);
        Assert.Equal("Foo", root.Name.Identifier.Text);
        Assert.Null(root.ExtendsClause);
        Assert.Empty(root.Imports);
    }

    [Fact]
    public void Parse_script_name_extends_round_trips()
    {
        const string source = "ScriptName Foo Extends Bar\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal("Foo", root.Name.Identifier.Text);
        Assert.NotNull(root.ExtendsClause);
        Assert.Equal("Bar", ((IdentifierNameSyntax)root.ExtendsClause.TypeName.Name).Identifier.Text);
    }

    [Fact]
    public void Missing_script_name_produces_missing_token_and_does_not_throw()
    {
        var tree = SyntaxTree.Parse("Foo\n", PapyrusDialect.SkyrimSe);

        Assert.Equal("Foo\n", tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.True(root.ScriptNameKeyword.IsMissing);
        Assert.Equal(0, root.ScriptNameKeyword.FullWidth);
        Assert.Equal("Foo", root.Name.Identifier.Text);
        Assert.False(root.Name.IsMissing);
    }

    [Fact]
    public void Empty_input_is_all_missing_and_does_not_throw()
    {
        var tree = SyntaxTree.Parse(string.Empty, PapyrusDialect.SkyrimSe);

        Assert.Equal(string.Empty, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.True(root.ScriptNameKeyword.IsMissing);
        Assert.True(root.Name.IsMissing);
        Assert.True(root.EndOfLineToken.IsMissing);
    }

    [Fact]
    public void Parse_imports_and_flags()
    {
        const string source = "ScriptName Foo Hidden Conditional\nImport Utility\nImport Game\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(SyntaxKind.HiddenKeyword, root.HiddenKeyword.Kind);
        Assert.Equal(SyntaxKind.ConditionalKeyword, root.ConditionalKeyword.Kind);
        Assert.Equal(2, root.Imports.Count);
        Assert.Equal("Utility", ((IdentifierNameSyntax)root.Imports.Get(0).Name.Name).Identifier.Text);
        Assert.Equal("Game", ((IdentifierNameSyntax)root.Imports.Get(1).Name.Name).Identifier.Text);
    }

    [Fact]
    public void Doc_comment_attaches_to_script_declaration()
    {
        const string source = "{A script}\nScriptName Foo\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(SyntaxKind.DocCommentToken, root.DocComment.Kind);
        Assert.Equal("{A script}", root.DocComment.Text);
    }

    [Fact]
    public void Garbage_input_does_not_throw_and_round_trips()
    {
        const string source = "@#$\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.True(root.ScriptNameKeyword.IsMissing);
    }

    [Fact]
    public void Parse_source_text_with_options()
    {
        const string source = "ScriptName Foo\n";
        var tree = SyntaxTree.Parse(SourceText.From(source), PapyrusParseOptions.Default);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.Same(PapyrusDialect.SkyrimSe, tree.Dialect);
    }
}
