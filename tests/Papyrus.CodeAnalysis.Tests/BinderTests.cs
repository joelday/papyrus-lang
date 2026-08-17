using System;
using System.Collections.Generic;
using System.Linq;
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

    private static ISymbol SingleMember(ISymbol container, NameTable names, string name)
    {
        return Assert.Single(container.GetMembers(names.Get(name)));
    }
}
