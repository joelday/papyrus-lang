using System;

namespace Papyrus.CodeAnalysis;

public readonly struct TypeName : IEquatable<TypeName>
{
    public TypeName(QualifiedName name, bool isArray = false, PapyrusName? structName = null)
    {
        Name = name;
        IsArray = isArray;
        StructName = structName;
    }

    public QualifiedName Name { get; }

    public bool IsArray { get; }

    public PapyrusName? StructName { get; }

    public static TypeName Parse(ReadOnlySpan<char> text, NameTable? names = null)
    {
        var isArray = text.EndsWith("[]", StringComparison.Ordinal);
        if (isArray)
        {
            text = text[..^2];
        }

        PapyrusName? structName = null;
        var hash = text.LastIndexOf('#');
        if (hash >= 0)
        {
            var structText = text[(hash + 1)..];
            structName = names is null ? new PapyrusName(structText.ToString()) : names.Get(structText);
            text = text[..hash];
        }

        return new TypeName(QualifiedName.Parse(text, names), isArray, structName);
    }

    public override string ToString()
    {
        var name = Name.ToString();
        if (StructName is { } structName)
        {
            name = name + "#" + structName.Text;
        }

        if (IsArray)
        {
            name += "[]";
        }

        return name;
    }

    public bool Equals(TypeName other)
    {
        return Name.Equals(other.Name)
            && IsArray == other.IsArray
            && Nullable.Equals(StructName, other.StructName);
    }

    public override bool Equals(object? obj) => obj is TypeName other && Equals(other);

    public override int GetHashCode()
    {
        var hash = Name.GetHashCode();
        hash = (hash * 31) + (IsArray ? 1 : 0);
        hash = (hash * 31) + (StructName?.GetHashCode() ?? 0);
        return hash;
    }

    public static bool operator ==(TypeName left, TypeName right) => left.Equals(right);

    public static bool operator !=(TypeName left, TypeName right) => !left.Equals(right);
}
