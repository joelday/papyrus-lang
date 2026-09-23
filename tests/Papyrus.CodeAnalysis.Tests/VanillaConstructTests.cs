using System;
using System.Collections.Generic;
using System.Linq;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Emit;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

/// <summary>
/// Constructs from the vanilla Fallout 4 scripts that the compiler previously rejected or,
/// worse, silently miscompiled. Each mirrors a real script (named in the test).
/// </summary>
public sealed class VanillaConstructTests
{
    [Fact]
    public void Import_after_other_declarations_brings_structs_and_globals_into_scope()
    {
        // WorkshopParentScript: `import WorkshopDataScript` appears after properties.
        var (compilation, diagnostics) = Compile(
            ("WorkshopDataScript",
                "ScriptName WorkshopDataScript\nStruct WorkshopVendorType\n  Int Id\nEndStruct\n"
                + "Int Function Double(Int a) global\n  return a * 2\nEndFunction\n"),
            ("WorkshopParentScript",
                "ScriptName WorkshopParentScript\nInt Property Count Auto\nimport WorkshopDataScript\n"
                + "WorkshopVendorType[] vendors\nInt Function Twice()\n  return Double(Count)\nEndFunction\n"));

        Assert.Empty(diagnostics);
        var call = Function(Emit(compilation, "WorkshopParentScript"), "Twice").Instructions
            .Single(static i => i.OpCode == PexOpCode.CallStatic);
        Assert.Equal("WorkshopDataScript", call.Arguments[0].Text);
        Assert.Equal("Double", call.Arguments[1].Text);
    }

    [Fact]
    public void Import_after_script_doc_comment_is_parsed_with_full_fidelity()
    {
        // DefaultRemoteControlObjectReference: a `{doc}` line sits between the header and the import.
        const string source =
            "ScriptName Foo\n{Doc for the script}\n\nimport DefaultRemoteControl\n;comment\nSignalChoices choices\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        var (_, diagnostics) = Compile(
            ("DefaultRemoteControl", "ScriptName DefaultRemoteControl\nStruct SignalChoices\n  Int X\nEndStruct\n"),
            ("Foo", source));
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Unqualified_type_resolves_in_the_current_namespace_first()
    {
        // Hardcore:HC_DrinkWaterEffectScript refers to HC_ManagerScript, meaning Hardcore:HC_ManagerScript.
        var (compilation, diagnostics) = Compile(
            ("Hardcore:HC_ManagerScript", "ScriptName Hardcore:HC_ManagerScript\nFunction Ping()\nEndFunction\n"),
            ("Hardcore:HC_DrinkWaterEffectScript",
                "ScriptName Hardcore:HC_DrinkWaterEffectScript\nHC_ManagerScript Property Manager Auto\n"
                + "Function F()\n  Manager.Ping()\nEndFunction\n"));

        Assert.Empty(diagnostics);
        var property = Assert.Single(Assert.Single(Emit(compilation, "Hardcore:HC_DrinkWaterEffectScript").Objects).Properties);
        Assert.Equal("Hardcore:HC_ManagerScript", property.TypeName, ignoreCase: true);
    }

    [Fact]
    public void Remote_event_handler_binds_parameters_and_is_named_like_caprica()
    {
        // BobbleheadStandContainerScript: `Event ObjectReference.OnActivate(ObjectReference akSender, ...)`.
        const string handler =
            "ScriptName Stand\nEvent ObjectReference.OnActivate(ObjectReference akSender, ObjectReference akActionRef)\n"
            + "  akSender.Disable()\nEndEvent\n";
        Assert.Equal(handler, SyntaxTree.Parse(handler, PapyrusDialect.Fallout4).GetRoot().ToFullString());

        var (compilation, diagnostics) = Compile(
            ("ObjectReference", "ScriptName ObjectReference\nFunction Disable() native\n"),
            ("Stand", handler));

        Assert.Empty(diagnostics);
        var function = Function(Emit(compilation, "Stand"), "::remote_ObjectReference_OnActivate");
        Assert.Contains(function.Instructions, static i => i.OpCode == PexOpCode.CallMethod && i.Arguments[0].Text == "Disable");
    }

    [Fact]
    public void Call_resolves_to_the_function_when_a_state_or_local_shares_its_name()
    {
        // BirdCritterScript has `state flee` and `function flee()`; defaultQuestRespawnScript
        // has a local `respawnCollection` and a function `RespawnCollection`.
        var (compilation, diagnostics) = Compile(
            ("Bird",
                "ScriptName Bird\nState flee\nEndState\nFunction flee()\nEndFunction\n"
                + "Function RespawnCollection(Int a)\nEndFunction\n"
                + "Function Go()\n  Int respawnCollection = 1\n  flee()\n  RespawnCollection(respawnCollection)\nEndFunction\n"));

        Assert.Empty(diagnostics);
        var names = Function(Emit(compilation, "Bird"), "Go").Instructions
            .Where(static i => i.OpCode == PexOpCode.CallMethod)
            .Select(static i => i.Arguments[0].Text)
            .ToList();
        Assert.Equal(["flee", "RespawnCollection"], names);
    }

    [Fact]
    public void Same_named_locals_in_sibling_blocks_keep_their_own_types_and_slots()
    {
        // DN102_LabDemo2Script declares `current` as ObjectReference in one loop and Actor in the next.
        var (compilation, diagnostics) = Compile(
            ("ObjectReference", "ScriptName ObjectReference\nFunction Disable() native\n"),
            ("Actor", "ScriptName Actor Extends ObjectReference\nFunction Kill() native\n"),
            ("Demo",
                "ScriptName Demo\nFunction Go(ObjectReference a, Actor b)\n"
                + "  While a\n    ObjectReference current = a\n    current.Disable()\n  EndWhile\n"
                + "  While b\n    Actor current = b\n    current.Kill()\n  EndWhile\nEndFunction\n"));

        Assert.Empty(diagnostics);
        var function = Function(Emit(compilation, "Demo"), "Go");
        Assert.Contains(function.Locals, static l => l.Name == "current" && l.TypeName == "ObjectReference");
        Assert.Contains(function.Locals, static l => l.Name == "::mangled_current_0" && l.TypeName == "Actor");
        var kill = function.Instructions.Single(static i => i.OpCode == PexOpCode.CallMethod && i.Arguments[0].Text == "Kill");
        Assert.Equal("::mangled_current_0", kill.Arguments[1].Text);
    }

    [Fact]
    public void State_field_is_available_in_GetState_and_GotoState()
    {
        // ScriptObject.psc reads and writes `__state`, the VM's `::State` variable.
        var (compilation, diagnostics) = Compile(
            ("ScriptObject",
                "ScriptName ScriptObject Native Hidden\nString Function GetState()\n  Return __state\nEndFunction\n"
                + "Function GotoState(String asNewState)\n  __state = asNewState\nEndFunction\n"));

        Assert.Empty(diagnostics);
        var file = Emit(compilation, "ScriptObject");
        Assert.Contains(Function(file, "GetState").Instructions, static i => i.Arguments.Any(static a => a.Text == "::State"));
        Assert.Contains(Function(file, "GotoState").Instructions, static i => i.Arguments.Any(static a => a.Text == "::State"));
    }

    [Fact]
    public void Unresolvable_call_is_an_error_instead_of_an_empty_call()
    {
        var (compilation, _) = Compile(("Foo", "ScriptName Foo\nFunction F()\n  Missing()\nEndFunction\n"));
        var emitter = new PexEmitter();
        emitter.EmitFile(compilation.GetScript(QualifiedName.Parse("Foo")), TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(emitter.Diagnostics);
        Assert.Equal(DiagnosticCatalog.UnresolvedFunctionId, diagnostic.Id);
        Assert.Contains("Missing", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void User_flags_apply_their_mask_only_where_the_flags_file_allows()
    {
        var flags = FlagsFile.Parse(
            "Flag Hidden 0 { Script Property StructVar }\nFlag Conditional 1 { Script Variable }\n"
            + "Flag Mandatory 5 { Property }\nFlag Both Hidden & Conditional\n");
        var (compilation, _) = Compile(
            ("Foo", "ScriptName Foo Hidden Conditional\nInt Property P Auto Mandatory Hidden\nInt v Conditional Hidden\n"));

        var file = new PexEmitter(flags).EmitFile(compilation.GetScript(QualifiedName.Parse("Foo")), TestContext.Current.CancellationToken);
        var obj = Assert.Single(file.Objects);

        Assert.Equal(0b11u, obj.UserFlags);
        Assert.Equal((1u << 5) | 1u, Assert.Single(obj.Properties).UserFlags);
        // Hidden is not allowed on variables, so only Conditional survives.
        Assert.Equal(0b10u, obj.Variables.Single(static v => v.Name == "v").UserFlags);
        // The composite has no bit of its own and is not in the PEX flag table.
        Assert.DoesNotContain(file.UserFlags, static f => f.Name == "Both");
    }

    private static (PapyrusCompilation Compilation, List<Diagnostic> Diagnostics) Compile(
        params (string Name, string Source)[] scripts)
    {
        var sources = new Dictionary<QualifiedName, SourceText>();
        foreach (var (name, source) in scripts)
        {
            sources[QualifiedName.Parse(name)] = SourceText.From(source);
        }

        var compilation = PapyrusCompilation.Create(PapyrusDialect.Fallout4, new TestScriptResolver(sources));
        var diagnostics = new List<Diagnostic>();
        foreach (var (name, _) in scripts)
        {
            diagnostics.AddRange(
                compilation.GetDiagnostics(QualifiedName.Parse(name), TestContext.Current.CancellationToken));
        }

        return (compilation, diagnostics);
    }

    private static PexFile Emit(PapyrusCompilation compilation, string scriptName)
    {
        var emitter = new PexEmitter();
        var file = emitter.EmitFile(compilation.GetScript(QualifiedName.Parse(scriptName)), TestContext.Current.CancellationToken);
        Assert.Empty(emitter.Diagnostics);
        return file;
    }

    private static PexFunction Function(PexFile file, string name)
    {
        return Assert.Single(file.Objects)
            .States.SelectMany(static state => state.Functions)
            .Single(function => string.Equals(function.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
