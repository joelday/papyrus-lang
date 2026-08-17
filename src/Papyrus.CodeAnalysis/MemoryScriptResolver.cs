using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis;

public sealed class MemoryScriptResolver : ScriptResolver
{
    private readonly ImmutableDictionary<QualifiedName, SourceText> _sources;
    private readonly ImmutableDictionary<string, PexScriptMetadata> _pex;

    public MemoryScriptResolver(
        IEnumerable<KeyValuePair<QualifiedName, SourceText>>? sources = null,
        IEnumerable<KeyValuePair<string, PexScriptMetadata>>? pex = null)
    {
        _sources = sources is null
            ? ImmutableDictionary<QualifiedName, SourceText>.Empty
            : ImmutableDictionary.CreateRange(sources);
        _pex = pex is null
            ? ImmutableDictionary.Create<string, PexScriptMetadata>(StringComparer.OrdinalIgnoreCase)
            : ImmutableDictionary.CreateRange(StringComparer.OrdinalIgnoreCase, pex);
    }

    public override bool TryResolve(QualifiedName name, out SourceText text, out string displayPath)
    {
        if (_sources.TryGetKey(name, out var stored) && _sources.TryGetValue(stored, out text!))
        {
            displayPath = stored.ToRelativePath();
            return true;
        }

        text = null!;
        displayPath = string.Empty;
        return false;
    }

    public override bool TryResolvePex(string colonName, out PexScriptMetadata metadata)
    {
        if (!string.IsNullOrEmpty(colonName) && _pex.TryGetValue(colonName, out metadata!))
        {
            return true;
        }

        metadata = null!;
        return false;
    }
}
