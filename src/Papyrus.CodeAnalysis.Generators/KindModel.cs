using System;
using System.Collections.Immutable;

namespace Papyrus.CodeAnalysis.Generators;

internal enum KindCategory
{
    Trivia,
    Token,
    Keyword,
    Node,
}

internal readonly struct KindModel : IEquatable<KindModel>
{
    public KindModel(string name, KindCategory category, string? text, ImmutableArray<string> children)
    {
        Name = name;
        Category = category;
        Text = text;
        Children = children;
    }

    public string Name { get; }

    public KindCategory Category { get; }

    public string? Text { get; }

    public ImmutableArray<string> Children { get; }

    public bool Equals(KindModel other)
    {
        if (!string.Equals(Name, other.Name, StringComparison.Ordinal)
            || Category != other.Category
            || !string.Equals(Text, other.Text, StringComparison.Ordinal)
            || Children.Length != other.Children.Length)
        {
            return false;
        }

        for (var i = 0; i < Children.Length; i++)
        {
            if (!string.Equals(Children[i], other.Children[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is KindModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Name);
            hash = (hash * 31) + (int)Category;
            hash = (hash * 31) + (Text is null ? 0 : StringComparer.Ordinal.GetHashCode(Text));
            for (var i = 0; i < Children.Length; i++)
            {
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Children[i]);
            }

            return hash;
        }
    }
}
