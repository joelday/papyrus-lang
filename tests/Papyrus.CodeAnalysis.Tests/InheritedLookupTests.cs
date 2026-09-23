using System.Collections.Generic;
using System.Linq;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Emit;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

/// <summary>
/// Name lookups that Caprica (and the CK compiler) resolve through the script
/// hierarchy or treat as built-ins: structs declared in a parent script, and the
/// FO4+ <c>CustomEventName</c> / <c>ScriptEventName</c> parameter types.
/// </summary>
public sealed class InheritedLookupTests
{
    private const string ScriptObject =
        """
        ScriptName ScriptObject Native Hidden
        Function RegisterForCustomEvent(ScriptObject akEventSource, CustomEventName asEventName) native
        Function SendCustomEvent(CustomEventName asEventName, Var[] akArgs = None) native
        Function RegisterForRemoteEvent(ScriptObject akEventSource, ScriptEventName asEventName) native
        """;

    [Fact]
    public void Struct_declared_in_parent_resolves_unqualified_in_child()
    {
        var (compilation, diagnostics) = Compile(
            PapyrusDialect.Fallout4,
            ("Base", "ScriptName Base\nStruct StructA\n  Int X\nEndStruct\n"),
            ("Child", "ScriptName Child Extends Base\nStructA hello\nFunction F()\n  StructA[] items = new StructA[0]\nEndFunction\n"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Struct_declared_in_grandparent_resolves_when_qualified_by_child()
    {
        var (compilation, diagnostics) = Compile(
            PapyrusDialect.Fallout4,
            ("Base", "ScriptName Base\nStruct StructA\n  Int X\nEndStruct\n"),
            ("Middle", "ScriptName Middle Extends Base\n"),
            ("Child", "ScriptName Child Extends Middle\nChild:StructA hello\n"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Struct_lookup_terminates_on_extends_cycle()
    {
        var (compilation, diagnostics) = Compile(
            PapyrusDialect.Fallout4,
            ("A", "ScriptName A Extends B\nMissing x\n"),
            ("B", "ScriptName B Extends A\n"));

        Assert.Contains(diagnostics, static d => d.Id == DiagnosticCatalog.CyclicBaseTypeId);
        Assert.Contains(diagnostics, static d => d.Id == DiagnosticCatalog.UnresolvedTypeId);
    }

    [Fact]
    public void Event_name_parameter_types_bind_in_fallout4_and_emit_as_string()
    {
        var (compilation, diagnostics) = Compile(PapyrusDialect.Fallout4, ("ScriptObject", ScriptObject));

        Assert.Empty(diagnostics);

        var file = Emit(compilation, "ScriptObject");
        var register = Function(file, "RegisterForCustomEvent");
        Assert.Equal("String", register.Parameters[1].TypeName);
        var remote = Function(file, "RegisterForRemoteEvent");
        Assert.Equal("String", remote.Parameters[1].TypeName);
    }

    [Fact]
    public void Event_name_types_are_ordinary_identifiers_in_skyrim()
    {
        var (compilation, diagnostics) = Compile(
            PapyrusDialect.SkyrimSe,
            ("Foo", "ScriptName Foo\nFunction F(CustomEventName a) native\n"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticCatalog.UnresolvedTypeId, diagnostic.Id);
    }

    [Fact]
    public void Custom_event_argument_is_qualified_by_the_declaring_script()
    {
        var (compilation, diagnostics) = Compile(
            PapyrusDialect.Fallout4,
            ("ScriptObject", ScriptObject),
            ("Sender", "ScriptName Sender Extends ScriptObject\nCustomEvent OnThing\n"),
            ("DerivedSender", "ScriptName DerivedSender Extends Sender\nFunction Fire()\n  SendCustomEvent(\"OnThing\")\nEndFunction\n"),
            ("Listener",
                "ScriptName Listener Extends ScriptObject\n"
                + "Function Listen(DerivedSender akSender)\n"
                + "  RegisterForCustomEvent(akSender, \"OnThing\")\n"
                + "  RegisterForRemoteEvent(akSender, \"OnInit\")\n"
                + "EndFunction\n"));

        Assert.Empty(diagnostics);

        // Self receiver: the event is found on a parent and named after its declaring script.
        Assert.Contains("Sender_OnThing", StringArguments(Function(Emit(compilation, "DerivedSender"), "Fire")));

        // Argument 1 resolves against argument 0's type; script events are left alone.
        var listen = StringArguments(Function(Emit(compilation, "Listener"), "Listen"));
        Assert.Contains("Sender_OnThing", listen);
        Assert.Contains("OnInit", listen);
        Assert.DoesNotContain("OnThing", listen);
    }

    [Fact]
    public void Overridden_script_binds_against_the_compilation_that_holds_the_override()
    {
        // papyrusc and the language server both override a script that the resolver can
        // also load from disk. Binding used to go through the pre-override compilation, whose
        // semantic model could not map the override's syntax back to its function, so the
        // local shadowing the function's own name resolved to the function (emitted as Self).
        const string source = "ScriptName Repro\nFunction Payout()\n  int Payout = 0\n  Payout += 1\nEndFunction\n";
        var name = QualifiedName.Parse("Repro");
        var onDisk = new Dictionary<QualifiedName, SourceText> { [name] = SourceText.From(source) };
        var compilation = PapyrusCompilation.Create(PapyrusDialect.Fallout4, new TestScriptResolver(onDisk))
            .WithScriptText(name, SourceText.From(source));

        var emitter = new PexEmitter();
        emitter.EmitFile(compilation.GetScript(name), TestContext.Current.CancellationToken);

        Assert.Empty(emitter.Diagnostics);
    }

    /// <summary>
    /// Compiles <paramref name="scripts"/> and returns every script's diagnostics (the
    /// per-script overload forces binding; the whole-compilation one only reports
    /// scripts some earlier caller already completed).
    /// </summary>
    private static (PapyrusCompilation Compilation, List<Diagnostic> Diagnostics) Compile(
        PapyrusDialect dialect,
        params (string Name, string Source)[] scripts)
    {
        var sources = new Dictionary<QualifiedName, SourceText>();
        foreach (var (name, source) in scripts)
        {
            sources[QualifiedName.Parse(name)] = SourceText.From(source);
        }

        var compilation = PapyrusCompilation.Create(dialect, new TestScriptResolver(sources));
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
        return new PexEmitter().EmitFile(compilation.GetScript(QualifiedName.Parse(scriptName)));
    }

    private static PexFunction Function(PexFile file, string name)
    {
        return Assert.Single(file.Objects)
            .States.SelectMany(static state => state.Functions)
            .Single(function => string.Equals(function.Name, name, System.StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> StringArguments(PexFunction function)
    {
        return function.Instructions
            .SelectMany(static instruction => instruction.Arguments.Concat(instruction.VariadicArguments))
            .Where(static value => value.Type == PexValueType.String)
            .Select(static value => value.Text ?? "")
            .ToList();
    }
}
