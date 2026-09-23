using System;

namespace Papyrus.CodeAnalysis.Text;

public readonly struct TextSpan : IEquatable<TextSpan>
{
    public TextSpan(int start, int length)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Start = start;
        Length = length;
    }

    public int Start { get; }

    public int Length { get; }

    public int End => Start + Length;

    public bool IsEmpty => Length == 0;

    public bool Equals(TextSpan other) => Start == other.Start && Length == other.Length;

    public override bool Equals(object? obj) => obj is TextSpan other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Start, Length);

    public override string ToString() => $"[{Start}..{End})";

    public static bool operator ==(TextSpan left, TextSpan right) => left.Equals(right);

    public static bool operator !=(TextSpan left, TextSpan right) => !left.Equals(right);
}
