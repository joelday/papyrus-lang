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

internal readonly struct ChildSlot : IEquatable<ChildSlot>
{
    public ChildSlot(string name, string typeName, bool isToken, bool isOptional)
    {
        Name = name;
        TypeName = typeName;
        IsToken = isToken;
        IsOptional = isOptional;
    }

    public string Name { get; }

    public string TypeName { get; }

    public bool IsToken { get; }

    public bool IsOptional { get; }

    public bool Equals(ChildSlot other)
    {
        return string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(TypeName, other.TypeName, StringComparison.Ordinal)
            && IsToken == other.IsToken
            && IsOptional == other.IsOptional;
    }

    public override bool Equals(object? obj) => obj is ChildSlot other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Name);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(TypeName);
            hash = (hash * 31) + (IsToken ? 1 : 0);
            hash = (hash * 31) + (IsOptional ? 1 : 0);
            return hash;
        }
    }
}

internal readonly struct KindModel : IEquatable<KindModel>
{
    public KindModel(
        string name,
        KindCategory category,
        string? text,
        ImmutableArray<ChildSlot> children,
        string typeName = "",
        string baseTypeName = "")
    {
        Name = name;
        Category = category;
        Text = text;
        Children = children;
        TypeName = typeName;
        BaseTypeName = baseTypeName;
    }

    public string Name { get; }

    public KindCategory Category { get; }

    public string? Text { get; }

    public ImmutableArray<ChildSlot> Children { get; }

    public string TypeName { get; }

    public string BaseTypeName { get; }

    public bool Equals(KindModel other)
    {
        if (!string.Equals(Name, other.Name, StringComparison.Ordinal)
            || Category != other.Category
            || !string.Equals(Text, other.Text, StringComparison.Ordinal)
            || !string.Equals(TypeName, other.TypeName, StringComparison.Ordinal)
            || !string.Equals(BaseTypeName, other.BaseTypeName, StringComparison.Ordinal)
            || Children.Length != other.Children.Length)
        {
            return false;
        }

        for (var i = 0; i < Children.Length; i++)
        {
            if (!Children[i].Equals(other.Children[i]))
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
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(TypeName);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(BaseTypeName);
            for (var i = 0; i < Children.Length; i++)
            {
                hash = (hash * 31) + Children[i].GetHashCode();
            }

            return hash;
        }
    }
}
