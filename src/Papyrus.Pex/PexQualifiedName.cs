using System;

namespace Papyrus.Pex;

/// <summary>
/// PEX-owned name. Colon-qualified script text plus an optional <c>#Struct</c>
/// suffix. Not <c>Papyrus.CodeAnalysis</c> <c>QualifiedName</c>.
/// </summary>
public readonly struct PexQualifiedName : IEquatable<PexQualifiedName>
{
    private readonly string? _text;
    private readonly string? _structName;

    public PexQualifiedName(string text, string? structName = null)
    {
        _text = text ?? "";
        _structName = string.IsNullOrEmpty(structName) ? null : structName;
    }

    /// <summary>Script name, e.g. <c>A:B:Script</c> or <c>Script</c>.</summary>
    public string Text => _text ?? "";

    /// <summary><c>Struct</c> when the source was <c>Script#Struct</c>.</summary>
    public string? StructName => _structName;

    /// <summary>
    /// Splits the first <c>#</c> into <see cref="Text"/> and <see cref="StructName"/>.
    /// </summary>
    public static PexQualifiedName Parse(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return default;

        int separator = value.IndexOf('#');
        if (separator < 0)
            return new PexQualifiedName(value);

        return new PexQualifiedName(value[..separator], value[(separator + 1)..]);
    }

    public override string ToString() =>
        StructName is null ? Text : $"{Text}#{StructName}";

    public bool Equals(PexQualifiedName other) =>
        string.Equals(Text, other.Text, StringComparison.Ordinal)
        && string.Equals(StructName, other.StructName, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is PexQualifiedName other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Text, StructName);

    public static bool operator ==(PexQualifiedName left, PexQualifiedName right) => left.Equals(right);

    public static bool operator !=(PexQualifiedName left, PexQualifiedName right) => !left.Equals(right);
}
