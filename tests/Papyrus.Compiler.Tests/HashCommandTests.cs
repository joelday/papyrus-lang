using System;
using System.IO;
using Papyrus.CodeAnalysis.Testing;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.Compiler.Tests;

public sealed class HashCommandTests
{
    [Fact]
    public void Help_MentionsHashAndWriteBaselines()
    {
        var code = Run(out var stdout, out var stderr, "--help");

        Assert.Equal(0, code);
        Assert.Contains("scripts acquire", stdout, StringComparison.Ordinal);
        Assert.Contains("hash", stdout, StringComparison.Ordinal);
        Assert.Contains("--write-baselines", stdout, StringComparison.Ordinal);
        Assert.Contains("attestation", stdout, StringComparison.Ordinal);
        Assert.Contains(
            "papyrusc verify --write-baselines --source <path> --out <file> --attestation <file> --game <game>",
            stdout,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "--write-baselines --source <path> --out <file> --attestation <file> [--game <game>]",
            stdout,
            StringComparison.Ordinal);
        Assert.Empty(stderr);

        var version = Run(out var versionOut, out var versionErr, "--version");
        Assert.Equal(0, version);
        Assert.Equal(Cli.Version, versionOut.Trim());
        Assert.Empty(versionErr);
    }

    [Fact]
    public void Hash_MissingPath_ExitsNonZero()
    {
        var code = Run(out _, out var stderr, "hash");

        Assert.Equal(1, code);
        Assert.Contains("path", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_MissingFile_ExitsNonZero()
    {
        using var root = new TempDir();
        var missing = Path.Combine(root.Path, "nope.pex");

        var code = Run(out _, out var stderr, "hash", missing);

        Assert.Equal(1, code);
        Assert.Contains("not found", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_File_PrintsNormalizedSha256()
    {
        using var root = new TempDir();
        var file = Path.Combine(root.Path, "empty.pex");
        File.WriteAllBytes(file, EmptySkyrimPex());

        var code = Run(out var stdout, out var stderr, "hash", file);

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.Matches("^[0-9a-f]{64}  empty\\.pex\\s*$", stdout.Trim());
        Assert.Contains(PexHashHarness.HashBytes(EmptySkyrimPex()), stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_Directory_PrintsEachPex()
    {
        using var root = new TempDir();
        File.WriteAllBytes(Path.Combine(root.Path, "a.pex"), EmptySkyrimPex());
        File.WriteAllBytes(Path.Combine(root.Path, "b.pex"), EmptySkyrimPex());
        File.WriteAllText(Path.Combine(root.Path, "skip.psc"), "scriptname Skip");

        var code = Run(out var stdout, out var stderr, "hash", root.Path);

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.Contains("a.pex", stdout, StringComparison.Ordinal);
        Assert.Contains("b.pex", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("skip.psc", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WriteBaselines_WithoutAttestation_ExitsNonZero()
    {
        using var root = new TempDir();
        File.WriteAllBytes(Path.Combine(root.Path, "a.pex"), EmptySkyrimPex());

        var code = Run(
            out _,
            out var stderr,
            "verify",
            "--write-baselines",
            "--game", "skyrimse",
            "--source", root.Path,
            "--out", Path.Combine(root.Path, "out.json"));

        Assert.Equal(1, code);
        Assert.Contains("--attestation", stderr, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root.Path, "out.json")));
    }

    [Fact]
    public void Verify_WriteBaselines_WithIncompleteAttestation_ExitsNonZero()
    {
        using var root = new TempDir();
        File.WriteAllBytes(Path.Combine(root.Path, "a.pex"), EmptySkyrimPex());
        var note = Path.Combine(root.Path, "note.txt");
        File.WriteAllText(note, "Machine OS: macOS\npapyrusc version: 0\n");

        var code = Run(
            out _,
            out var stderr,
            "verify",
            "--write-baselines",
            "--game", "skyrimse",
            "--source", root.Path,
            "--out", Path.Combine(root.Path, "out.json"),
            "--attestation", note);

        Assert.Equal(1, code);
        Assert.Contains("CK compiler path SHA-256", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WriteBaselines_ThenCompare_RoundTrips()
    {
        using var root = new TempDir();
        File.WriteAllBytes(Path.Combine(root.Path, "Actor.pex"), EmptySkyrimPex());
        var note = Path.Combine(root.Path, "note.txt");
        File.WriteAllText(
            note,
            """
            Machine OS: macOS 15
            papyrusc version: 0.0.0-test
            CK compiler path SHA-256: n/a
            CK file version: n/a
            Statement: The hashed .pex files came from a legitimate game/CK install that I have a license to use.
            """);
        var baseline = Path.Combine(root.Path, "skyrimse.json");

        var write = Run(
            out var writeOut,
            out var writeErr,
            "verify",
            "--write-baselines",
            "--game", "skyrimse",
            "--source", root.Path,
            "--out", baseline,
            "--attestation", note);

        Assert.Equal(0, write);
        Assert.Empty(writeErr);
        Assert.Contains("Wrote 1 hash", writeOut, StringComparison.Ordinal);
        Assert.Contains("legitimate", File.ReadAllText(baseline), StringComparison.Ordinal);

        var compare = Run(
            out var compareOut,
            out var compareErr,
            "verify",
            "--game", "skyrimse",
            "--source", root.Path,
            "--baseline", baseline);

        Assert.Equal(0, compare);
        Assert.Empty(compareErr);
        Assert.Contains("OK:", compareOut, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_Compare_Mismatch_ExitsNonZero()
    {
        using var root = new TempDir();
        File.WriteAllBytes(Path.Combine(root.Path, "Actor.pex"), EmptySkyrimPex());
        var baseline = Path.Combine(root.Path, "skyrimse.json");
        File.WriteAllText(
            baseline,
            """
            {
              "game": "skyrimse",
              "generated": "2026-08-16",
              "scripts": {
                "Actor": { "sha256": "0000000000000000000000000000000000000000000000000000000000000000", "status": "identical" }
              }
            }
            """);

        var code = Run(
            out _,
            out var stderr,
            "verify",
            "--source", root.Path,
            "--baseline", baseline);

        Assert.Equal(1, code);
        Assert.Contains("hash mismatch: Actor", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithoutBaseline_ExitsNonZero()
    {
        using var root = new TempDir();
        File.WriteAllBytes(Path.Combine(root.Path, "a.pex"), EmptySkyrimPex());

        var code = Run(out _, out var stderr, "verify", "--source", root.Path);

        Assert.Equal(1, code);
        Assert.Contains("--baseline is required", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void BaselineAlias_WithoutBaseline_ExitsNonZero()
    {
        using var root = new TempDir();
        File.WriteAllBytes(Path.Combine(root.Path, "a.pex"), EmptySkyrimPex());

        var code = Run(out _, out var stderr, "baseline", "--source", root.Path);

        Assert.Equal(1, code);
        Assert.Contains("--baseline is required", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_MalformedBaseline_ExitsNonZero()
    {
        using var root = new TempDir();
        File.WriteAllBytes(Path.Combine(root.Path, "Actor.pex"), EmptySkyrimPex());
        var baseline = Path.Combine(root.Path, "bad.json");
        File.WriteAllText(baseline, "{ not json");

        var code = Run(
            out _,
            out var stderr,
            "verify",
            "--source", root.Path,
            "--baseline", baseline);

        Assert.Equal(1, code);
        Assert.Contains("invalid baseline JSON", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_EmptyBaseline_ExitsNonZero()
    {
        using var root = new TempDir();
        File.WriteAllBytes(Path.Combine(root.Path, "Actor.pex"), EmptySkyrimPex());
        var baseline = Path.Combine(root.Path, "empty.json");
        File.WriteAllText(baseline, "   \n");

        var code = Run(
            out _,
            out var stderr,
            "verify",
            "--source", root.Path,
            "--baseline", baseline);

        Assert.Equal(1, code);
        Assert.Contains("baseline JSON is empty", stderr, StringComparison.Ordinal);
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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "papyrusc-hash-" + Guid.NewGuid().ToString("N"));
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
