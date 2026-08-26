using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Emit;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class EngineLimitTests
{
    [Fact]
    public void Array_of_129_elements_is_pap2002_error()
    {
        const string source = "ScriptName Foo\nFunction Bar()\n  Int[] a = new Int[129]\nEndFunction\n";
        var (obj, diagnostics) = Emit(source);

        var diagnostic = Assert.Single(diagnostics, static d => d.Id.StartsWith("PAP20", StringComparison.Ordinal));
        Assert.Equal(DiagnosticCatalog.ArraySizeExceedsLimitId, diagnostic.Id);
        Assert.Same(DiagnosticCatalog.ArraySizeExceedsLimit, diagnostic.Descriptor);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(DiagnosticCatalog.EngineLimitCategory, diagnostic.Descriptor.Category);
        AssertSpan(diagnostic, source, "new Int[129]");
        Assert.NotNull(obj);
        Assert.Equal("Foo", obj.Name);
    }

    [Fact]
    public void Array_size_from_constant_binary_is_pap2002()
    {
        const string source = "ScriptName Foo\nFunction Bar()\n  Int[] a = new Int[128 + 1]\nEndFunction\n";
        var (_, diagnostics) = Emit(source);

        var diagnostic = Assert.Single(diagnostics, static d => d.Id == DiagnosticCatalog.ArraySizeExceedsLimitId);
        AssertSpan(diagnostic, source, "new Int[128 + 1]");
    }

    [Fact]
    public void Array_of_128_elements_is_within_limit()
    {
        var (_, diagnostics) = Emit(
            "ScriptName Foo\nFunction Bar()\n  Int[] a = new Int[128]\nEndFunction\n");

        Assert.DoesNotContain(diagnostics, static d => d.Id.StartsWith("PAP20", StringComparison.Ordinal));
    }

    [Fact]
    public void IgnoreEngineLimits_downgrades_to_warning_and_still_emits()
    {
        var (obj, diagnostics) = Emit(
            "ScriptName Foo\nFunction Bar()\n  Int[] a = new Int[129]\nEndFunction\n",
            ignoreEngineLimits: true);

        var diagnostic = Assert.Single(diagnostics, static d => d.Id == DiagnosticCatalog.ArraySizeExceedsLimitId);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Descriptor.DefaultSeverity);
        Assert.NotNull(obj);
        Assert.Contains(
            Function(obj, "Bar").Instructions,
            static i => i.OpCode == PexOpCode.ArrayCreate);
    }

    [Fact]
    public void Too_many_properties_is_pap2005()
    {
        var diagnostic = AssertLimit(
            "ScriptName Foo\nInt Property A Auto\nInt Property B Auto\n",
            limits => limits with { MaxProperties = 1 },
            DiagnosticCatalog.TooManyPropertiesId);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void Too_many_empty_state_functions_is_pap2003()
    {
        AssertLimit(
            "ScriptName Foo\nFunction A()\nEndFunction\nFunction B()\nEndFunction\n",
            limits => limits with { MaxFunctionsInEmptyState = 1 },
            DiagnosticCatalog.TooManyFunctionsInEmptyStateId);
    }

    [Fact]
    public void Too_many_named_state_functions_is_pap2004()
    {
        const string source = "ScriptName Foo\nState S\nFunction A()\nEndFunction\nFunction B()\nEndFunction\nEndState\n";
        var diagnostic = AssertLimit(
            source,
            limits => limits with { MaxFunctionsPerNamedState = 1 },
            DiagnosticCatalog.TooManyFunctionsInNamedStateId);
        AssertSpanAfter(diagnostic, source, "State ", "S");
    }

    [Fact]
    public void Too_many_variables_is_pap2006()
    {
        AssertLimit(
            "ScriptName Foo\nInt A\nInt B\n",
            limits => limits with { MaxVariables = 1 },
            DiagnosticCatalog.TooManyVariablesId);
    }

    [Fact]
    public void Too_many_initial_values_is_pap2007()
    {
        AssertLimit(
            "ScriptName Foo\nInt A = 1\nInt B = 2\n",
            limits => limits with { MaxInitialValues = 1 },
            DiagnosticCatalog.TooManyInitialValuesId);
    }

    [Fact]
    public void Too_many_named_states_is_pap2008()
    {
        AssertLimit(
            "ScriptName Foo\nState A\nFunction F()\nEndFunction\nEndState\nState B\nFunction G()\nEndFunction\nEndState\n",
            limits => limits with { MaxNamedStates = 1 },
            DiagnosticCatalog.TooManyNamedStatesId);
    }

    [Fact]
    public void Too_many_parameters_is_pap2009()
    {
        AssertLimit(
            "ScriptName Foo\nFunction Bar(Int a, Int b)\nEndFunction\n",
            limits => limits with { MaxParameters = 1 },
            DiagnosticCatalog.TooManyParametersId);
    }

    [Fact]
    public void Too_many_user_flags_is_pap2010()
    {
        var flags = FlagsFile.Parse("Hidden\nConditional\n");
        var diagnostic = AssertLimit(
            "ScriptName Foo\nFunction Bar()\nEndFunction\n",
            limits => limits with { MaxUserFlags = 1 },
            DiagnosticCatalog.TooManyUserFlagsId,
            flags: flags);
        Assert.Contains("2", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("1", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void User_flag_bit_out_of_range_is_reported_on_emit_and_ignore_downgrades()
    {
        var flags = FlagsFile.Parse("Hidden {32}\n");
        var (_, errors) = Emit(
            "ScriptName Foo\nFunction Bar()\nEndFunction\n",
            flags: flags);
        var error = Assert.Single(errors, static d => d.Id == DiagnosticCatalog.UserFlagBitIndexOutOfRangeId);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.DoesNotContain(errors, static d => d.Id == DiagnosticCatalog.TooManyUserFlagsId);

        var (_, warnings) = Emit(
            "ScriptName Foo\nFunction Bar()\nEndFunction\n",
            flags: flags,
            ignoreEngineLimits: true);
        var warning = Assert.Single(warnings, static d => d.Id == DiagnosticCatalog.UserFlagBitIndexOutOfRangeId);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void Out_of_range_flag_bit_does_not_count_toward_pap2010()
    {
        var text = new StringBuilder();
        for (var i = 0; i < 31; i++)
        {
            text.Append('F').Append(i).Append(" {").Append(i).Append('}').AppendLine();
        }

        text.Append("Bad {32}\n");
        var flags = FlagsFile.Parse(text.ToString());
        var (_, diagnostics) = Emit(
            "ScriptName Foo\nFunction Bar()\nEndFunction\n",
            flags: flags);

        Assert.Contains(diagnostics, static d => d.Id == DiagnosticCatalog.UserFlagBitIndexOutOfRangeId);
        Assert.DoesNotContain(diagnostics, static d => d.Id == DiagnosticCatalog.TooManyUserFlagsId);
    }

    [Fact]
    public void Too_many_guards_is_pap2011()
    {
        const string source = "ScriptName Foo\nGuard A\nGuard B\nFunction Bar()\nEndFunction\n";
        var diagnostic = AssertLimit(
            source,
            limits => limits with { MaxGuards = 1 },
            DiagnosticCatalog.TooManyGuardsId,
            dialect: PapyrusDialect.Starfield);
        AssertSpanAfter(diagnostic, source, "Guard ", "B");
    }

    [Fact]
    public void IgnoreEngineLimits_still_emits_object_for_injected_limit()
    {
        var (obj, diagnostics) = Emit(
            "ScriptName Foo\nInt Property A Auto\nInt Property B Auto\n",
            limits: PapyrusDialect.SkyrimSe.Limits with { MaxProperties = 1 },
            ignoreEngineLimits: true);

        var diagnostic = Assert.Single(diagnostics, static d => d.Id == DiagnosticCatalog.TooManyPropertiesId);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(2, obj.Properties.Length);
    }

    private static Diagnostic AssertLimit(
        string source,
        Func<EngineLimits, EngineLimits> limits,
        string diagnosticId,
        PapyrusDialect? dialect = null,
        FlagsFile? flags = null)
    {
        dialect ??= PapyrusDialect.SkyrimSe;
        var (obj, diagnostics) = Emit(
            source,
            limits: limits(dialect.Limits),
            dialect: dialect,
            flags: flags);
        var diagnostic = Assert.Single(diagnostics, d => d.Id == diagnosticId);
        Assert.True(DiagnosticCatalog.TryGet(diagnosticId, out var descriptor));
        Assert.Same(descriptor, diagnostic.Descriptor);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.NotNull(obj);
        return diagnostic;
    }

    private static PexFunction Function(PexObject obj, string name)
    {
        foreach (var state in obj.States)
        {
            if (state.Name is not (null or ""))
            {
                continue;
            }

            foreach (var function in state.Functions)
            {
                if (string.Equals(function.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return function;
                }
            }
        }

        throw new InvalidOperationException("Function '" + name + "' was not emitted.");
    }

    private static void AssertSpan(Diagnostic diagnostic, string source, string fragment)
    {
        var start = source.IndexOf(fragment, StringComparison.Ordinal);
        Assert.True(start >= 0, "Fragment '" + fragment + "' was not found.");
        Assert.Equal(start, diagnostic.Span.Start);
        Assert.Equal(fragment.Length, diagnostic.Span.Length);
    }

    private static void AssertSpanAfter(Diagnostic diagnostic, string source, string prefix, string fragment)
    {
        var start = source.IndexOf(prefix + fragment, StringComparison.Ordinal);
        Assert.True(start >= 0, "Fragment '" + prefix + fragment + "' was not found.");
        Assert.Equal(start + prefix.Length, diagnostic.Span.Start);
        Assert.Equal(fragment.Length, diagnostic.Span.Length);
    }

    private static (PexObject Obj, ImmutableArray<Diagnostic> Diagnostics) Emit(
        string source,
        EngineLimits? limits = null,
        bool ignoreEngineLimits = false,
        PapyrusDialect? dialect = null,
        FlagsFile? flags = null,
        string scriptName = "Foo")
    {
        dialect ??= PapyrusDialect.SkyrimSe;
        if (limits is not null)
        {
            dialect = dialect with { Limits = limits };
        }

        var options = ignoreEngineLimits
            ? new PapyrusParseOptions { IgnoreEngineLimits = true }
            : PapyrusParseOptions.Default;
        var sources = new Dictionary<QualifiedName, SourceText>
        {
            [QualifiedName.Parse(scriptName)] = SourceText.From(source),
        };
        var compilation = PapyrusCompilation.Create(dialect, new TestScriptResolver(sources), options);
        var script = compilation.GetScript(QualifiedName.Parse(scriptName));
        var emitter = new PexEmitter(flags);
        var obj = emitter.Emit(script, TestContext.Current.CancellationToken);
        return (obj, emitter.Diagnostics);
    }
}
