using System.Collections.Generic;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class ChampollionCompatTests
{
    [Fact]
    public void Lexer_without_compat_splits_colon_colon_identifier()
    {
        var tokens = Lex("::temp0\n", compat: false);

        Assert.Equal(SyntaxKind.ColonToken, tokens[0].Kind);
        Assert.Equal(":", tokens[0].Text);
        Assert.Equal(SyntaxKind.ColonToken, tokens[1].Kind);
        Assert.Equal(SyntaxKind.IdentifierToken, tokens[2].Kind);
        Assert.Equal("temp0", tokens[2].Text);
    }

    [Fact]
    public void Lexer_with_compat_keeps_colon_colon_identifier()
    {
        var tokens = Lex("::temp0\n", compat: true);

        Assert.Equal(SyntaxKind.IdentifierToken, tokens[0].Kind);
        Assert.Equal("::temp0", tokens[0].Text);
        Assert.Equal(SyntaxKind.EndOfLineToken, tokens[1].Kind);
    }

    [Fact]
    public void Parse_temp_local_and_script_struct_type()
    {
        var tree = SyntaxTree.Parse(
            SourceText.From(CompatSource),
            PapyrusDialect.Fallout4,
            CompatOptions(),
            TestContext.Current.CancellationToken);

        Assert.Equal(CompatSource, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(2, root.Members.Count);

        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(1));
        var parameter = function.ParameterList.Parameters.Get(0);
        Assert.Equal("Foo", ((IdentifierNameSyntax)parameter.TypeName.Name).Identifier.Text);
        Assert.Equal(SyntaxKind.HashToken, parameter.TypeName.HashToken.Kind);
        Assert.Equal("Point", parameter.TypeName.StructName.Text);

        var declare = Assert.IsType<DeclareStatementSyntax>(function.Statements.Get(0));
        Assert.Equal("::temp0", declare.Name.Identifier.Text);
        Assert.Equal("Int", ((IdentifierNameSyntax)declare.TypeName.Name).Identifier.Text);
    }

    [Fact]
    public void Parse_without_compat_does_not_treat_temp_as_identifier()
    {
        var tree = SyntaxTree.Parse(
            SourceText.From(CompatSource),
            PapyrusDialect.Fallout4,
            PapyrusParseOptions.Default,
            TestContext.Current.CancellationToken);
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(1));
        var hasTemp = false;
        for (var i = 0; i < function.Statements.Count; i++)
        {
            if (function.Statements.Get(i) is DeclareStatementSyntax declare
                && declare.Name.Identifier.Text == "::temp0")
            {
                hasTemp = true;
                break;
            }
        }

        Assert.False(hasTemp);
    }

    [Fact]
    public void Bind_temp_local_and_script_struct_parameter()
    {
        var name = QualifiedName.Parse("Foo");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.Fallout4,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [name] = SourceText.From(CompatSource),
            }),
            CompatOptions());

        var script = compilation.GetScript(name);
        var names = compilation.Names;
        var bar = Assert.IsAssignableFrom<IFunctionSymbol>(Assert.Single(script.GetMembers(names.Get("Bar"))));

        var origin = Assert.Single(bar.Parameters);
        Assert.Equal("origin", origin.Name.Text);
        Assert.Equal(TypeKind.Struct, origin.Type.TypeKind);
        Assert.Equal("Point", origin.Type.TypeName.StructName?.Text);
        Assert.Equal("Foo#Point", origin.Type.TypeName.ToString());

        var temp = Assert.Single(bar.Locals, static local => local.Name.Text == "::temp0");
        Assert.Equal("Int", temp.Type.Name.Text);

        var tree = compilation.GetSyntaxTree(name);
        var model = compilation.GetSemanticModel(tree);
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(1));
        var declare = Assert.IsType<DeclareStatementSyntax>(function.Statements.Get(0));
        var declared = Assert.IsAssignableFrom<IVariableSymbol>(
            model.GetDeclaredSymbol(declare, TestContext.Current.CancellationToken));
        Assert.Same(temp, declared);

        Assert.DoesNotContain(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static d => d.Severity == DiagnosticSeverity.Error);
    }

    private const string CompatSource =
        "ScriptName Foo\n" +
        "Struct Point\nFloat X\nEndStruct\n" +
        "Int Function Bar(Foo#Point origin)\n" +
        "  Int ::temp0 = 1\n" +
        "  Return ::temp0\n" +
        "EndFunction\n";

    private static PapyrusParseOptions CompatOptions()
        => new() { ChampollionCompat = true };

    private static List<SyntaxToken> Lex(string source, bool compat)
    {
        var lexer = new Lexer(
            SourceText.From(source),
            PapyrusDialect.SkyrimSe,
            new PapyrusParseOptions { ChampollionCompat = compat });

        var tokens = new List<SyntaxToken>();
        SyntaxToken token;
        do
        {
            token = lexer.Lex();
            tokens.Add(token);
        }
        while (token.Kind != SyntaxKind.EndOfFileToken);

        return tokens;
    }
}
