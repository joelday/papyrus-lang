using System;
using System.Collections.Generic;
using System.Linq;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

/// <summary>
/// What the editor's Problems view shows: syntax errors are reported once, bad references
/// in bodies are reported, and one mistake does not cascade into several diagnostics.
/// </summary>
public sealed class DiagnosticQualityTests
{
    private const string Other =
        "ScriptName Other\nFunction Ping()\nEndFunction\nInt Property Count Auto\n";

    private const string Utility =
        "ScriptName Utility Hidden\nFunction Wait(Float afSeconds) Global Native\n";

    [Fact]
    public void Unterminated_call_is_one_syntax_error()
    {
        var diagnostic = Assert.Single(Diagnostics("Function Go()\n  Ping(\nEndFunction\nFunction Ping()\nEndFunction\n"));
        Assert.Equal(DiagnosticCatalog.ExpectedTokenId, diagnostic.Id);
        Assert.Contains("')'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Garbled_declaration_reports_the_bad_type_and_one_syntax_error_without_cascading()
    {
        // WorkshopInitializeLocationScript with a stray space typed into the type name.
        var diagnostics = Diagnostics("RefCol lectionAlias Property SettlementNPCs Auto Const\n");

        Assert.Contains(diagnostics, static d => d.Id == DiagnosticCatalog.UnresolvedTypeId && d.Message.Contains("'RefCol'"));
        Assert.Single(diagnostics, static d => d.Id is DiagnosticCatalog.ExpectedTokenId or DiagnosticCatalog.UnexpectedTokenId);
        Assert.DoesNotContain(diagnostics, static d => d.Message.Contains("''", StringComparison.Ordinal));
        Assert.Equal(2, diagnostics.Count);
    }

    [Fact]
    public void Missing_type_is_a_syntax_error_not_an_unresolved_type()
    {
        var diagnostic = Assert.Single(Diagnostics("Property Orphan Auto\n"));
        Assert.Equal(DiagnosticCatalog.ExpectedTokenId, diagnostic.Id);
    }

    [Theory]
    [InlineData("  Target.Nope = 1\n", DiagnosticCatalog.MemberNotFoundId, "'Other' does not contain a member named 'Nope'")]
    [InlineData("  Target.Nope()\n", DiagnosticCatalog.UnresolvedFunctionId, "'Other.Nope'")]
    [InlineData("  nope = 1\n", DiagnosticCatalog.UndefinedNameId, "'nope'")]
    [InlineData("  Nope()\n", DiagnosticCatalog.UnresolvedFunctionId, "'Nope'")]
    public void Bad_references_in_bodies_are_reported(string statement, string id, string message)
    {
        var diagnostic = Assert.Single(Diagnostics($"Other Property Target Auto\nFunction Go()\n{statement}EndFunction\n"));
        Assert.Equal(id, diagnostic.Id);
        Assert.Contains(message, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unresolved_receiver_is_reported_once_not_per_member()
    {
        var diagnostic = Assert.Single(Diagnostics("Function Go()\n  Missing.Thing().Other = 1\nEndFunction\n"));
        Assert.Equal(DiagnosticCatalog.UndefinedNameId, diagnostic.Id);
        Assert.Contains("'Missing'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Valid_code_has_no_diagnostics()
    {
        Assert.Empty(Diagnostics(
            "import Utility\n"
            + "Other Property Target Auto\n"
            + "Int[] values\n"
            + "\n"
            + "{A stray doc comment documents nothing.}\n"
            + "Function Go(Int i)\n"
            + "{Go.}\n"
            + "  Wait(1.0)\n"
            + "  Utility.Wait(values.Length)\n"
            + "  Self.Go(Target.Count)\n"
            + "  Target.Ping()\n"
            + "  values[i] = 2\n"
            + "  Int x = values[0] Const\n"
            + "EndFunction"));
    }

    private static List<Diagnostic> Diagnostics(string members)
    {
        var sources = new Dictionary<QualifiedName, SourceText>
        {
            [QualifiedName.Parse("Foo")] = SourceText.From("ScriptName Foo\n" + members),
            [QualifiedName.Parse("Other")] = SourceText.From(Other),
            [QualifiedName.Parse("Utility")] = SourceText.From(Utility),
        };
        var compilation = PapyrusCompilation.Create(PapyrusDialect.Fallout4, new TestScriptResolver(sources));
        return compilation.GetDiagnostics(QualifiedName.Parse("Foo"), TestContext.Current.CancellationToken).ToList();
    }
}
