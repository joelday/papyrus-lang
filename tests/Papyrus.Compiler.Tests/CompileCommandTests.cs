using System;
using System.IO;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.Compiler.Tests;

public sealed class CompileCommandTests
{
    [Fact]
    public void Help_MentionsCompile()
    {
        var code = Run(out var stdout, out var stderr, "--help");

        Assert.Equal(0, code);
        Assert.Contains("compile <script.psc>", stdout, StringComparison.Ordinal);
        Assert.Contains("--game", stdout, StringComparison.Ordinal);
        Assert.Contains("skyrimle", stdout, StringComparison.Ordinal);
        Assert.Empty(stderr);
    }

    [Fact]
    public void Compile_MissingPath_ExitsNonZero()
    {
        var code = Run(out _, out var stderr, "compile", "--game", "skyrimse");

        Assert.Equal(1, code);
        Assert.Contains("path", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_MissingGame_ExitsNonZero()
    {
        using var root = new TempDir();
        var script = Path.Combine(root.Path, "Foo.psc");
        File.WriteAllText(script, TinyScript());

        var code = Run(out _, out var stderr, "compile", script);

        Assert.Equal(1, code);
        Assert.Contains("--game must be one of", stderr, StringComparison.Ordinal);
        Assert.Contains("skyrimle", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_MissingFile_ExitsNonZero()
    {
        using var root = new TempDir();
        var missing = Path.Combine(root.Path, "nope.psc");

        var code = Run(out _, out var stderr, "compile", missing, "--game", "skyrimse");

        Assert.Equal(1, code);
        Assert.Contains("not found", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_TinyScript_WritesPexAndRoundTrips()
    {
        using var root = new TempDir();
        var script = Path.Combine(root.Path, "Foo.psc");
        File.WriteAllText(script, TinyScript());
        var pexPath = Path.Combine(root.Path, "Foo.pex");

        var code = Run(out var stdout, out var stderr, "compile", script, "--game", "skyrimse");

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.True(File.Exists(pexPath));
        Assert.Contains(pexPath, stdout, StringComparison.Ordinal);

        var bytes = File.ReadAllBytes(pexPath);
        var file = PexReader.Read(bytes);
        Assert.Equal(bytes, PexWriter.Write(file));
        Assert.Equal(PexVersion.V32, file.Version);
        Assert.Equal(GameId.SkyrimSpecialEdition, file.Game);
        Assert.Equal(DateTimeOffset.UnixEpoch, file.CompileTime);
        Assert.Equal("Foo", Assert.Single(file.Objects).Name);
    }

    [Fact]
    public void Compile_GameSkyrimLe_Accepted()
    {
        using var root = new TempDir();
        var script = Path.Combine(root.Path, "Foo.psc");
        File.WriteAllText(script, TinyScript());
        var output = Path.Combine(root.Path, "out", "Foo.pex");

        var code = Run(
            out _,
            out var stderr,
            "compile",
            script,
            "--game", "skyrimle",
            "--output", output,
            "--time", "0");

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.True(File.Exists(output));

        var bytes = File.ReadAllBytes(output);
        var file = PexReader.Read(bytes, GameId.Skyrim);
        Assert.Equal(bytes, PexWriter.Write(file));
        Assert.Equal(PexVersion.V31, file.Version);
        Assert.Equal(GameId.Skyrim, file.Game);
        Assert.Equal(DateTimeOffset.UnixEpoch, file.CompileTime);
    }

    [Fact]
    public void Compile_ErrorDiagnostics_ExitsNonZeroAndDoesNotWrite()
    {
        using var root = new TempDir();
        var script = Path.Combine(root.Path, "Foo.psc");
        File.WriteAllText(script, "ScriptName Foo extends MissingBase\nFunction Bar()\nEndFunction\n");
        var pexPath = Path.Combine(root.Path, "Foo.pex");

        var code = Run(out _, out var stderr, "compile", script, "--game", "skyrimse");

        Assert.Equal(1, code);
        Assert.Contains("PAP1001", stderr, StringComparison.Ordinal);
        Assert.False(File.Exists(pexPath));
    }

    private static string TinyScript()
    {
        return "ScriptName Foo\nFunction Bar()\nEndFunction\n";
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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "papyrusc-compile-" + Guid.NewGuid().ToString("N"));
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
