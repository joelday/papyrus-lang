using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Papyrus.Architecture.Tests;

public sealed class EmptyCatchTests
{
    [Fact]
    public void CodeAnalysis_must_not_contain_empty_or_default_returning_catch()
        => AssertNoEmptyCatch("src", "Papyrus.CodeAnalysis");

    [Fact]
    public void Pex_must_not_contain_empty_or_default_returning_catch()
        => AssertNoEmptyCatch("src", "Papyrus.Pex");

    private static void AssertNoEmptyCatch(params string[] relativePath)
    {
        var directory = Path.Combine(new[] { RepoRoot.Find() }.Concat(relativePath).ToArray());
        var violations = EmptyCatchScanner.ScanProject(directory);
        if (violations.Count == 0)
        {
            return;
        }

        Assert.Fail(string.Join(Environment.NewLine, violations));
    }
}
