using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class BinderTests
{
    [Fact]
    public void GetScript_lists_declared_members_from_syntax()
    {
        var name = QualifiedName.Parse("Foo");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.Starfield,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [name] = SourceText.From(
                    "ScriptName Foo\n" +
                    "Int Property Health Auto\n" +
                    "Int Count = 1\n" +
                    "Function Bar() Native\n" +
                    "Event OnInit()\nEndEvent\n" +
                    "Auto State Ready\nFunction Swing()\nEndFunction\nEndState\n" +
                    "Struct Point\nFloat X\nEndStruct\n" +
                    "Group Stats\nInt Property Magicka Auto\nEndGroup\n" +
                    "CustomEvent OnSomething\n" +
                    "Guard Mutex\n"),
            }));

        var script = compilation.GetScript(name);
        var names = compilation.Names;

        var bar = Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(script, names, "Bar"));
        Assert.Equal(SymbolKind.Function, bar.Kind);
        Assert.True(bar.IsNative);
        Assert.Empty(bar.Parameters);

        Assert.IsAssignableFrom<IEventSymbol>(SingleMember(script, names, "OnInit"));
        Assert.IsAssignableFrom<IPropertySymbol>(SingleMember(script, names, "Health"));
        Assert.IsAssignableFrom<IVariableSymbol>(SingleMember(script, names, "Count"));
        Assert.IsAssignableFrom<IPropertySymbol>(SingleMember(script, names, "Magicka"));
        Assert.IsAssignableFrom<ICustomEventSymbol>(SingleMember(script, names, "OnSomething"));
        Assert.IsAssignableFrom<IGuardSymbol>(SingleMember(script, names, "Mutex"));

        var state = Assert.IsAssignableFrom<IStateSymbol>(SingleMember(script, names, "Ready"));
        Assert.True(state.IsAuto);
        Assert.Equal("Swing", Assert.Single(state.GetMembers()).Name.Text);

        var struc = Assert.IsAssignableFrom<IStructSymbol>(SingleMember(script, names, "Point"));
        Assert.Equal("X", Assert.Single(struc.GetMembers()).Name.Text);
        Assert.Equal(TypeKind.Struct, struc.TypeKind);
    }

    [Fact]
    public void GetScript_resolves_extends_to_source_base()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse("Child")] = SourceText.From("ScriptName Child Extends Parent\nFunction Go()\nEndFunction\n"),
                [QualifiedName.Parse("Parent")] = SourceText.From("ScriptName Parent\nFunction Base()\nEndFunction\n"),
            }));

        var child = compilation.GetScript(QualifiedName.Parse("Child"));
        var parent = compilation.GetScript(QualifiedName.Parse("Parent"));

        Assert.Same(parent, child.BaseType);
        Assert.Equal("Base", Assert.Single(parent.GetMembers()).Name.Text);
        Assert.Equal("Go", Assert.Single(child.GetMembers()).Name.Text);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GetScript_missing_base_is_error_type_with_diagnostic()
    {
        var name = QualifiedName.Parse("Child");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [name] = SourceText.From("ScriptName Child Extends Missing\n"),
            }));

        var child = compilation.GetScript(name);

        Assert.NotNull(child.BaseType);
        Assert.Equal("Missing", child.BaseType.Name.Text);
        Assert.Equal(TypeKind.Error, child.BaseType.TypeKind);
        var diagnostic = Assert.Single(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static d => d.Id == PapyrusCompilation.UnresolvedBaseTypeDiagnosticId));
        Assert.Contains("Missing", diagnostic.Message, StringComparison.Ordinal);
        Assert.Same(compilation.GetSyntaxTree(name), diagnostic.Tree);
    }

    [Fact]
    public void GetScript_extends_cycle_is_error_type_on_each_participant()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse("A")] = SourceText.From("ScriptName A Extends B\n"),
                [QualifiedName.Parse("B")] = SourceText.From("ScriptName B Extends A\n"),
            }));

        var a = compilation.GetScript(QualifiedName.Parse("A"));
        var b = compilation.GetScript(QualifiedName.Parse("B"));

        Assert.NotNull(a.BaseType);
        Assert.NotNull(b.BaseType);
        Assert.NotSame(b, a.BaseType);
        Assert.NotSame(a, b.BaseType);
        Assert.Equal(TypeKind.Error, a.BaseType.TypeKind);
        Assert.Equal(TypeKind.Error, b.BaseType.TypeKind);
        Assert.Equal("B", a.BaseType.Name.Text);
        Assert.Equal("A", b.BaseType.Name.Text);

        var cycle = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Id == PapyrusCompilation.CyclicBaseTypeDiagnosticId)
            .ToArray();
        Assert.Equal(2, cycle.Length);
        Assert.Contains(cycle, static d => d.Message.Contains("A", StringComparison.Ordinal));
        Assert.Contains(cycle, static d => d.Message.Contains("B", StringComparison.Ordinal));
    }

    [Fact]
    public void GetScript_mutual_field_types_are_not_an_extends_cycle()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse("A")] = SourceText.From("ScriptName A\nB Other\n"),
                [QualifiedName.Parse("B")] = SourceText.From("ScriptName B\nA Other\n"),
            }));

        var a = compilation.GetScript(QualifiedName.Parse("A"));
        var b = compilation.GetScript(QualifiedName.Parse("B"));

        Assert.Null(a.BaseType);
        Assert.Null(b.BaseType);
        Assert.Equal("Other", Assert.Single(a.GetMembers()).Name.Text);
        Assert.Equal("Other", Assert.Single(b.GetMembers()).Name.Text);
        Assert.Empty(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static d => d.Id == PapyrusCompilation.CyclicBaseTypeDiagnosticId));
    }

    [Fact]
    public void GetScript_source_extends_pex_base()
    {
        var metadata = new PexScriptMetadata
        {
            Name = new PexQualifiedName("Bar"),
            Functions =
            [
                new PexFunctionMetadata { Name = "Go", ReturnTypeName = "None" },
            ],
        };
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(
                new Dictionary<QualifiedName, SourceText>
                {
                    [QualifiedName.Parse("Foo")] = SourceText.From("ScriptName Foo Extends Bar\n"),
                },
                new Dictionary<string, PexScriptMetadata> { ["Bar"] = metadata }));

        var foo = compilation.GetScript(QualifiedName.Parse("Foo"));
        var bar = compilation.GetScript(QualifiedName.Parse("Bar"));

        Assert.Same(bar, foo.BaseType);
        Assert.Equal("Go", Assert.Single(bar.GetMembers()).Name.Text);
        Assert.Empty(bar.DeclaringSyntaxReferences);
        Assert.Empty(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static d =>
                    d.Id == PapyrusCompilation.UnresolvedBaseTypeDiagnosticId
                    || d.Id == PapyrusCompilation.CyclicBaseTypeDiagnosticId));
    }

    [Fact]
    public void GetScript_pex_base_from_metadata_is_unchanged()
    {
        var metadata = new PexScriptMetadata
        {
            Name = new PexQualifiedName("Child"),
            BaseType = new PexQualifiedName("Parent"),
            Functions =
            [
                new PexFunctionMetadata { Name = "Go", ReturnTypeName = "None" },
            ],
        };
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(
                pex: new Dictionary<string, PexScriptMetadata> { ["Child"] = metadata }));

        var script = compilation.GetScript(QualifiedName.Parse("Child"));

        Assert.Equal("Parent", script.BaseType?.Name.Text);
        Assert.Equal("Go", Assert.Single(script.GetMembers()).Name.Text);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ForceComplete_members_and_base_type_is_idempotent()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse("Child")] = SourceText.From("ScriptName Child Extends Parent\nFunction Go()\nEndFunction\n"),
                [QualifiedName.Parse("Parent")] = SourceText.From("ScriptName Parent\n"),
            }));

        var source = Assert.IsType<SourceScriptSymbol>(compilation.GetScript(QualifiedName.Parse("Child")));
        var members = source.GetMembers();
        var baseType = source.BaseType;

        source.ForceComplete(CompletionPart.Members | CompletionPart.BaseType, TestContext.Current.CancellationToken);
        source.ForceComplete(CompletionPart.All, TestContext.Current.CancellationToken);

        Assert.Equal(members, source.GetMembers());
        Assert.Same(baseType, source.BaseType);
    }

    [Fact]
    public void GetScript_namespaced_extends_resolves()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.Fallout4,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse("A:B:Child")] = SourceText.From("ScriptName A:B:Child Extends A:B:Parent\n"),
                [QualifiedName.Parse("A:B:Parent")] = SourceText.From("ScriptName A:B:Parent\n"),
            }));

        var child = compilation.GetScript(QualifiedName.Parse("A:B:Child"));
        var parent = compilation.GetScript(QualifiedName.Parse("A:B:Parent"));

        Assert.Same(parent, child.BaseType);
        Assert.Equal("A:B:Parent", parent.QualifiedName.ToString());
    }

    [Fact]
    public void GetScript_binds_function_return_and_parameter_types()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.Fallout4,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse("Foo")] = SourceText.From(
                    "ScriptName Foo\n" +
                    "Struct Point\nFloat X\nEndStruct\n" +
                    "Int Function Mix(Foo[] items, A:B:Bar other, Foo#Point origin)\nEndFunction\n"),
                [QualifiedName.Parse("A:B:Bar")] = SourceText.From("ScriptName A:B:Bar\n"),
            }));

        var foo = compilation.GetScript(QualifiedName.Parse("Foo"));
        var bar = compilation.GetScript(QualifiedName.Parse("A:B:Bar"));
        var names = compilation.Names;
        var mix = Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(foo, names, "Mix"));

        Assert.Equal(TypeKind.Intrinsic, mix.ReturnType.TypeKind);
        Assert.Equal("Int", mix.ReturnType.Name.Text);
        Assert.Equal(3, mix.Parameters.Length);

        var items = mix.Parameters[0];
        Assert.Equal("items", items.Name.Text);
        Assert.Equal(TypeKind.Array, items.Type.TypeKind);
        Assert.Equal("Foo", items.Type.GetArrayElementType()?.Name.Text);
        Assert.Same(foo, items.Type.GetArrayElementType());

        Assert.Same(bar, mix.Parameters[1].Type);
        Assert.Equal("A:B:Bar", mix.Parameters[1].Type.TypeName.Name.ToString());

        var origin = mix.Parameters[2];
        var point = Assert.IsAssignableFrom<IStructSymbol>(SingleMember(foo, names, "Point"));
        Assert.Same(point, origin.Type);
        Assert.Equal(TypeKind.Struct, origin.Type.TypeKind);
        Assert.Equal("Point", origin.Type.TypeName.StructName?.Text);
        Assert.Empty(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static d => d.Id == PapyrusCompilation.UnresolvedTypeDiagnosticId));
    }

    [Fact]
    public void GetScript_binds_property_variable_and_struct_member_types()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.Fallout4,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse("Foo")] = SourceText.From(
                    "ScriptName Foo\n" +
                    "Int Property Health Auto\n" +
                    "String Label = \"x\"\n" +
                    "Struct Point\nFloat X\nEndStruct\n"),
            }));

        var foo = compilation.GetScript(QualifiedName.Parse("Foo"));
        var names = compilation.Names;

        var health = Assert.IsAssignableFrom<IPropertySymbol>(SingleMember(foo, names, "Health"));
        Assert.Equal(TypeKind.Intrinsic, health.Type.TypeKind);
        Assert.Equal("Int", health.Type.Name.Text);

        var label = Assert.IsAssignableFrom<IVariableSymbol>(SingleMember(foo, names, "Label"));
        Assert.Equal("String", label.Type.Name.Text);

        var point = Assert.IsAssignableFrom<IStructSymbol>(SingleMember(foo, names, "Point"));
        var x = Assert.IsAssignableFrom<IVariableSymbol>(SingleMember(point, names, "X"));
        Assert.Equal("Float", x.Type.Name.Text);
        Assert.Equal(TypeKind.Intrinsic, x.Type.TypeKind);
    }

    [Fact]
    public void GetScript_missing_type_is_error_with_diagnostic()
    {
        var name = QualifiedName.Parse("Foo");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [name] = SourceText.From("ScriptName Foo\nMissing Function Bad(NoSuch value)\nEndFunction\n"),
            }));

        var foo = compilation.GetScript(name);
        var bad = Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(foo, compilation.Names, "Bad"));

        Assert.Equal(TypeKind.Error, bad.ReturnType.TypeKind);
        Assert.Equal("Missing", bad.ReturnType.Name.Text);
        var parameter = Assert.Single(bad.Parameters);
        Assert.Equal(TypeKind.Error, parameter.Type.TypeKind);
        Assert.Equal("NoSuch", parameter.Type.Name.Text);

        var diagnostics = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Id == PapyrusCompilation.UnresolvedTypeDiagnosticId)
            .ToArray();
        Assert.Equal(2, diagnostics.Length);
        Assert.Contains(diagnostics, static d => d.Message.Contains("Missing", StringComparison.Ordinal));
        Assert.Contains(diagnostics, static d => d.Message.Contains("NoSuch", StringComparison.Ordinal));
        Assert.All(diagnostics, d => Assert.Same(compilation.GetSyntaxTree(name), d.Tree));
    }

    [Fact]
    public void GetScript_binds_function_and_event_body_locals()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse("Foo")] = SourceText.From(
                    "ScriptName Foo\n" +
                    "Function Go(Int seed)\n" +
                    "  Float local = 1.0\n" +
                    "  If seed\n" +
                    "    String inner = \"x\"\n" +
                    "  EndIf\n" +
                    "EndFunction\n" +
                    "Event OnInit()\n" +
                    "  Bool ready = true\n" +
                    "EndEvent\n"),
            }));

        var foo = compilation.GetScript(QualifiedName.Parse("Foo"));
        var names = compilation.Names;

        var go = Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(foo, names, "Go"));
        Assert.Equal(2, go.Locals.Length);
        Assert.Equal("local", go.Locals[0].Name.Text);
        Assert.Equal("Float", go.Locals[0].Type.Name.Text);
        Assert.Equal(TypeKind.Intrinsic, go.Locals[0].Type.TypeKind);
        Assert.Equal("inner", go.Locals[1].Name.Text);
        Assert.Equal("String", go.Locals[1].Type.Name.Text);

        var onInit = Assert.IsAssignableFrom<IEventSymbol>(SingleMember(foo, names, "OnInit"));
        var ready = Assert.Single(onInit.Locals);
        Assert.Equal("ready", ready.Name.Text);
        Assert.Equal("Bool", ready.Type.Name.Text);
        Assert.Equal("None", onInit.ReturnType.Name.Text);
    }

    [Fact]
    public void GetScript_unknown_local_type_is_error_with_diagnostic()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse("Foo")] = SourceText.From(
                    "ScriptName Foo\nFunction Go()\n  Missing local = 1\nEndFunction\n"),
            }));

        var go = Assert.IsAssignableFrom<IFunctionSymbol>(
            SingleMember(compilation.GetScript(QualifiedName.Parse("Foo")), compilation.Names, "Go"));
        var local = Assert.Single(go.Locals);
        Assert.Equal(TypeKind.Error, local.Type.TypeKind);
        Assert.Contains(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static d => d.Id == PapyrusCompilation.UnresolvedTypeDiagnosticId
                && d.Message.Contains("Missing", StringComparison.Ordinal));
    }

    [Fact]
    public void GetScript_mutual_field_types_resolve()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse("A")] = SourceText.From("ScriptName A\nB Other\n"),
                [QualifiedName.Parse("B")] = SourceText.From("ScriptName B\nA Other\n"),
            }));

        var a = compilation.GetScript(QualifiedName.Parse("A"));
        var b = compilation.GetScript(QualifiedName.Parse("B"));
        var aOther = Assert.IsAssignableFrom<IVariableSymbol>(Assert.Single(a.GetMembers()));
        var bOther = Assert.IsAssignableFrom<IVariableSymbol>(Assert.Single(b.GetMembers()));

        Assert.Same(b, aOther.Type);
        Assert.Same(a, bOther.Type);
        Assert.Empty(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static d =>
                    d.Id == PapyrusCompilation.CyclicBaseTypeDiagnosticId
                    || d.Id == PapyrusCompilation.UnresolvedTypeDiagnosticId));
    }

    [Fact]
    public void Concurrent_GetScript_and_ForceComplete_do_not_drop_members()
    {
        var childName = QualifiedName.Parse("Child");
        var parentName = QualifiedName.Parse("Parent");
        var otherName = QualifiedName.Parse("Other");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [childName] = SourceText.From(
                    "ScriptName Child Extends Parent\nInt Function Run(Other value)\n  Float local = 1.0\nEndFunction\n"),
                [parentName] = SourceText.From("ScriptName Parent\nInt Function Base(Child who)\nEndFunction\n"),
                [otherName] = SourceText.From("ScriptName Other Extends Parent\nChild Property Link Auto\n"),
            }));

        var failures = new ConcurrentBag<string>();
        void Work()
        {
            try
            {
                for (var i = 0; i < 25; i++)
                {
                    var child = compilation.GetScript(childName);
                    var parent = compilation.GetScript(parentName);
                    var other = compilation.GetScript(otherName);

                    if (child.GetMembers().IsDefaultOrEmpty)
                    {
                        failures.Add("child members empty");
                    }

                    if (parent.GetMembers().IsDefaultOrEmpty)
                    {
                        failures.Add("parent members empty");
                    }

                    if (other.GetMembers().IsDefaultOrEmpty)
                    {
                        failures.Add("other members empty");
                    }

                    if (child.BaseType is null || child.BaseType.TypeKind == TypeKind.Error)
                    {
                        failures.Add("child base missing");
                    }

                    if (other.BaseType is null || other.BaseType.TypeKind == TypeKind.Error)
                    {
                        failures.Add("other base missing");
                    }

                    var run = child.GetMembers(compilation.Names.Get("Run"));
                    if (run.IsDefaultOrEmpty || run[0] is not IFunctionSymbol runFn)
                    {
                        failures.Add("run missing");
                        continue;
                    }

                    if (!string.Equals(runFn.ReturnType.Name.Text, "Int", StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add("run return " + runFn.ReturnType.Name.Text);
                    }

                    if (runFn.Parameters.IsDefaultOrEmpty)
                    {
                        failures.Add("run parameters empty");
                    }

                    if (runFn.Locals.IsDefaultOrEmpty)
                    {
                        failures.Add("run locals empty");
                    }

                    if (child is SourceScriptSymbol source)
                    {
                        source.ForceComplete(CompletionPart.All, TestContext.Current.CancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                failures.Add(ex.ToString());
            }
        }

        var threads = new Thread[6];
        for (var t = 0; t < threads.Length; t++)
        {
            threads[t] = new Thread(Work) { IsBackground = true };
            threads[t].Start();
        }

        foreach (var thread in threads)
        {
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "worker thread timed out");
        }

        Assert.Empty(failures);
    }

    private static ISymbol SingleMember(ISymbol container, NameTable names, string name)
    {
        return Assert.Single(container.GetMembers(names.Get(name)));
    }
}
