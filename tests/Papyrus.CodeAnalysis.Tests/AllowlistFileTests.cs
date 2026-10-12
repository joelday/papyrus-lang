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
        Assert.Equal("RobotSelfDestructScript", Assert.Single(document.Entries[3].Scripts));
        Assert.True(document.ContainsScript("RobotSelfDestructScript"));
        Assert.True(document.ContainsId("parent-scope-params"));
        Assert.False(document.ContainsScript("Actor"));
        Assert.False(document.ContainsScript("None"));
        Assert.False(document.ContainsScript("Bool"));
        Assert.False(document.ContainsScript("String"));
        Assert.False(document.ContainsScript("Auto"));
        Assert.False(document.ContainsScript("Var"));
        Assert.False(document.ContainsScript("object"));
        Assert.False(document.ContainsScript("parent"));
        Assert.False(document.ContainsScript("read"));
        Assert.False(document.ContainsScript("scope"));
        Assert.False(document.ContainsScript("params"));
        Assert.False(document.ContainsScript("CK"));
        Assert.False(document.ContainsScript("FO4"));
        Assert.False(document.ContainsScript("frenzyFX"));
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
        Assert.Empty(document.Entries[0].Scripts);
    }

    [Fact]
    public void Allowlist_parse_scripts_line_is_explicit_not_prose()
    {
        var document = AllowlistFile.Parse(
            """
            1. `parent-scope-params` — Mentions None Bool String Auto in prose.
               - scripts: RobotSelfDestructScript, Other.psc
            """);

        Assert.Equal(1, document.Size);
        Assert.Equal("parent-scope-params", document.Entries[0].Id);
        Assert.Contains("Mentions None Bool String Auto in prose.", document.Entries[0].Justification, StringComparison.Ordinal);
        Assert.Equal(2, document.Entries[0].Scripts.Length);
        Assert.Equal("RobotSelfDestructScript", document.Entries[0].Scripts[0]);
        Assert.Equal("Other", document.Entries[0].Scripts[1]);
        Assert.True(document.ContainsScript("RobotSelfDestructScript"));
        Assert.True(document.ContainsScript("Other"));
        Assert.False(document.ContainsScript("None"));
        Assert.False(document.ContainsScript("Bool"));
        Assert.False(document.ContainsScript("String"));
        Assert.False(document.ContainsScript("Auto"));
    }

    [Fact]
    public void Allowlist_parse_crlf_keeps_scripts_line_with_its_entry()
    {
        var document = AllowlistFile.Parse(
            "1. `parent-scope-params` — Prose.\r\n   - scripts: RobotSelfDestructScript, Other.psc\r\n2. Second.\r\n");

        Assert.Equal(2, document.Size);
        Assert.Equal(2, document.Entries[0].Scripts.Length);
        Assert.True(document.ContainsScript("RobotSelfDestructScript"));
        Assert.Equal("Second.", document.Entries[1].Justification);
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
