using System;
using System.IO;
using Papyrus.Workspaces;
using Xunit;

namespace Papyrus.Workspaces.Tests;

public sealed class ProjectDiscoveryTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "papyrus-discovery-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Finds_nested_projects_sorted_by_path()
    {
        var top = Touch("top.ppj");
        var nested = Touch(Path.Combine("mods", "b", "B.PPJ"));
        var sibling = Touch(Path.Combine("mods", "a", "a.ppj"));
        Touch(Path.Combine("mods", "a", "Script.psc"));

        var found = ProjectDiscovery.FindProjectFiles(_root);

        Assert.Equal([sibling, nested, top], found);
    }

    [Theory]
    [InlineData(".git")]
    [InlineData(".vscode")]
    [InlineData("node_modules")]
    [InlineData("bin")]
    [InlineData("OBJ")]
    public void Skips_hidden_and_build_directories(string directory)
    {
        Touch(Path.Combine(directory, "nested", "skipped.ppj"));

        Assert.Empty(ProjectDiscovery.FindProjectFiles(_root));
    }

    [Fact]
    public void Missing_root_yields_nothing()
    {
        Assert.Empty(ProjectDiscovery.FindProjectFiles(Path.Combine(_root, "missing")));
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("a/b/c.ppj", true)]
    [InlineData("ab/c.ppj", false)]
    [InlineData("", true)]
    public void IsUnderRoot_matches_whole_path_segments(string relative, bool expected)
    {
        var root = Path.Combine(_root, "a");
        var path = relative.Length == 0 ? root : Path.Combine(_root, relative);

        Assert.Equal(expected, ProjectDiscovery.IsUnderRoot(path, root + Path.DirectorySeparatorChar));
    }

    private string Touch(string relative)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<PapyrusProject />");
        return path;
    }
}
