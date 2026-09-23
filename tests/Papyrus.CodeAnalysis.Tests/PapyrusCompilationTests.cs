using System;
using System.Collections.Generic;
using System.Threading;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class PapyrusCompilationTests
{
    [Fact]
    public void Create_null_arguments_throw()
    {
        var resolver = new TestScriptResolver();
        Assert.Throws<ArgumentNullException>(() => PapyrusCompilation.Create(null!, resolver));
        Assert.Throws<ArgumentNullException>(() => PapyrusCompilation.Create(PapyrusDialect.SkyrimSe, null!));
    }

    [Fact]
    public void GetScript_source_has_name_weak_syntax_and_declared_members()
    {
        var name = QualifiedName.Parse("Foo");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [name] = SourceText.From("ScriptName Foo\nFunction Bar()\nEndFunction\n"),
            }));

        var script = compilation.GetScript(name);

        Assert.Equal("Foo", script.Name.Text);
        Assert.Equal("Foo", script.QualifiedName.ToString());
        Assert.Equal(SymbolKind.Script, script.Kind);
        Assert.Equal("Bar", Assert.Single(script.GetMembers()).Name.Text);
        Assert.Null(script.BaseType);
        var reference = Assert.Single(script.DeclaringSyntaxReferences);
        Assert.Same(compilation.GetSyntaxTree(name), reference.SyntaxTree);
        Assert.Same(compilation.GetSyntaxTree(name), compilation.GetSyntaxTree(QualifiedName.Parse("foo")));
    }

    [Fact]
    public void GetScript_pex_uses_adapter()
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
                pex: new Dictionary<string, PexScriptMetadata> { ["Bar"] = metadata }));

        var script = compilation.GetScript(QualifiedName.Parse("Bar"));

        Assert.Equal("Bar", script.QualifiedName.ToString());
        Assert.Equal("Go", Assert.Single(script.GetMembers()).Name.Text);
        Assert.Empty(script.DeclaringSyntaxReferences);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GetScript_missing_returns_error_symbol_and_diagnostic()
    {
        var compilation = PapyrusCompilation.Create(PapyrusDialect.SkyrimSe, new TestScriptResolver());
        var name = QualifiedName.Parse("Missing");

        var script = compilation.GetScript(name);

        Assert.Equal("Missing", script.Name.Text);
        Assert.Empty(script.GetMembers());
        Assert.Empty(script.DeclaringSyntaxReferences);
        var diagnostic = Assert.Single(compilation.GetDiagnostics(TestContext.Current.CancellationToken));
        Assert.Equal(PapyrusCompilation.UnresolvedScriptDiagnosticId, diagnostic.Id);
        Assert.Contains("Missing", diagnostic.Message, StringComparison.Ordinal);
        Assert.Same(compilation.GetSyntaxTree(name), diagnostic.Tree);
    }

    [Fact]
    public void GetSyntaxTree_missing_does_not_throw()
    {
        var compilation = PapyrusCompilation.Create(PapyrusDialect.SkyrimSe, new TestScriptResolver());
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Nope"));

        Assert.True(tree.TryGetRoot(out var root));
        Assert.True(root.Name.IsMissing);
        Assert.Single(compilation.GetDiagnostics(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GetDiagnostics_only_includes_requested_trees()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse("Foo")] = SourceText.From("ScriptName Foo\n"),
                [QualifiedName.Parse("Bar")] = SourceText.From("ScriptName Bar\n"),
            }));

        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken));
        _ = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GetDiagnostics_by_name_scopes_to_the_requested_script()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(
                new Dictionary<QualifiedName, SourceText>
                {
                    [QualifiedName.Parse("Foo")] = SourceText.From(
                        "ScriptName Foo\nInt Property Bar Auto\nUnknownType Baz\n"
                    ),
                    [QualifiedName.Parse("Other")] = SourceText.From("ScriptName Other\n"),
                }
            )
        );

        var diagnostics = compilation.GetDiagnostics(
            QualifiedName.Parse("Foo"),
            TestContext.Current.CancellationToken
        );

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Same(compilation.GetSyntaxTree(QualifiedName.Parse("Foo")), d.Tree));

        // Scoping to "Foo" must not force-complete or report anything for "Other".
        Assert.Empty(
            compilation.GetDiagnostics(QualifiedName.Parse("Other"), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public void WithScriptText_returns_new_snapshot_and_leaves_original()
    {
        var name = QualifiedName.Parse("Foo");
        var original = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [name] = SourceText.From("ScriptName Foo\n"),
            }));
        var originalTree = original.GetSyntaxTree(name);

        var updated = original.WithScriptText(name, SourceText.From("ScriptName Foo\nFunction Go()\nEndFunction\n"));

        Assert.NotSame(original, updated);
        Assert.Same(originalTree, original.GetSyntaxTree(name));
        Assert.NotSame(originalTree, updated.GetSyntaxTree(name));
        Assert.Equal("ScriptName Foo\nFunction Go()\nEndFunction\n", updated.GetSyntaxTree(name).GetText().ToString());
        Assert.Equal("Go", Assert.Single(updated.GetScript(name).GetMembers()).Name.Text);
        Assert.Same(original.Names, updated.Names);
        Assert.Same(original.Resolver, updated.Resolver);
    }

    [Fact]
    public void WithSyntaxTree_replaces_by_declared_name()
    {
        var compilation = PapyrusCompilation.Create(PapyrusDialect.SkyrimSe, new TestScriptResolver());
        var tree = SyntaxTree.Parse("ScriptName Widget\n", PapyrusDialect.SkyrimSe);

        var updated = compilation.WithSyntaxTree(tree);

        Assert.Same(tree, updated.GetSyntaxTree(QualifiedName.Parse("Widget")));
        Assert.Equal("Widget", updated.GetScript(QualifiedName.Parse("Widget")).Name.Text);
        Assert.Single(updated.GetScript(QualifiedName.Parse("Widget")).DeclaringSyntaxReferences);
    }

    [Fact]
    public void WithSyntaxTree_namespaced_declaration_uses_qualified_name()
    {
        var compilation = PapyrusCompilation.Create(PapyrusDialect.Fallout4, new TestScriptResolver());
        var tree = SyntaxTree.Parse("ScriptName A:B:Script\n", PapyrusDialect.Fallout4);

        var updated = compilation.WithSyntaxTree(tree);

        Assert.Same(tree, updated.GetSyntaxTree(QualifiedName.Parse("A:B:Script")));
    }

    [Fact]
    public void WithSyntaxTree_missing_name_throws()
    {
        var compilation = PapyrusCompilation.Create(PapyrusDialect.SkyrimSe, new TestScriptResolver());
        var tree = SyntaxTree.Parse(string.Empty, PapyrusDialect.SkyrimSe);

        Assert.Throws<ArgumentException>(() => compilation.WithSyntaxTree(tree));
    }

    [Fact]
    public void GetDiagnostics_honors_cancellation()
    {
        var compilation = PapyrusCompilation.Create(PapyrusDialect.SkyrimSe, new TestScriptResolver());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => compilation.GetDiagnostics(cts.Token));
    }

    [Fact]
    public void Source_is_preferred_over_pex()
    {
        var name = QualifiedName.Parse("Foo");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(
                new Dictionary<QualifiedName, SourceText>
                {
                    [name] = SourceText.From("ScriptName Foo\n"),
                },
                new Dictionary<string, PexScriptMetadata>
                {
                    ["Foo"] = new PexScriptMetadata
                    {
                        Name = new PexQualifiedName("Foo"),
                        Functions =
                        [
                            new PexFunctionMetadata { Name = "FromPex", ReturnTypeName = "None" },
                        ],
                    },
                }));

        var script = compilation.GetScript(name);
        Assert.Empty(script.GetMembers());
        Assert.NotEmpty(script.DeclaringSyntaxReferences);
    }

    [Fact]
    public void WithScriptText_invalidates_dependents_and_keeps_unrelated_script()
    {
        var actor = QualifiedName.Parse("Actor");
        var quest = QualifiedName.Parse("Quest");
        var widget = QualifiedName.Parse("Widget");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [actor] = SourceText.From("ScriptName Actor\nFunction Old()\nEndFunction\n"),
                [quest] = SourceText.From("ScriptName Quest Extends Actor\n"),
                [widget] = SourceText.From("ScriptName Widget\nFunction Idle()\nEndFunction\n"),
            }));

        var actorSymbol = compilation.GetScript(actor);
        var questSymbol = compilation.GetScript(quest);
        var widgetSymbol = compilation.GetScript(widget);
        Assert.Same(actorSymbol, questSymbol.BaseType);
        Assert.Contains(quest, compilation.GetDependents(actor));
        Assert.DoesNotContain(actor, compilation.GetDependents(actor));
        Assert.DoesNotContain(widget, compilation.GetDependents(actor));
        Assert.Empty(compilation.GetDependents(widget));

        var updated = compilation.WithScriptText(
            actor,
            SourceText.From("ScriptName Actor\nFunction Old()\nEndFunction\nFunction NewMember()\nEndFunction\n"));

        var newActor = updated.GetScript(actor);
        var newQuest = updated.GetScript(quest);
        var newWidget = updated.GetScript(widget);

        Assert.Same(widgetSymbol, newWidget);
        Assert.NotSame(questSymbol, newQuest);
        Assert.NotSame(actorSymbol, newActor);
        Assert.Same(newActor, newQuest.BaseType);
        Assert.Contains(newActor.GetMembers(), static member => member.Name.Text.Equals("NewMember", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(newQuest.BaseType!.GetMembers(), static member => member.Name.Text.Equals("NewMember", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WithScriptText_invalidates_transitive_dependents_only()
    {
        var a = QualifiedName.Parse("A");
        var b = QualifiedName.Parse("B");
        var c = QualifiedName.Parse("C");
        var d = QualifiedName.Parse("D");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [a] = SourceText.From("ScriptName A\n"),
                [b] = SourceText.From("ScriptName B Extends A\n"),
                [c] = SourceText.From("ScriptName C Extends B\n"),
                [d] = SourceText.From("ScriptName D\n"),
            }));

        var aSymbol = compilation.GetScript(a);
        var bSymbol = compilation.GetScript(b);
        var cSymbol = compilation.GetScript(c);
        var dSymbol = compilation.GetScript(d);
        var dependents = compilation.GetDependents(a);
        Assert.Contains(b, dependents);
        Assert.Contains(c, dependents);
        Assert.DoesNotContain(d, dependents);
        Assert.DoesNotContain(a, dependents);

        var updated = compilation.WithScriptText(a, SourceText.From("ScriptName A\nFunction NewMember()\nEndFunction\n"));

        Assert.Same(dSymbol, updated.GetScript(d));
        Assert.NotSame(aSymbol, updated.GetScript(a));
        Assert.NotSame(bSymbol, updated.GetScript(b));
        Assert.NotSame(cSymbol, updated.GetScript(c));
        Assert.Same(updated.GetScript(a), updated.GetScript(b).BaseType);
        Assert.Same(updated.GetScript(b), updated.GetScript(c).BaseType);
        Assert.Contains(
            updated.GetScript(c).BaseType!.BaseType!.GetMembers(),
            static member => member.Name.Text.Equals("NewMember", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetScriptForType_records_type_reference_dependents()
    {
        var actor = QualifiedName.Parse("Actor");
        var quest = QualifiedName.Parse("Quest");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [actor] = SourceText.From("ScriptName Actor\n"),
                [quest] = SourceText.From("ScriptName Quest\nActor Property Target Auto\n"),
            }));

        _ = compilation.GetScript(quest);

        Assert.Contains(quest, compilation.GetDependents(actor));

        var updated = compilation.WithScriptText(actor, SourceText.From("ScriptName Actor\nFunction Ping()\nEndFunction\n"));
        var rebound = updated.GetScript(quest);
        var target = Assert.IsAssignableFrom<IPropertySymbol>(
            Assert.Single(rebound.GetMembers(updated.Names.Get("Target"))));
        var actorType = Assert.IsAssignableFrom<IScriptSymbol>(target.Type);
        Assert.Equal("Ping", Assert.Single(actorType.GetMembers()).Name.Text);
    }
}
