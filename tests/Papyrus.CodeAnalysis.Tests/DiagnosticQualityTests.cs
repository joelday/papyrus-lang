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

    [Theory]
    [InlineData("  Int x = \"text\"\n", DiagnosticCatalog.CannotConvertId, "'String' to 'Int'")]
    [InlineData("  Other o = 5\n", DiagnosticCatalog.CannotConvertId, "'Int' to 'Other'")]
    [InlineData("  Take(1.5)\n", DiagnosticCatalog.CannotConvertId, "'Float' to 'Int'")]
    [InlineData("  Take(1, 2)\n", DiagnosticCatalog.TooManyArgumentsId, "takes 1 argument(s) but 2")]
    [InlineData("  Take()\n", DiagnosticCatalog.MissingArgumentId, "parameter 'a'")]
    [InlineData("  Take(b = 1)\n", DiagnosticCatalog.UnknownNamedArgumentId, "no parameter named 'b'")]
    [InlineData("  Limit = 2\n", DiagnosticCatalog.ReadOnlyAssignmentId, "'Limit' is read-only")]
    [InlineData("  Max = 2\n", DiagnosticCatalog.ReadOnlyAssignmentId, "'Max' is read-only")]
    [InlineData("  Int y = 1\n  If true\n    Int y = 2\n  EndIf\n", DiagnosticCatalog.DuplicateDeclarationId, "'y' is already declared")]
    public void Type_argument_readonly_and_duplicate_errors_in_bodies(string body, string id, string message)
    {
        var diagnostic = Assert.Single(Diagnostics(
            "Int Property Limit = 1 AutoReadOnly\nInt Max = 3 Const\nFunction Take(Int a)\nEndFunction\n"
            + "Function Go()\n" + body + "EndFunction\n"));
        Assert.Equal(id, diagnostic.Id);
        Assert.Contains(message, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Coercions_the_CK_accepts_are_not_errors()
    {
        Assert.Empty(Diagnostics(
            "Function Go(Other o)\n"
            + "  String s = 5\n"          // anything but None becomes a String
            + "  Bool b = o\n"            // ...or a Bool
            + "  Float f = 1\n"           // Int becomes Float
            + "  Var v = o\n"             // anything but an array becomes Var
            + "  Other n = None\n"
            + "  Int y = 1\n  If b\n    Int z = 2\n  Else\n    Int z = 3\n  EndIf\n" // sibling blocks
            + "EndFunction\n"));
    }

    [Fact]
    public void Duplicate_members_are_reported_once_per_extra_declaration()
    {
        var diagnostic = Assert.Single(Diagnostics("Int Property A Auto\nInt A\nFunction F()\nEndFunction\n"));
        Assert.Equal(DiagnosticCatalog.DuplicateDeclarationId, diagnostic.Id);
    }

    [Fact]
    public void Function_that_can_fall_off_its_end_gets_a_warning()
    {
        var diagnostic = Assert.Single(Diagnostics("Int Function F(Bool b)\n  If b\n    Return 1\n  EndIf\nEndFunction\n"));
        Assert.Equal(DiagnosticCatalog.NotAllPathsReturnId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Empty(Diagnostics("Int Function F(Bool b)\n  If b\n    Return 1\n  EndIf\n  Return 0\nEndFunction\n"));
    }

    [Fact]
    public void Non_call_use_of_a_name_shared_by_a_property_and_a_function_is_the_property()
    {
        // MS13Script: `Function DivideSpoils(...)` and `Bool Property DivideSpoils`.
        Assert.Empty(Diagnostics(
            "Function DivideSpoils(Float cut = 0.5)\nEndFunction\nBool Property DivideSpoils = true Auto\n"
            + "Function Go(Foo q)\n  q.DivideSpoils = false\n  DivideSpoils = true\n  q.DivideSpoils(0.1)\nEndFunction\n"));
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
