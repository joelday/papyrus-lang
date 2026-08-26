using System;
using System.IO;
using Papyrus.CodeAnalysis.Testing;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class AllowlistFileTests
{
    [Fact]
    public void Allowlist_committed_file_parses_four_ck_differences()
    {
        var path = FindAllowlistPath();
        var document = AllowlistFile.Load(path);

        Assert.Equal(4, document.Size);
        Assert.Equal("none-to-bool-string", document.Entries[0].Id);
        Assert.Equal("none-to-object", document.Entries[1].Id);
        Assert.Equal("parent-auto-read", document.Entries[2].Id);
        Assert.Equal("parent-scope-params", document.Entries[3].Id);
        Assert.All(document.Entries, static e => Assert.False(string.IsNullOrWhiteSpace(e.Justification)));
        Assert.True(document.ContainsScript("RobotSelfDestructScript"));
        Assert.True(document.ContainsId("parent-scope-params"));
        Assert.False(document.ContainsScript("Actor"));
    }

    [Fact]
    public void Allowlist_parse_empty_is_size_zero()
    {
        var document = AllowlistFile.Parse("# just a heading\n\nReviewed like code.\n");
        Assert.Equal(0, document.Size);
    }

    [Fact]
    public void Allowlist_parse_numbered_items_without_ids()
    {
        var document = AllowlistFile.Parse(
            """
            1. First construct.
            2. Second construct.
            """);

        Assert.Equal(2, document.Size);
        Assert.Equal("1", document.Entries[0].Id);
        Assert.Equal("First construct.", document.Entries[0].Justification);
        Assert.Equal("2", document.Entries[1].Id);
    }

    [Fact]
    public void Allowlist_find_default_path_from_output()
    {
        var path = AllowlistFile.FindDefaultPath(AppContext.BaseDirectory);
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.Equal(4, AllowlistFile.Load(path!).Size);
    }

    private static string FindAllowlistPath()
    {
        var copied = Path.Combine(AppContext.BaseDirectory, "compliance", "allowlist.md");
        if (File.Exists(copied))
            return copied;

        var found = AllowlistFile.FindDefaultPath(AppContext.BaseDirectory);
        if (found is not null)
            return found;

        throw new InvalidOperationException("docs/compliance/allowlist.md was not found.");
    }
}
