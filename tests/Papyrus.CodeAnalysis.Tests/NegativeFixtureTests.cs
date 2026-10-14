using System;
using System.Collections.Generic;
using System.IO;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Testing;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class NegativeFixtureTests
{
    [Fact]
    public void Each_negative_fixture_binds_and_produces_a_diagnostic()
    {
        var fixtures = ParseFixtureCatalog.Load(ParseFixtureCatalog.Find("Bind", "Negative"));
        Assert.NotEmpty(fixtures);

        foreach (var fixture in fixtures)
        {
            var tree = SyntaxTree.Parse(fixture.Source, fixture.Dialect);
            var name = DeclaredName(tree) ?? QualifiedName.Parse(
                Path.GetFileNameWithoutExtension(fixture.Name.Replace('/', Path.DirectorySeparatorChar)));
            var compilation = PapyrusCompilation.Create(
                fixture.Dialect,
                new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
                {
                    [name] = SourceText.From(fixture.Source),
                }));

            _ = compilation.GetScript(name);
            Assert.False(
                compilation.GetDiagnostics(TestContext.Current.CancellationToken).IsDefaultOrEmpty,
                fixture.Name + " bound without diagnostics.");
        }
    }

    private static QualifiedName? DeclaredName(SyntaxTree tree)
    {
        if (!tree.TryGetRoot(out var root) || root.Name.IsMissing)
        {
            return null;
        }

        var text = NameText(root.Name);
        return text.Length == 0 ? null : QualifiedName.Parse(text);
    }

    private static string NameText(NameSyntax name)
    {
        switch (name)
        {
            case IdentifierNameSyntax identifier:
                return identifier.Identifier.Text;
            case QualifiedNameSyntax qualified:
                var left = NameText(qualified.Left);
                var right = qualified.Right.Identifier.Text;
                return left.Length == 0 ? right : left + ":" + right;
            default:
                return string.Empty;
        }
    }
}
