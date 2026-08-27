using System;
using System.IO;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.Compiler.Tests;

public sealed class ChampollionCommandTests
{
    [Fact]
    public void Help_MentionsChampollionVerifyAndCompat()
    {
        var code = Run(out var stdout, out var stderr, "--help");

        Assert.Equal(0, code);
        Assert.Contains("verify --champollion", stdout, StringComparison.Ordinal);
        Assert.Contains("--champollion-compat", stdout, StringComparison.Ordinal);
        Assert.Contains("PAPYRUSC_CHAMPOLLION", stdout, StringComparison.Ordinal);
        Assert.Contains("tools/Champollion", stdout, StringComparison.Ordinal);
        Assert.Contains("Skips (exit 0)", stdout, StringComparison.Ordinal);
        Assert.Empty(stderr);
    }

    [Fact]
    public void Verify_Champollion_WithoutBaseline_IsAllowed()
    {
        using var root = new TempDir();

        var code = Run(out var stdout, out var stderr, "verify", "--champollion", "--source", root.Path);

        Assert.Equal(0, code);
        Assert.Contains("skip:", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("--baseline is required", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_Champollion_MissingSource_SkipsZero()
    {
        using var root = new TempDir();
        var missing = Path.Combine(root.Path, "no-corpus");

        var code = Run(out var stdout, out var stderr, "verify", "--champollion", "--source", missing);

        Assert.Equal(0, code);
        Assert.Contains("skip: source not found", stdout, StringComparison.Ordinal);
        Assert.Empty(stderr);
    }

    [Fact]
    public void Verify_Champollion_EmptyDirectory_SkipsZero()
    {
        using var root = new TempDir();

        var code = Run(out var stdout, out var stderr, "verify", "--champollion", "--source", root.Path);

        Assert.Equal(0, code);
        Assert.Contains("skip: no .pex files", stdout, StringComparison.Ordinal);
        Assert.Empty(stderr);
    }

    [Fact]
    public void Verify_Champollion_MissingBinary_SkipsZero()
    {
        using var root = new TempDir();
        File.WriteAllBytes(Path.Combine(root.Path, "Foo.pex"), EmptySkyrimPex());
        var missingBinary = Path.Combine(root.Path, "no-such-champollion");

        var code = Run(
            out var stdout,
            out var stderr,
            "verify",
            "--champollion",
            "--source", root.Path,
            "--game", "skyrimse",
            "--champollion-path", missingBinary);

        Assert.Equal(0, code);
        Assert.Contains("skip: Champollion binary not found", stdout, StringComparison.Ordinal);
        Assert.Empty(stderr);
    }

    [Fact]
    public void Verify_Champollion_WithWriteBaselines_ExitsNonZero()
    {
        using var root = new TempDir();

        var code = Run(
            out _,
            out var stderr,
            "verify",
            "--champollion",
            "--write-baselines",
            "--source", root.Path,
            "--out", Path.Combine(root.Path, "out.json"),
            "--attestation", Path.Combine(root.Path, "note.txt"),
            "--game", "skyrimse");

        Assert.Equal(1, code);
        Assert.Contains("--champollion cannot be used with --write-baselines", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_ChampollionCompat_AcceptsTempIdentifier()
    {
        using var root = new TempDir();
        var script = Path.Combine(root.Path, "Foo.psc");
        File.WriteAllText(
            script,
            "ScriptName Foo\nFunction Bar()\n  Int ::temp0 = 1\n  Return ::temp0\nEndFunction\n");

        var without = Run(out _, out var withoutErr, "compile", script, "--game", "skyrimse");
        Assert.Equal(1, without);
        Assert.False(string.IsNullOrWhiteSpace(withoutErr));

        var with = Run(
            out var stdout,
            out var stderr,
            "compile",
            script,
            "--game", "skyrimse",
            "--champollion-compat");

        Assert.Equal(0, with);
        Assert.Empty(stderr);
        Assert.True(File.Exists(Path.Combine(root.Path, "Foo.pex")));
        Assert.Contains("Foo.pex", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void HashForRoundTrip_IgnoresDebugInfo()
    {
        var withoutBytes = PexWriter.Write(new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
            Objects = [new PexObject { Name = "Foo" }],
        });
        var withBytes = PexWriter.Write(new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
            DebugInfo = new PexDebugInfo
            {
                Functions =
                [
                    new PexDebugFunction
                    {
                        ObjectName = "Foo",
                        FunctionName = "Bar",
                        InstructionLineMap = [1, 2],
                    },
                ],
            },
            Objects = [new PexObject { Name = "Foo" }],
        });

        var roundWithout = ChampollionCommand.HashForRoundTrip(withoutBytes);
        var roundWith = ChampollionCommand.HashForRoundTrip(withBytes);
        Assert.Equal(roundWithout, roundWith);
        Assert.NotEqual(PexNormalizer.Hash(PexReader.Read(withBytes)), roundWith);
    }

    [Fact]
    public void TryFindDecompiledScript_PrefersRelativeThenObjectName()
    {
        using var root = new TempDir();
        var relative = Path.Combine(root.Path, "Data", "Scripts", "Vendor");
        Directory.CreateDirectory(relative);
        var relativePsc = Path.Combine(relative, "Foo.psc");
        File.WriteAllText(relativePsc, "ScriptName Vendor:Foo\n");

        Assert.True(ChampollionCommand.TryFindDecompiledScript(
            root.Path,
            "Data/Scripts/Vendor/Foo.pex",
            "Vendor:Foo",
            out var found));
        Assert.Equal(Path.GetFullPath(relativePsc), found);

        Directory.Delete(relative, recursive: true);
        var fromObject = Path.Combine(root.Path, "Vendor");
        Directory.CreateDirectory(fromObject);
        var objectPsc = Path.Combine(fromObject, "Foo.psc");
        File.WriteAllText(objectPsc, "ScriptName Vendor:Foo\n");

        Assert.True(ChampollionCommand.TryFindDecompiledScript(
            root.Path,
            "Data/Scripts/Vendor/Foo.pex",
            "Vendor:Foo",
            out found));
        Assert.Equal(Path.GetFullPath(objectPsc), found);
    }

    [Fact]
    public void Compile_ChampollionCompat_AcceptsScriptStructType()
    {
        using var root = new TempDir();
        var script = Path.Combine(root.Path, "Foo.psc");
        File.WriteAllText(
            script,
            "ScriptName Foo\nStruct Point\nFloat X\nEndStruct\nFunction Bar(Foo#Point origin)\nEndFunction\n");

        var code = Run(
            out _,
            out var stderr,
            "compile",
            script,
            "--game", "fallout4",
            "--champollion-compat");

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.True(File.Exists(Path.Combine(root.Path, "Foo.pex")));
    }

    private static byte[] EmptySkyrimPex()
    {
        return PexWriter.Write(new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
        });
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
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "papyrusc-champollion-" + Guid.NewGuid().ToString("N"));
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
