using System.Collections.Generic;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class MemoryScriptResolverTests
{
    [Fact]
    public void TryResolve_is_case_insensitive_on_qualified_name()
    {
        var resolver = new MemoryScriptResolver(new Dictionary<QualifiedName, SourceText>
        {
            [QualifiedName.Parse("Foo")] = SourceText.From("ScriptName Foo\n"),
        });

        Assert.True(resolver.TryResolve(QualifiedName.Parse("foo"), out var text, out var path));
        Assert.Equal("ScriptName Foo\n", text.ToString());
        Assert.Equal("Foo.psc", path);
    }

    [Fact]
    public void TryResolvePex_looks_up_colon_name()
    {
        var metadata = new PexScriptMetadata { Name = new PexQualifiedName("A:B:Script") };
        var resolver = new MemoryScriptResolver(
            pex: new Dictionary<string, PexScriptMetadata> { ["A:B:Script"] = metadata });

        Assert.True(resolver.TryResolvePex("a:b:script", out var resolved));
        Assert.Same(metadata, resolved);
        Assert.False(resolver.TryResolvePex("Missing", out _));
        Assert.False(resolver.TryResolvePex(null!, out _));
    }
}
