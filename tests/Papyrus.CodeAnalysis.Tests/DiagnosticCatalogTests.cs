using System;
using System.Collections.Generic;
using System.Linq;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class DiagnosticCatalogTests
{
    [Fact]
    public void Catalog_contains_registered_ids()
    {
        var ids = Ids();

        Assert.Contains(PapyrusCompilation.UnresolvedScriptDiagnosticId, ids);
        Assert.Contains(PapyrusCompilation.UnresolvedBaseTypeDiagnosticId, ids);
        Assert.Contains(PapyrusCompilation.CyclicBaseTypeDiagnosticId, ids);
        Assert.Contains(PapyrusCompilation.UnresolvedTypeDiagnosticId, ids);
        Assert.Contains(DiagnosticCatalog.InternalErrorId, ids);

        Assert.Equal("PAP1000", DiagnosticCatalog.UnresolvedScript.Id);
        Assert.Equal("PAP1001", DiagnosticCatalog.UnresolvedBaseType.Id);
        Assert.Equal("PAP1002", DiagnosticCatalog.CyclicBaseType.Id);
        Assert.Equal("PAP1003", DiagnosticCatalog.UnresolvedType.Id);
        Assert.Equal("PAP9999", DiagnosticCatalog.InternalError.Id);

        Assert.Equal(PapyrusCompilation.UnresolvedScriptDiagnosticId, DiagnosticCatalog.UnresolvedScript.Id);
        Assert.Equal(PapyrusCompilation.UnresolvedBaseTypeDiagnosticId, DiagnosticCatalog.UnresolvedBaseType.Id);
        Assert.Equal(PapyrusCompilation.CyclicBaseTypeDiagnosticId, DiagnosticCatalog.CyclicBaseType.Id);
        Assert.Equal(PapyrusCompilation.UnresolvedTypeDiagnosticId, DiagnosticCatalog.UnresolvedType.Id);
    }

    [Fact]
    public void Catalog_ids_are_unique()
    {
        var ids = Ids();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TryGet_finds_registered_descriptors()
    {
        Assert.True(DiagnosticCatalog.TryGet("PAP1000", out var unresolved));
        Assert.Same(DiagnosticCatalog.UnresolvedScript, unresolved);
        Assert.False(DiagnosticCatalog.TryGet("PAP0001", out _));
        Assert.False(DiagnosticCatalog.TryGet(null!, out _));
    }

    [Fact]
    public void GetMessage_formats_catalog_arguments()
    {
        var info = new DiagnosticInfo(DiagnosticCatalog.UnresolvedScript, "Foo");
        var diagnostic = new Diagnostic(info, default);

        Assert.Same(DiagnosticCatalog.UnresolvedScript, diagnostic.Descriptor);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("PAP1000", diagnostic.Id);
        Assert.Equal("Script 'Foo' could not be resolved.", diagnostic.GetMessage());
        Assert.Equal(diagnostic.GetMessage(), diagnostic.Message);
        Assert.Equal(diagnostic.GetMessage(), info.Message);
    }

    [Fact]
    public void Id_and_message_constructor_remains_compatible()
    {
        var info = new DiagnosticInfo("PAP2000", "User flag 'X' bit index 32 is outside the 32-bit mask (0-31).");
        var diagnostic = new Diagnostic(info, new TextSpan(0, 1));

        Assert.Equal("PAP2000", diagnostic.Id);
        Assert.Equal("User flag 'X' bit index 32 is outside the 32-bit mask (0-31).", diagnostic.Message);
        Assert.Equal(diagnostic.Message, diagnostic.GetMessage());
        Assert.NotNull(diagnostic.Descriptor);
        Assert.Equal("PAP2000", diagnostic.Descriptor.Id);
    }

    [Fact]
    public void Binding_diagnostics_use_catalog_descriptors()
    {
        var name = QualifiedName.Parse("Child");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [name] = SourceText.From("ScriptName Child Extends Missing\n"),
            }));

        _ = compilation.GetScript(name);
        var diagnostic = Assert.Single(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static d => d.Id == PapyrusCompilation.UnresolvedBaseTypeDiagnosticId));

        Assert.Same(DiagnosticCatalog.UnresolvedBaseType, diagnostic.Descriptor);
        Assert.Equal("Base type 'Missing' could not be resolved.", diagnostic.GetMessage());
    }

    private static List<string> Ids()
    {
        return DiagnosticCatalog.All.Select(static descriptor => descriptor.Id).ToList();
    }
}
