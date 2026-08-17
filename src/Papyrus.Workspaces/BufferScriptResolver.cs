using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;

namespace Papyrus.Workspaces;

public sealed class BufferScriptResolver : ScriptResolver
{
    private readonly ScriptResolver _inner;
    private readonly ImmutableDictionary<string, SourceText> _buffersByPath;
    private readonly ImmutableDictionary<QualifiedName, SourceText> _buffersByName;

    public BufferScriptResolver(ScriptResolver inner)
        : this(
            inner,
            ImmutableDictionary.Create<string, SourceText>(StringComparer.OrdinalIgnoreCase),
            ImmutableDictionary<QualifiedName, SourceText>.Empty)
    {
    }

    public BufferScriptResolver(
        ScriptResolver inner,
        IEnumerable<KeyValuePair<string, SourceText>>? pathBuffers,
        IEnumerable<KeyValuePair<QualifiedName, SourceText>>? nameBuffers = null)
        : this(
            inner,
            pathBuffers is null
                ? ImmutableDictionary.Create<string, SourceText>(StringComparer.OrdinalIgnoreCase)
                : ImmutableDictionary.CreateRange(StringComparer.OrdinalIgnoreCase, NormalizePathBuffers(pathBuffers)),
            nameBuffers is null
                ? ImmutableDictionary<QualifiedName, SourceText>.Empty
                : ImmutableDictionary.CreateRange(nameBuffers))
    {
    }

    private BufferScriptResolver(
        ScriptResolver inner,
        ImmutableDictionary<string, SourceText> buffersByPath,
        ImmutableDictionary<QualifiedName, SourceText> buffersByName)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _buffersByPath = buffersByPath;
        _buffersByName = buffersByName;
    }

    public BufferScriptResolver WithBuffer(string path, SourceText text)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(text);
        return new BufferScriptResolver(_inner, _buffersByPath.SetItem(NormalizePath(path), text), _buffersByName);
    }

    public BufferScriptResolver WithBuffer(QualifiedName name, SourceText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new BufferScriptResolver(_inner, _buffersByPath, _buffersByName.SetItem(name, text));
    }

    public override bool TryResolve(QualifiedName name, out SourceText text, out string displayPath)
    {
        if (_buffersByName.TryGetValue(name, out text!))
        {
            displayPath = name.ToRelativePath();
            return true;
        }

        if (_inner.TryResolve(name, out text, out displayPath))
        {
            if (_buffersByPath.TryGetValue(NormalizePath(displayPath), out var dirty))
            {
                text = dirty;
            }

            return true;
        }

        var relative = name.ToRelativePath();
        foreach (var pair in _buffersByPath)
        {
            if (PathEndsWith(pair.Key, relative))
            {
                text = pair.Value;
                displayPath = pair.Key;
                return true;
            }
        }

        text = null!;
        displayPath = string.Empty;
        return false;
    }

    public override bool TryResolvePex(string colonName, out PexScriptMetadata metadata)
        => _inner.TryResolvePex(colonName, out metadata);

    private static IEnumerable<KeyValuePair<string, SourceText>> NormalizePathBuffers(
        IEnumerable<KeyValuePair<string, SourceText>> buffers)
    {
        foreach (var pair in buffers)
        {
            yield return new KeyValuePair<string, SourceText>(NormalizePath(pair.Key), pair.Value);
        }
    }

    private static string NormalizePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }

        return path.Replace('\\', '/');
    }

    private static bool PathEndsWith(string path, string relative)
    {
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        var suffix = relative.Replace('\\', '/').TrimStart('/');
        if (!normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalized.Length == suffix.Length
            || normalized[normalized.Length - suffix.Length - 1] == '/';
    }
}
