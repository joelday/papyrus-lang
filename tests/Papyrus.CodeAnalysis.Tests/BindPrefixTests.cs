using System;
using System.Collections.Generic;
using System.Threading;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Binding;
using Papyrus.CodeAnalysis.Testing;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class BindPrefixTests
{
    private const int LargeFileStride = 4;
    private const int LargeFileThreshold = 200;

    [Fact(Timeout = 30_000)]
    public void Prefix_binds_without_throw_or_hang()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        foreach (var fixture in ParseFixtureCatalog.Load(ParseFixtureCatalog.FindDirectory()))
        {
            foreach (var length in PrefixLengths(fixture.Source.Length))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AssertBindDoesNotThrow(
                    fixture.Source.Substring(0, length),
                    fixture.Dialect,
                    fixture.Name + " prefix length " + length,
                    cancellationToken);
            }
        }
    }

    private static IEnumerable<int> PrefixLengths(int sourceLength)
    {
        if (sourceLength <= LargeFileThreshold)
        {
            for (var length = 0; length <= sourceLength; length++)
            {
                yield return length;
            }

            yield break;
        }

        for (var length = 0; length <= sourceLength; length += LargeFileStride)
        {
            yield return length;
        }

        if (sourceLength % LargeFileStride != 0)
        {
            yield return sourceLength;
        }
    }

    private static void AssertBindDoesNotThrow(
        string source,
        PapyrusDialect dialect,
        string label,
        CancellationToken cancellationToken)
    {
        try
        {
            var name = QualifiedName.Parse("BindPrefix");
            var compilation = PapyrusCompilation.Create(
                dialect,
                new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
                {
                    [name] = SourceText.From(source),
                }));

            var script = compilation.GetScript(name);
            _ = compilation.GetDiagnostics(cancellationToken);
            _ = Binder.BindScript(script);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Assert.Fail(label + " threw " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
