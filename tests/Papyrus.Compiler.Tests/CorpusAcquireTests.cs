using System;
using System.IO;
using Xunit;

namespace Papyrus.Compiler.Tests;

public sealed class CorpusAcquireTests
{
    [Fact]
    public void Help_PrintsUsageAndExitsZero()
    {
        var code = Run(out var stdout, out var stderr, "--help");

        Assert.Equal(0, code);
        Assert.Contains("corpus acquire", stdout, StringComparison.Ordinal);
        Assert.Contains("--source", stdout, StringComparison.Ordinal);
        Assert.Empty(stderr);
    }

    [Fact]
    public void MissingSourceDirectory_ExitsNonZero()
    {
        using var root = new TempDir();
        var missing = Path.Combine(root.Path, "nope");
        var dest = Path.Combine(root.Path, "out");

        var code = Run(
            out _,
            out var stderr,
            "corpus", "acquire",
            "--game", "skyrimse",
            "--source", missing,
            "--dest", dest);

        Assert.Equal(1, code);
        Assert.Contains("source not found", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSourceOption_ExitsNonZero()
    {
        var code = Run(out _, out var stderr, "corpus", "acquire", "--game", "fallout4");

        Assert.Equal(1, code);
        Assert.Contains("--source is required", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidGame_ExitsNonZero()
    {
        var code = Run(
            out _,
            out var stderr,
            "corpus", "acquire",
            "--game", "skyrimle",
            "--source", ".");

        Assert.Equal(1, code);
        Assert.Contains("--game must be one of", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void DestInsideWorktree_WithoutForce_ExitsNonZero()
    {
        using var root = new TempDir();
        Directory.CreateDirectory(Path.Combine(root.Path, ".git"));
        var source = Path.Combine(root.Path, "scripts");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Actor.psc"), "scriptname Actor");
        var dest = Path.Combine(root.Path, "corpus", "skyrimse");

        var code = Run(
            out _,
            out var stderr,
            "corpus", "acquire",
            "--game", "skyrimse",
            "--source", source,
            "--dest", dest);

        Assert.Equal(1, code);
        Assert.Contains("git worktree", stderr, StringComparison.Ordinal);
        Assert.False(Directory.Exists(dest));
    }

    [Fact]
    public void DestSymlinkIntoWorktreeSubdir_WithoutForce_ExitsNonZero()
    {
        using var outside = new TempDir();
        using var worktree = new TempDir();
        Directory.CreateDirectory(Path.Combine(worktree.Path, ".git"));
        var target = Path.Combine(worktree.Path, "corpus", "skyrimse");
        Directory.CreateDirectory(target);

        var source = Path.Combine(outside.Path, "scripts");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Actor.psc"), "scriptname Actor");

        var destLink = Path.Combine(outside.Path, "link-dest");
        if (!TryCreateDirectorySymlink(destLink, target))
        {
            return;
        }

        var code = Run(
            out _,
            out var stderr,
            "corpus", "acquire",
            "--game", "skyrimse",
            "--source", source,
            "--dest", destLink);

        Assert.Equal(1, code);
        Assert.Contains("git worktree", stderr, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(target, "Actor.psc")));
    }

    [Fact]
    public void DestDanglingSymlinkIntoWorktreeSubdir_WithoutForce_ExitsNonZero()
    {
        using var outside = new TempDir();
        using var worktree = new TempDir();
        Directory.CreateDirectory(Path.Combine(worktree.Path, ".git"));
        var target = Path.Combine(worktree.Path, "corpus", "skyrimse");

        var source = Path.Combine(outside.Path, "scripts");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Actor.psc"), "scriptname Actor");

        var destLink = Path.Combine(outside.Path, "link-dest");
        if (!TryCreateDirectorySymlink(destLink, target))
        {
            return;
        }

        var code = Run(
            out _,
            out var stderr,
            "corpus", "acquire",
            "--game", "skyrimse",
            "--source", source,
            "--dest", destLink);

        Assert.Equal(1, code);
        Assert.Contains("git worktree", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void DestInsideWorktree_WithForce_CopiesScripts()
    {
        using var root = new TempDir();
        Directory.CreateDirectory(Path.Combine(root.Path, ".git"));
        var source = Path.Combine(root.Path, "Data", "Scripts");
        Directory.CreateDirectory(Path.Combine(source, "Source"));
        File.WriteAllText(Path.Combine(source, "Actor.pex"), "pex");
        File.WriteAllText(Path.Combine(source, "Source", "Actor.psc"), "scriptname Actor");
        File.WriteAllText(Path.Combine(source, "readme.txt"), "ignore");
        var dest = Path.Combine(root.Path, "corpus", "skyrimse");

        var code = Run(
            out var stdout,
            out var stderr,
            "corpus", "acquire",
            "--game", "SkyrimSE",
            "--source", source,
            "--dest", dest,
            "--force");

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.Contains("2 files", stdout, StringComparison.Ordinal);
        Assert.Contains("1 .psc", stdout, StringComparison.Ordinal);
        Assert.Contains("1 .pex", stdout, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(dest, "Actor.pex")));
        Assert.True(File.Exists(Path.Combine(dest, "Source", "Actor.psc")));
        Assert.False(File.Exists(Path.Combine(dest, "readme.txt")));
    }

    [Fact]
    public void DefaultDest_IsCorpusGameUnderCwd()
    {
        using var root = new TempDir();
        var source = Path.Combine(root.Path, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Quest.PSC"), "scriptname Quest");
        var cwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(root.Path);
            var code = Run(
                out var stdout,
                out var stderr,
                "corpus", "acquire",
                "--game", "starfield",
                "--source", source);

            Assert.Equal(0, code);
            Assert.Empty(stderr);
            Assert.True(File.Exists(Path.Combine(root.Path, "corpus", "starfield", "Quest.PSC")));
            Assert.Contains(Path.Combine(root.Path, "corpus", "starfield"), stdout, StringComparison.Ordinal);
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
        }
    }

    [Fact]
    public void UnknownCommand_ExitsNonZero()
    {
        var code = Run(out _, out var stderr, "compile");

        Assert.Equal(1, code);
        Assert.Contains("unknown command", stderr, StringComparison.Ordinal);
    }

    private static bool TryCreateDirectorySymlink(string path, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(path, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static int Run(out string stdout, out string stderr, params string[] args)
    {
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var code = Cli.Run(args, outWriter, errWriter);
        stdout = outWriter.ToString();
        stderr = errWriter.ToString();
        return code;
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "papyrusc-corpus-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
