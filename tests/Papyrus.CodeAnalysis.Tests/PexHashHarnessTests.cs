using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Papyrus.CodeAnalysis.Testing;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class PexHashHarnessTests
{
    [Fact]
    public void HashBytes_SyntheticFixtures_MatchCommittedHashFile()
    {
        Dictionary<string, string> expected = BaselineFile.ParseHashList(File.ReadAllText(FixturePath("synthetic.hashes")));
        var actual = new List<PexHashEntry>();
        foreach (string name in new[] { "empty-skyrim", "empty-fallout4", "empty-starfield" })
        {
            string hash = PexHashHarness.HashBytes(ReadHexFixture(name + ".pex.hex"));
            actual.Add(new PexHashEntry(name + ".pex", name, hash));
        }

        foreach (PexHashEntry entry in actual)
        {
            Assert.True(expected.TryGetValue(entry.ScriptName, out string? want), $"missing committed hash for {entry.ScriptName}");
            Assert.Equal(want, entry.Sha256);
        }

        Assert.Equal(expected.Count, actual.Count);
    }

    [Fact]
    public void HashDirectory_SyntheticPexOnDisk_MatchesCommittedHashFile()
    {
        using var dir = new TempDir();
        foreach (string name in new[] { "empty-skyrim", "empty-fallout4", "empty-starfield" })
            File.WriteAllBytes(Path.Combine(dir.Path, name + ".pex"), ReadHexFixture(name + ".pex.hex"));

        var entries = PexHashHarness.HashDirectory(dir.Path);
        Dictionary<string, string> expected = BaselineFile.ParseHashList(File.ReadAllText(FixturePath("synthetic.hashes")));
        var comparison = BaselineFile.Compare(
            BaselineFile.FromHashes("synthetic", ToEntries(expected), generated: "2026-08-16"),
            entries);

        Assert.True(comparison.IsMatch, string.Join(Environment.NewLine, comparison.Errors));
    }

    [Fact]
    public void HashBytes_FilesDifferingOnlyInPathTimeUser_AreEqual()
    {
        string a = PexHashHarness.HashBytes(ReadHexFixture("incidental-a.pex.hex"));
        string b = PexHashHarness.HashBytes(ReadHexFixture("incidental-b.pex.hex"));
        Assert.Equal(a, b);
        Assert.Matches("^[0-9a-f]{64}$", a);
    }

    [Fact]
    public void HashFile_MissingPath_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => PexHashHarness.HashFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pex")));
    }

    [Fact]
    public void HashPath_RejectsNonPexFile()
    {
        using var dir = new TempDir();
        string file = Path.Combine(dir.Path, "notes.txt");
        File.WriteAllText(file, "nope");
        Assert.Throws<InvalidOperationException>(() => PexHashHarness.HashPath(file));
    }

    [Fact]
    public void HashBytes_CorruptPex_Throws()
    {
        Assert.Throws<PexFormatException>(() => PexHashHarness.HashBytes([0x00, 0x01, 0x02, 0x03]));
    }

    [Fact]
    public void WriteBaselines_RequiresAttestationAndEmbedsIt()
    {
        using var dir = new TempDir();
        string pex = Path.Combine(dir.Path, "empty-skyrim.pex");
        File.WriteAllBytes(pex, ReadHexFixture("empty-skyrim.pex.hex"));
        var entries = PexHashHarness.HashDirectory(dir.Path);

        Assert.False(AttestationNote.TryParse("", out _, out string emptyError));
        Assert.Contains("empty", emptyError, StringComparison.Ordinal);

        Assert.False(AttestationNote.TryParse("Machine OS: macOS\n", out _, out string missing));
        Assert.Contains("papyrusc version", missing, StringComparison.Ordinal);

        const string noteText =
            """
            Machine OS: macOS 15
            papyrusc version: 0.0.0-test
            CK compiler path SHA-256: n/a
            CK file version: n/a
            Statement: The hashed .pex files came from a legitimate game/CK install that I have a license to use.
            """;
        Assert.True(AttestationNote.TryParse(noteText, out AttestationNote note, out string error), error);

        string outPath = Path.Combine(dir.Path, "synthetic.json");
        BaselineFile.WriteBaselines(outPath, "synthetic", entries, note, generated: "2026-08-16");

        BaselineDocument loaded = BaselineFile.Load(outPath);
        Assert.Equal("synthetic", loaded.Game);
        Assert.NotNull(loaded.Attestation);
        Assert.Contains("legitimate", loaded.Attestation!.Statement, StringComparison.OrdinalIgnoreCase);
        Assert.True(loaded.Scripts.ContainsKey("empty-skyrim"));
        Assert.Equal(entries[0].Sha256, loaded.Scripts["empty-skyrim"].Sha256);
    }

    [Fact]
    public void Compare_ReportsMismatchAndMissing()
    {
        var expected = BaselineFile.FromHashes(
            "synthetic",
            [new PexHashEntry("keep.pex", "keep", "aa"), new PexHashEntry("gone.pex", "gone", "bb")],
            generated: "2026-08-16");
        var actual = new[]
        {
            new PexHashEntry("keep.pex", "keep", "cc"),
            new PexHashEntry("extra.pex", "extra", "dd"),
        };

        BaselineComparison comparison = BaselineFile.Compare(expected, actual);
        Assert.False(comparison.IsMatch);
        Assert.Contains(comparison.Errors, e => e.Contains("hash mismatch: keep", StringComparison.Ordinal));
        Assert.Contains(comparison.Errors, e => e.Contains("missing from source: gone", StringComparison.Ordinal));
        Assert.Contains(comparison.Errors, e => e.Contains("missing from baseline: extra", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_MalformedOrEmptyJson_ThrowsInvalidOperation()
    {
        var empty = Assert.Throws<InvalidOperationException>(() => BaselineFile.Parse("  "));
        Assert.Contains("empty", empty.Message, StringComparison.Ordinal);

        var bad = Assert.Throws<InvalidOperationException>(() => BaselineFile.Parse("{ not json"));
        Assert.Contains("invalid baseline JSON", bad.Message, StringComparison.Ordinal);
        Assert.IsType<System.Text.Json.JsonException>(bad.InnerException);
    }

    [Fact]
    public void ParseHashList_IgnoresComments()
    {
        Dictionary<string, string> parsed = BaselineFile.ParseHashList(
            """
            # comment
            empty-skyrim  abcdef
            """);
        Assert.Equal("abcdef", parsed["empty-skyrim"]);
    }

    private static IEnumerable<PexHashEntry> ToEntries(Dictionary<string, string> hashes)
    {
        foreach (KeyValuePair<string, string> pair in hashes)
            yield return new PexHashEntry(pair.Key + ".pex", pair.Key, pair.Value);
    }

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static byte[] ReadHexFixture(string name)
    {
        var hex = new StringBuilder();
        foreach (string raw in File.ReadAllLines(FixturePath(name)))
        {
            string line = raw;
            int comment = line.IndexOf('#');
            if (comment >= 0)
                line = line[..comment];
            foreach (char c in line)
            {
                if (!char.IsWhiteSpace(c))
                    hex.Append(c);
            }
        }

        return Convert.FromHexString(hex.ToString());
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "papyrus-hash-" + Guid.NewGuid().ToString("N"));
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
