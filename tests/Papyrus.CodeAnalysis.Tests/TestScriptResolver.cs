using System.Collections.Generic;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis.Tests;

internal sealed class TestScriptResolver : ScriptResolver
{
    private readonly MemoryScriptResolver _inner;

    public TestScriptResolver(
        IEnumerable<KeyValuePair<QualifiedName, SourceText>>? sources = null,
        IEnumerable<KeyValuePair<string, PexScriptMetadata>>? pex = null)
    {
        _inner = new MemoryScriptResolver(sources, pex);
    }

    public override bool TryResolve(QualifiedName name, out SourceText text, out string displayPath)
        => _inner.TryResolve(name, out text, out displayPath);

    public override bool TryResolvePex(string colonName, out PexScriptMetadata metadata)
        => _inner.TryResolvePex(colonName, out metadata);
}

