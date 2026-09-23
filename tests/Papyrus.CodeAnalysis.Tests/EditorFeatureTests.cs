using System;
using System.Collections.Generic;
using System.Linq;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

/// <summary>Semantic-model behavior behind completion, signature help, and hover docs.</summary>
public sealed class EditorFeatureTests
{
    // Documented the way the engine scripts are: `;` lines above the declaration.
    private const string Target =
        "ScriptName Target\n"
        + ";------------------\n"
        + "; Moves this object to the target\n"
        + "; and optionally matches rotation\n"
        + "Function MoveTo(Target akTarget, Bool abMatch = true) native\n"
        + "\n"
        + "; Unrelated section comment\n"
        + "\n"
        + "Function Undocumented() native\n"
        + ";/ Block\n   comment /;\n"
        + "Int Function Count() native\n"
        + "Target Function Self2() native\n";

    [Fact]
    public void Comment_lines_above_a_declaration_are_its_documentation()
    {
        var target = Compile("ScriptName Foo\n").GetScript(QualifiedName.Parse("Target"));

        Assert.Equal(
            "Moves this object to the target\nand optionally matches rotation",
            SymbolDisplay.GetDocumentation(Member(target, "MoveTo")));
        Assert.Null(SymbolDisplay.GetDocumentation(Member(target, "Undocumented")));
        Assert.Equal("Block\ncomment", SymbolDisplay.GetDocumentation(Member(target, "Count")));
    }

    [Fact]
    public void Doc_comment_wins_over_comment_above()
    {
        var foo = Compile("ScriptName Foo\n; above\nFunction Go()\n{Doc wins.}\nEndFunction\n")
            .GetScript(QualifiedName.Parse("Foo"));
        Assert.Equal("Doc wins.", SymbolDisplay.GetDocumentation(Member(foo, "Go")));
    }

    [Theory]
    [InlineData("  Other.\n")]
    [InlineData("  Other.Mo\n")]
    [InlineData("  Other.Self2().\n")]
    public void Member_completion_context_is_found_right_after_the_dot(string line)
    {
        var source = "ScriptName Foo\nTarget Property Other Auto\nFunction Go()\n" + line + "EndFunction\n";
        var model = Model(source);
        var position = source.IndexOf(line, StringComparison.Ordinal) + line.TrimEnd('\n').Length;

        Assert.True(model.TryGetMemberAccessContainer(position, out var container, TestContext.Current.CancellationToken));
        Assert.Equal("Target", container!.Name.Text);
        Assert.Contains(model.LookupSymbols(position, container), static s => s.Name.Text == "MoveTo");
    }

    [Fact]
    public void Signature_help_context_is_found_in_an_unterminated_call()
    {
        const string line = "  Other.MoveTo(Other, ";
        var source = "ScriptName Foo\nTarget Property Other Auto\nFunction Go()\n" + line + "\nEndFunction\n";
        var model = Model(source);
        var position = source.IndexOf(line, StringComparison.Ordinal) + line.Length;

        Assert.True(model.TryGetInvocationContext(position, out var function, out var active, TestContext.Current.CancellationToken));
        Assert.Equal("MoveTo", function!.Name.Text);
        Assert.Equal(1, active);
    }

    private static PapyrusCompilation Compile(string foo)
    {
        var sources = new Dictionary<QualifiedName, SourceText>
        {
            [QualifiedName.Parse("Foo")] = SourceText.From(foo),
            [QualifiedName.Parse("Target")] = SourceText.From(Target),
        };
        return PapyrusCompilation.Create(PapyrusDialect.Fallout4, new TestScriptResolver(sources));
    }

    private static SemanticModel Model(string foo)
    {
        var compilation = Compile(foo);
        return compilation.GetSemanticModel(compilation.GetSyntaxTree(QualifiedName.Parse("Foo")));
    }

    private static ISymbol Member(IScriptSymbol script, string name)
        => script.GetMembers().Single(member => member.Name.Text == name);
}
