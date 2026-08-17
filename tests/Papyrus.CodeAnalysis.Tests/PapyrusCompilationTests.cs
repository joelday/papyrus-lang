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
}
