using System;
using System.IO;
using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Papyrus.Architecture.Tests;

public sealed class PexDependencyTests
{
    [Fact]
    public void Pex_must_not_reference_code_analysis()
    {
        var assembly = Assembly.Load("Papyrus.Pex");

        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn("Papyrus.CodeAnalysis")
            .GetResult();

        if (!result.IsSuccessful)
        {
            var failing = result.FailingTypeNames ?? Array.Empty<string>();
            Assert.Fail(
                "Papyrus.Pex types depend on Papyrus.CodeAnalysis."
                + Environment.NewLine
                + string.Join(Environment.NewLine, failing));
        }

        Assert.DoesNotContain(
            assembly.GetReferencedAssemblies(),
            static name => string.Equals(name.Name, "Papyrus.CodeAnalysis", StringComparison.Ordinal));

        var projectPath = Path.Combine(RepoRoot.Find(), "src", "Papyrus.Pex", "Papyrus.Pex.csproj");
        var project = File.ReadAllText(projectPath);
        Assert.DoesNotContain("Papyrus.CodeAnalysis", project, StringComparison.Ordinal);
    }
}
