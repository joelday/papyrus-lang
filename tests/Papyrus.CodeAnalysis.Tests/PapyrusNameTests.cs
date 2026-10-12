using System;
using System.Collections.Generic;
using Papyrus.CodeAnalysis;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class PapyrusNameTests
{
    [Fact]
    public void Foo_equals_foo()
    {
        var foo = new PapyrusName("Foo");
        var lower = new PapyrusName("foo");

        Assert.Equal(foo, lower);
        Assert.True(foo == lower);
        Assert.False(foo != lower);
        Assert.Equal(foo.GetHashCode(), lower.GetHashCode());
        Assert.Equal("Foo", foo.Text);
        Assert.Equal("foo", lower.Text);
    }

    [Fact]
    public void Distinct_names_are_not_equal()
    {
        var foo = new PapyrusName("Foo");
        var bar = new PapyrusName("Bar");

        Assert.NotEqual(foo, bar);
        Assert.True(foo != bar);
    }

    [Fact]
    public void Hash_collision_that_is_not_string_equal_is_not_equal()
    {
        var (leftText, rightText) = FindOrdinalIgnoreCaseHashCollision();
        var left = new PapyrusName(leftText);
        var right = new PapyrusName(rightText);

        Assert.Equal(left.OrdinalIgnoreCaseHash, right.OrdinalIgnoreCaseHash);
        Assert.False(string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase));
        // ObjectIdentifier compares GetHashCode() only and would treat these as equal.
        Assert.NotEqual(left, right);
        Assert.False(left == right);
        Assert.True(left != right);
    }

    [Fact]
    public void QualifiedName_parses_namespace_script()
    {
        var name = QualifiedName.Parse("A:B:Script");

        Assert.Equal(3, name.Parts.Length);
        Assert.Equal("A", name.Parts[0].Text);
        Assert.Equal("B", name.Parts[1].Text);
        Assert.Equal("Script", name.Parts[2].Text);
        Assert.Equal("A:B:Script", name.ToString());
        Assert.Equal("A/B/Script.psc", name.ToRelativePath());
    }

    [Fact]
    public void QualifiedName_equals_is_case_insensitive()
    {
        var left = QualifiedName.Parse("A:B:Script");
        var right = QualifiedName.Parse("a:b:script");

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.NotEqual(left, QualifiedName.Parse("A:B:Other"));
    }

    [Fact]
    public void TypeName_formats_struct_and_array()
    {
        var type = TypeName.Parse("A:B:Script#Struct[]");

        Assert.Equal("A:B:Script", type.Name.ToString());
        Assert.True(type.IsArray);
        Assert.Equal("Struct", type.StructName?.Text);
        Assert.Equal("A:B:Script#Struct[]", type.ToString());
        Assert.Equal(TypeName.Parse("a:b:script#struct[]"), type);
    }

    [Fact]
    public void NameTable_interns_ignore_case_and_keeps_first_casing()
    {
        var names = new NameTable();
        var first = names.Get("Foo");
        var second = names.Get("foo".AsSpan());

        Assert.Equal(first, second);
        Assert.Same(first.Text, second.Text);
        Assert.Equal("Foo", second.Text);
    }

    private static (string Left, string Right) FindOrdinalIgnoreCaseHashCollision()
    {
        var seen = new Dictionary<int, string>();
        for (var i = 0; i < 400_000; i++)
        {
            var candidate = "N" + i.ToString("X");
            var hash = string.GetHashCode(candidate, StringComparison.OrdinalIgnoreCase);
            if (seen.TryGetValue(hash, out var existing)
                && !string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return (existing, candidate);
            }

            seen.TryAdd(hash, candidate);
        }

        throw new InvalidOperationException("Failed to find an OrdinalIgnoreCase hash collision.");
    }
}
