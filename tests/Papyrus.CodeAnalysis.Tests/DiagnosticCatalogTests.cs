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
        Assert.Contains(PapyrusCompilation.InvalidIsOperatorDiagnosticId, ids);
        Assert.Contains(PapyrusCompilation.CannotConvertDiagnosticId, ids);
        Assert.Contains(PapyrusCompilation.UnsupportedStructDiagnosticId, ids);
        Assert.Contains(PapyrusCompilation.UnsupportedGroupDiagnosticId, ids);
        Assert.Contains(PapyrusCompilation.UnsupportedCustomEventDiagnosticId, ids);
        Assert.Contains(PapyrusCompilation.UnsupportedGuardDiagnosticId, ids);
        Assert.Contains(PapyrusCompilation.UnsupportedNamespaceDiagnosticId, ids);
        Assert.Contains(PapyrusCompilation.ReservedMemberNameDiagnosticId, ids);
        Assert.Contains(DiagnosticCatalog.InternalErrorId, ids);
        Assert.Contains(DiagnosticCatalog.UserFlagBitIndexOutOfRangeId, ids);
        Assert.Contains(DiagnosticCatalog.TooManyNamedUserFlagsId, ids);
        Assert.Contains(DiagnosticCatalog.InvalidFlagsFileId, ids);
        Assert.Contains(DiagnosticCatalog.ExpectedTokenId, ids);
        Assert.Contains(DiagnosticCatalog.UnexpectedTokenId, ids);
        Assert.Contains(DiagnosticCatalog.UnresolvedFunctionId, ids);
        Assert.Contains(DiagnosticCatalog.UndefinedNameId, ids);
        Assert.Contains(DiagnosticCatalog.MemberNotFoundId, ids);
        Assert.Contains(DiagnosticCatalog.ArraySizeExceedsLimitId, ids);
        Assert.Contains(DiagnosticCatalog.TooManyFunctionsInEmptyStateId, ids);
        Assert.Contains(DiagnosticCatalog.TooManyFunctionsInNamedStateId, ids);
        Assert.Contains(DiagnosticCatalog.TooManyPropertiesId, ids);
        Assert.Contains(DiagnosticCatalog.TooManyVariablesId, ids);
        Assert.Contains(DiagnosticCatalog.TooManyInitialValuesId, ids);
        Assert.Contains(DiagnosticCatalog.TooManyNamedStatesId, ids);
        Assert.Contains(DiagnosticCatalog.TooManyParametersId, ids);
        Assert.Contains(DiagnosticCatalog.TooManyUserFlagsId, ids);
        Assert.Contains(DiagnosticCatalog.TooManyGuardsId, ids);

        Assert.Equal("PAP1000", DiagnosticCatalog.UnresolvedScript.Id);
        Assert.Equal("PAP1001", DiagnosticCatalog.UnresolvedBaseType.Id);
        Assert.Equal("PAP1002", DiagnosticCatalog.CyclicBaseType.Id);
        Assert.Equal("PAP1003", DiagnosticCatalog.UnresolvedType.Id);
        Assert.Equal("PAP1004", DiagnosticCatalog.InvalidIsOperator.Id);
        Assert.Equal("PAP1005", DiagnosticCatalog.CannotConvert.Id);
        Assert.Equal("PAP1006", DiagnosticCatalog.UnsupportedStruct.Id);
        Assert.Equal("PAP1007", DiagnosticCatalog.UnsupportedGroup.Id);
        Assert.Equal("PAP1008", DiagnosticCatalog.UnsupportedCustomEvent.Id);
        Assert.Equal("PAP1009", DiagnosticCatalog.UnsupportedGuard.Id);
        Assert.Equal("PAP1010", DiagnosticCatalog.UnsupportedNamespace.Id);
        Assert.Equal("PAP1011", DiagnosticCatalog.ReservedMemberName.Id);
        Assert.Equal("PAP2000", DiagnosticCatalog.UserFlagBitIndexOutOfRange.Id);
        Assert.Equal("PAP2001", DiagnosticCatalog.TooManyNamedUserFlags.Id);
        Assert.Equal("PAP2002", DiagnosticCatalog.ArraySizeExceedsLimit.Id);
        Assert.Equal("PAP2003", DiagnosticCatalog.TooManyFunctionsInEmptyState.Id);
        Assert.Equal("PAP2004", DiagnosticCatalog.TooManyFunctionsInNamedState.Id);
        Assert.Equal("PAP2005", DiagnosticCatalog.TooManyProperties.Id);
        Assert.Equal("PAP2006", DiagnosticCatalog.TooManyVariables.Id);
        Assert.Equal("PAP2007", DiagnosticCatalog.TooManyInitialValues.Id);
        Assert.Equal("PAP2008", DiagnosticCatalog.TooManyNamedStates.Id);
        Assert.Equal("PAP2009", DiagnosticCatalog.TooManyParameters.Id);
        Assert.Equal("PAP2010", DiagnosticCatalog.TooManyUserFlags.Id);
        Assert.Equal("PAP2011", DiagnosticCatalog.TooManyGuards.Id);
        Assert.Equal("PAP9999", DiagnosticCatalog.InternalError.Id);

        Assert.Equal(PapyrusCompilation.UnresolvedScriptDiagnosticId, DiagnosticCatalog.UnresolvedScript.Id);
        Assert.Equal(PapyrusCompilation.UnresolvedBaseTypeDiagnosticId, DiagnosticCatalog.UnresolvedBaseType.Id);
        Assert.Equal(PapyrusCompilation.CyclicBaseTypeDiagnosticId, DiagnosticCatalog.CyclicBaseType.Id);
        Assert.Equal(PapyrusCompilation.UnresolvedTypeDiagnosticId, DiagnosticCatalog.UnresolvedType.Id);
        Assert.Equal(PapyrusCompilation.InvalidIsOperatorDiagnosticId, DiagnosticCatalog.InvalidIsOperator.Id);
        Assert.Equal(PapyrusCompilation.CannotConvertDiagnosticId, DiagnosticCatalog.CannotConvert.Id);
        Assert.Equal(PapyrusCompilation.UnsupportedStructDiagnosticId, DiagnosticCatalog.UnsupportedStruct.Id);
        Assert.Equal(PapyrusCompilation.UnsupportedGroupDiagnosticId, DiagnosticCatalog.UnsupportedGroup.Id);
        Assert.Equal(PapyrusCompilation.UnsupportedCustomEventDiagnosticId, DiagnosticCatalog.UnsupportedCustomEvent.Id);
        Assert.Equal(PapyrusCompilation.UnsupportedGuardDiagnosticId, DiagnosticCatalog.UnsupportedGuard.Id);
        Assert.Equal(PapyrusCompilation.UnsupportedNamespaceDiagnosticId, DiagnosticCatalog.UnsupportedNamespace.Id);
        Assert.Equal(PapyrusCompilation.ReservedMemberNameDiagnosticId, DiagnosticCatalog.ReservedMemberName.Id);
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
        Assert.True(DiagnosticCatalog.TryGet("PAP2002", out var arraySize));
        Assert.Same(DiagnosticCatalog.ArraySizeExceedsLimit, arraySize);
        Assert.False(DiagnosticCatalog.TryGet("PAP0999", out _));
        Assert.False(DiagnosticCatalog.TryGet(null!, out _));
    }

    [Fact]
    public void Engine_limit_descriptors_use_engine_limit_category()
    {
        foreach (var descriptor in DiagnosticCatalog.All)
        {
            if (!descriptor.Id.StartsWith("PAP20", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.Equal(DiagnosticCatalog.EngineLimitCategory, descriptor.Category);
            Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        }
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
