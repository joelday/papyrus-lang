using System.Collections.Generic;
using System.IO;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;
using Papyrus.Workspaces;
using Xunit;

namespace Papyrus.Workspaces.Tests;

public sealed class BufferScriptResolverTests
{
    [Fact]
    public void Name_buffer_is_resolved_without_inner_source()
    {
        var inner = new MemoryScriptResolver();
        var resolver = new BufferScriptResolver(inner)
            .WithBuffer(QualifiedName.Parse("Foo"), SourceText.From("ScriptName Foo\n"));

        Assert.True(resolver.TryResolve(QualifiedName.Parse("Foo"), out var text, out var path));
        Assert.Equal("ScriptName Foo\n", text.ToString());
        Assert.Equal("Foo.psc", path);
    }

    [Fact]
    public void Path_buffer_shadows_inner_file()
    {
        var name = QualifiedName.Parse("Foo");
        var inner = new MemoryScriptResolver(new Dictionary<QualifiedName, SourceText>
        {
            [name] = SourceText.From("ScriptName Disk\n"),
        });
        var resolver = new BufferScriptResolver(inner)
            .WithBuffer("Foo.psc", SourceText.From("ScriptName Dirty\n"));

        Assert.True(resolver.TryResolve(name, out var text, out _));
        Assert.Equal("ScriptName Dirty\n", text.ToString());
    }

    [Fact]
    public void Path_buffer_matches_exact_relative_path_only()
    {
        var resolver = new BufferScriptResolver(new MemoryScriptResolver())
            .WithBuffer("A/B/Script.psc", SourceText.From("ScriptName A:B:Script\n"));

        Assert.True(resolver.TryResolve(QualifiedName.Parse("A:B:Script"), out var text, out var path));
        Assert.Equal("ScriptName A:B:Script\n", text.ToString());
        Assert.Equal("A/B/Script.psc", path.Replace('\\', '/'));
        Assert.False(resolver.TryResolve(QualifiedName.Parse("B:Script"), out _, out _));
        Assert.False(resolver.TryResolve(QualifiedName.Parse("Script"), out _, out _));
    }

    [Fact]
    public void Path_buffer_matches_include_candidate_when_disk_is_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), "papyrus-buffer-missing-" + Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        try
        {
            var unsaved = Path.Combine(root, "A", "B", "Script.psc");
            var resolver = new BufferScriptResolver(
                    new FileSystemScriptResolver([root], game: GameId.Fallout4))
                .WithBuffer(unsaved, SourceText.From("ScriptName A:B:Script\n"));

            Assert.True(resolver.TryResolve(QualifiedName.Parse("A:B:Script"), out var text, out _));
            Assert.Equal("ScriptName A:B:Script\n", text.ToString());
            Assert.False(resolver.TryResolve(QualifiedName.Parse("Script"), out _, out _));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void TryResolvePex_delegates_to_inner()
    {
        var metadata = new PexScriptMetadata { Name = new PexQualifiedName("Bar") };
        var resolver = new BufferScriptResolver(
            new MemoryScriptResolver(pex: new Dictionary<string, PexScriptMetadata> { ["Bar"] = metadata }));

        Assert.True(resolver.TryResolvePex("Bar", out var resolved));
        Assert.Same(metadata, resolved);
        Assert.False(resolver.TryResolvePex("Missing", out _));
    }

    [Fact]
    public void WithBuffer_does_not_mutate_original()
    {
        var original = new BufferScriptResolver(new MemoryScriptResolver());
        var updated = original.WithBuffer(QualifiedName.Parse("Foo"), SourceText.From("ScriptName Foo\n"));

        Assert.False(original.TryResolve(QualifiedName.Parse("Foo"), out _, out _));
        Assert.True(updated.TryResolve(QualifiedName.Parse("Foo"), out _, out _));
    }

    [Fact]
    public void Overlay_shadows_filesystem_at_resolved_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "papyrus-buffer-" + Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        try
        {
            var diskPath = Path.Combine(root, "Foo.psc");
            File.WriteAllText(diskPath, "ScriptName Disk\n");
            var resolver = new BufferScriptResolver(new FileSystemScriptResolver([root]))
                .WithBuffer(diskPath, SourceText.From("ScriptName Buffer\n"));

            Assert.True(resolver.TryResolve(QualifiedName.Parse("Foo"), out var text, out _));
            Assert.Equal("ScriptName Buffer\n", text.ToString());
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
