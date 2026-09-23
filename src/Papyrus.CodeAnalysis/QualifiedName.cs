using System;
using System.Collections.Immutable;

namespace Papyrus.CodeAnalysis;

public readonly struct QualifiedName : IEquatable<QualifiedName>
{
    public QualifiedName(ImmutableArray<PapyrusName> parts)
    {
        Parts = parts.IsDefault ? ImmutableArray<PapyrusName>.Empty : parts;
    }

    public QualifiedName(params PapyrusName[] parts)
        : this(parts is null ? ImmutableArray<PapyrusName>.Empty : ImmutableArray.Create(parts))
    {
    }

    public ImmutableArray<PapyrusName> Parts { get; }

    public static QualifiedName Parse(ReadOnlySpan<char> text, NameTable? names = null)
    {
        if (text.IsEmpty)
        {
            return new QualifiedName(ImmutableArray<PapyrusName>.Empty);
        }

        var builder = ImmutableArray.CreateBuilder<PapyrusName>();
        while (true)
        {
            var colon = text.IndexOf(':');
            var part = colon < 0 ? text : text[..colon];
            builder.Add(names is null ? new PapyrusName(part.ToString()) : names.Get(part));
            if (colon < 0)
            {
                break;
            }

            text = text[(colon + 1)..];
        }

        return new QualifiedName(builder.ToImmutable());
    }

    public override string ToString() => JoinParts(':');

    // Forward slashes: CodeAnalysis has no I/O and must not use OS separators.
    public string ToRelativePath() => JoinParts('/') + ".psc";

    public bool Equals(QualifiedName other)
    {
        var parts = Parts.IsDefault ? ImmutableArray<PapyrusName>.Empty : Parts;
        var otherParts = other.Parts.IsDefault ? ImmutableArray<PapyrusName>.Empty : other.Parts;
        if (parts.Length != otherParts.Length)
        {
            return false;
        }

        for (var i = 0; i < parts.Length; i++)
        {
            if (!parts[i].Equals(otherParts[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is QualifiedName other && Equals(other);

    public override int GetHashCode()
    {
        var parts = Parts.IsDefault ? ImmutableArray<PapyrusName>.Empty : Parts;
        var hash = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            hash = (hash * 31) + parts[i].GetHashCode();
        }

        return hash;
    }

    public static bool operator ==(QualifiedName left, QualifiedName right) => left.Equals(right);

    public static bool operator !=(QualifiedName left, QualifiedName right) => !left.Equals(right);

    private string JoinParts(char separator)
    {
        var parts = Parts.IsDefault ? ImmutableArray<PapyrusName>.Empty : Parts;
        if (parts.IsEmpty)
        {
            return string.Empty;
        }

        if (parts.Length == 1)
        {
            return parts[0].Text;
        }

        var texts = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            texts[i] = parts[i].Text;
        }

        return string.Join(separator, texts);
    }
}
