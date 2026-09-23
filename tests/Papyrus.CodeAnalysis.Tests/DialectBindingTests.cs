using System;
using System.Collections.Generic;
using System.Linq;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class DialectBindingTests
{
    [Fact]
    public void Skyrim_is_operator_is_pap1004()
    {
        var diagnostic = BindSingle(
            PapyrusDialect.SkyrimSe,
            "SkyrimIs",
            "ScriptName SkyrimIs\nFunction F()\n  Bool x = None is Form\nEndFunction\n",
            PapyrusCompilation.InvalidIsOperatorDiagnosticId);

        Assert.Same(DiagnosticCatalog.InvalidIsOperator, diagnostic.Descriptor);
    }

    [Fact]
    public void Skyrim_script_name_colon_is_namespace_diagnostic()
    {
        var diagnostic = BindSingle(
            PapyrusDialect.SkyrimSe,
            "A:B:SkyrimNamespace",
            "ScriptName A:B:SkyrimNamespace\n",
            PapyrusCompilation.UnsupportedNamespaceDiagnosticId);

        Assert.Same(DiagnosticCatalog.UnsupportedNamespace, diagnostic.Descriptor);
        Assert.Equal("Namespaces are not supported in this dialect.", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("Self")]
    [InlineData("Parent")]
    [InlineData("self")]
    public void Declared_self_or_parent_member_is_reserved(string memberName)
    {
        var diagnostic = BindSingle(
            PapyrusDialect.SkyrimSe,
            "Reserved",
            "ScriptName Reserved\nInt Property " + memberName + " Auto\n",
            PapyrusCompilation.ReservedMemberNameDiagnosticId);

        Assert.Same(DiagnosticCatalog.ReservedMemberName, diagnostic.Descriptor);
        Assert.Contains(memberName, diagnostic.GetMessage(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_named_parent_is_not_a_reserved_member()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Parent",
            "ScriptName Parent\n");

        _ = compilation.GetScript(QualifiedName.Parse("Parent"));
        Assert.Empty(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static d => d.Id == PapyrusCompilation.ReservedMemberNameDiagnosticId));
    }

    [Theory]
    [InlineData(
        "ScriptName Foo\nStruct Point\nFloat X\nEndStruct\n",
        "PAP1006",
        "Fallout4")]
    [InlineData(
        "ScriptName Foo\nGroup Stats\nInt Property Health Auto\nEndGroup\n",
        "PAP1007",
        "Fallout4")]
    [InlineData(
        "ScriptName Foo\nCustomEvent OnSomething\n",
        "PAP1008",
        "Fallout4")]
    [InlineData(
        "ScriptName Foo\nGuard Mutex\n",
        "PAP1009",
        "Starfield")]
    public void Unsupported_feature_from_other_dialect_tree_is_diagnosed(
        string source,
        string expectedId,
        string parseDialectName)
    {
        var parseDialect = parseDialectName == "Starfield"
            ? PapyrusDialect.Starfield
            : PapyrusDialect.Fallout4;
        var tree = SyntaxTree.Parse(source, parseDialect);
        var compilation = Compilation(PapyrusDialect.SkyrimSe, "Foo", "ScriptName Foo\n")
            .WithSyntaxTree(tree);

        _ = compilation.GetScript(QualifiedName.Parse("Foo"));
        Assert.Contains(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            d => d.Id == expectedId);
    }

    [Fact]
    public void Skyrim_struct_type_hash_is_unsupported_struct()
    {
        BindSingle(
            PapyrusDialect.SkyrimSe,
            "SkyrimStruct",
            "ScriptName SkyrimStruct\nObjectReference#Point Value\n",
            PapyrusCompilation.UnsupportedStructDiagnosticId);
    }

    [Fact]
    public void Fallout4_does_not_diagnose_supported_struct_and_namespace()
    {
        var compilation = Compilation(
            PapyrusDialect.Fallout4,
            "A:B:Foo",
            "ScriptName A:B:Foo\nStruct Point\nFloat X\nEndStruct\n");

        _ = compilation.GetScript(QualifiedName.Parse("A:B:Foo"));
        Assert.Empty(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static d =>
                    d.Id == PapyrusCompilation.UnsupportedStructDiagnosticId
                    || d.Id == PapyrusCompilation.UnsupportedNamespaceDiagnosticId
                    || d.Id == PapyrusCompilation.UnsupportedGroupDiagnosticId
                    || d.Id == PapyrusCompilation.UnsupportedCustomEventDiagnosticId));
    }

    private static Diagnostic BindSingle(
        PapyrusDialect dialect,
        string scriptName,
        string source,
        string diagnosticId)
    {
        var compilation = Compilation(dialect, scriptName, source);
        _ = compilation.GetScript(QualifiedName.Parse(scriptName));
        return Assert.Single(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(d => d.Id == diagnosticId));
    }

    private static PapyrusCompilation Compilation(PapyrusDialect dialect, string name, string source)
    {
        return PapyrusCompilation.Create(
            dialect,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse(name)] = SourceText.From(source),
            }));
    }
}
