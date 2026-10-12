using System;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class DialectTests
{
    [Fact]
    public void Skyrim_keywords_do_not_contain_struct()
    {
        Assert.False(PapyrusDialect.SkyrimSe.Keywords.ContainsKey("struct"));
        Assert.False(PapyrusDialect.SkyrimLe.Keywords.ContainsKey("struct"));
        Assert.False(PapyrusDialect.SkyrimSe.Keywords.ContainsKey("var"));
        Assert.False(PapyrusDialect.SkyrimSe.Keywords.ContainsKey("group"));
        Assert.False(PapyrusDialect.SkyrimSe.Keywords.ContainsKey("const"));
        Assert.False(PapyrusDialect.SkyrimSe.Keywords.ContainsKey("guard"));
    }

    [Fact]
    public void Fallout4_keywords_contain_struct()
    {
        Assert.True(PapyrusDialect.Fallout4.Keywords.ContainsKey("struct"));
        Assert.Equal(SyntaxKind.StructKeyword, PapyrusDialect.Fallout4.Keywords["struct"]);
        Assert.Equal(SyntaxKind.StructKeyword, PapyrusDialect.Fallout4.Keywords["STRUCT"]);
        Assert.Equal(SyntaxKind.VarKeyword, PapyrusDialect.Fallout4.Keywords["var"]);
        Assert.Equal(SyntaxKind.GroupKeyword, PapyrusDialect.Fallout4.Keywords["group"]);
        Assert.Equal(SyntaxKind.ConstKeyword, PapyrusDialect.Fallout4.Keywords["const"]);
        Assert.False(PapyrusDialect.Fallout4.Keywords.ContainsKey("guard"));
    }

    [Fact]
    public void Starfield_keywords_contain_guard_but_not_lock()
    {
        Assert.Equal(SyntaxKind.GuardKeyword, PapyrusDialect.Starfield.Keywords["guard"]);
        Assert.Equal(SyntaxKind.EndGuardKeyword, PapyrusDialect.Starfield.Keywords["endguard"]);
        Assert.Equal(SyntaxKind.TryGuardKeyword, PapyrusDialect.Starfield.Keywords["tryguard"]);
        Assert.False(PapyrusDialect.Starfield.Keywords.ContainsKey("lock"));
        Assert.False(PapyrusDialect.Starfield.Keywords.ContainsKey("endlock"));
        Assert.False(PapyrusDialect.Starfield.Keywords.ContainsKey("trylock"));
        Assert.False(PapyrusDialect.Starfield.Keywords.ContainsKey("unlock"));
    }

    [Fact]
    public void User_flags_are_not_lexer_keywords()
    {
        Assert.False(PapyrusDialect.SkyrimSe.Keywords.ContainsKey("hidden"));
        Assert.False(PapyrusDialect.SkyrimSe.Keywords.ContainsKey("conditional"));
        Assert.False(PapyrusDialect.Fallout4.Keywords.ContainsKey("hidden"));
        Assert.False(PapyrusDialect.Starfield.Keywords.ContainsKey("hidden"));
        Assert.False(PapyrusDialect.Starfield.Keywords.ContainsKey("conditional"));
    }

    [Fact]
    public void Keywords_use_ordinal_ignore_case()
    {
        Assert.Equal(StringComparer.OrdinalIgnoreCase, PapyrusDialect.SkyrimSe.Keywords.Comparer);
        Assert.Equal(SyntaxKind.ScriptNameKeyword, PapyrusDialect.SkyrimSe.Keywords["scriptname"]);
        Assert.Equal(SyntaxKind.ScriptNameKeyword, PapyrusDialect.SkyrimSe.Keywords["ScriptName"]);
    }

    [Fact]
    public void SkyrimLe_exists()
    {
        var dialect = PapyrusDialect.SkyrimLe;
        Assert.NotNull(dialect);
        Assert.Equal(GameId.Skyrim, dialect.Game);
        Assert.Equal(LanguageVersion.Official, dialect.Version);
        Assert.Equal(PexVersion.V31, dialect.PexVersion);
        Assert.Equal(PexOpCode.SkyrimMax, dialect.MaxOpCode);
        Assert.False(dialect.SupportsStructs);
    }

    [Fact]
    public void MaxGuards_is_511_only_for_Starfield()
    {
        Assert.Equal(511, PapyrusDialect.Starfield.Limits.MaxGuards);
        Assert.Equal(0, PapyrusDialect.SkyrimSe.Limits.MaxGuards);
        Assert.Equal(0, PapyrusDialect.SkyrimLe.Limits.MaxGuards);
        Assert.Equal(0, PapyrusDialect.Fallout4.Limits.MaxGuards);
        Assert.Equal(0, PapyrusDialect.Fallout76.Limits.MaxGuards);
    }

    [Theory]
    [InlineData(nameof(PapyrusDialect.SkyrimLe), false)]
    [InlineData(nameof(PapyrusDialect.SkyrimSe), false)]
    [InlineData(nameof(PapyrusDialect.Fallout4), true)]
    [InlineData(nameof(PapyrusDialect.Starfield), true)]
    [InlineData(nameof(PapyrusDialect.Fallout76), true)]
    public void SupportsIsOperator_is_false_for_Skyrim(string name, bool expected)
    {
        Assert.Equal(expected, Dialect(name).SupportsIsOperator);
    }

    [Fact]
    public void Shared_engine_limits_match_the_design_table()
    {
        foreach (var dialect in new[]
                 {
                     PapyrusDialect.SkyrimLe,
                     PapyrusDialect.SkyrimSe,
                     PapyrusDialect.Fallout4,
                     PapyrusDialect.Starfield,
                     PapyrusDialect.Fallout76,
                 })
        {
            Assert.Equal(128, dialect.Limits.MaxArrayElements);
            Assert.Equal(2047, dialect.Limits.MaxFunctionsInEmptyState);
            Assert.Equal(511, dialect.Limits.MaxFunctionsPerNamedState);
            Assert.Equal(1023, dialect.Limits.MaxProperties);
            Assert.Equal(1023, dialect.Limits.MaxVariables);
            Assert.Equal(1023, dialect.Limits.MaxInitialValues);
            Assert.Equal(127, dialect.Limits.MaxNamedStates);
            Assert.Equal(511, dialect.Limits.MaxParameters);
            Assert.Equal(31, dialect.Limits.MaxUserFlags);
        }
    }

    [Fact]
    public void Skyrim_allow_flags_are_true_fo4_plus_are_false()
    {
        Assert.True(PapyrusDialect.SkyrimSe.AllowUnknownEventsOnNonNativeClass);
        Assert.True(PapyrusDialect.SkyrimLe.AllowVariableShadowingParentProperty);
        Assert.False(PapyrusDialect.Fallout4.AllowLocalUseBeforeDeclaration);
        Assert.False(PapyrusDialect.Starfield.AllowAssigningVoidMethodCallResult);
    }

    private static PapyrusDialect Dialect(string name)
    {
        return name switch
        {
            nameof(PapyrusDialect.SkyrimLe) => PapyrusDialect.SkyrimLe,
            nameof(PapyrusDialect.SkyrimSe) => PapyrusDialect.SkyrimSe,
            nameof(PapyrusDialect.Fallout4) => PapyrusDialect.Fallout4,
            nameof(PapyrusDialect.Starfield) => PapyrusDialect.Starfield,
            nameof(PapyrusDialect.Fallout76) => PapyrusDialect.Fallout76,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, message: null),
        };
    }
}
