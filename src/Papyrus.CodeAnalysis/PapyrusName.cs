using System;

namespace Papyrus.CodeAnalysis;

public readonly struct PapyrusName : IEquatable<PapyrusName>
{
    private static readonly int EmptyHash =
        string.GetHashCode(string.Empty, StringComparison.OrdinalIgnoreCase);

    private readonly string? _text;
    private readonly int _hash;

    public PapyrusName(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        _hash = string.GetHashCode(text, StringComparison.OrdinalIgnoreCase);
    }

    public static PapyrusName Empty { get; } = new(string.Empty);

    public string Text => _text ?? string.Empty;

    public int OrdinalIgnoreCaseHash => _text is null ? EmptyHash : _hash;

    // K14: hash-only equality silently aliases distinct names (ObjectIdentifier's bug).
    public bool Equals(PapyrusName other)
        => string.Equals(Text, other.Text, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is PapyrusName other && Equals(other);

    public override int GetHashCode() => OrdinalIgnoreCaseHash;

    public override string ToString() => Text;

    public static bool operator ==(PapyrusName left, PapyrusName right) => left.Equals(right);

    public static bool operator !=(PapyrusName left, PapyrusName right) => !left.Equals(right);
}
