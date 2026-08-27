using System;
using System.Text;
using Xunit;

namespace Papyrus.Pex.Tests;

/// <summary>
/// PEX header and string-table bytes are Windows-1252 (K19), not UTF-8.
/// Champollion's C++ reader is the design contract; these tests use
/// <see cref="PexReader"/> / <see cref="PexWriter"/> only.
/// </summary>
public sealed class PexWriterEncodingTests
{
    // é “ ” €
    private const string DistinctiveText = "\u00E9\u201C\u201D\u20AC";

    private static readonly byte[] DistinctiveWindows1252 = [0xE9, 0x93, 0x94, 0x80];

    [Fact]
    public void Write_header_strings_are_windows_1252_not_utf8()
    {
        var file = MinimalFile();
        file.SourcePath = DistinctiveText + ".psc";
        file.UserName = DistinctiveText;
        file.MachineName = DistinctiveText;

        byte[] bytes = PexWriter.Write(file);
        PexFile read = PexReader.Read(bytes);

        Assert.Equal(file.SourcePath, read.SourcePath);
        Assert.Equal(file.UserName, read.UserName);
        Assert.Equal(file.MachineName, read.MachineName);
        Assert.True(ContainsSequence(bytes, DistinctiveWindows1252));
        Assert.False(ContainsSequence(bytes, Encoding.UTF8.GetBytes(DistinctiveText)));
        Assert.Equal(bytes, PexWriter.Write(read));
    }

    [Fact]
    public void Write_string_table_is_windows_1252_not_utf8()
    {
        var file = MinimalFile();
        file.Objects =
        [
            new PexObject
            {
                Name = DistinctiveText,
                ParentClassName = "",
                DocumentationString = DistinctiveText,
                AutoStateName = "",
            },
        ];

        byte[] bytes = PexWriter.Write(file);
        PexFile read = PexReader.Read(bytes);

        Assert.Contains(DistinctiveText, read.StringTable);
        Assert.Equal(DistinctiveText, Assert.Single(read.Objects).Name);
        Assert.Equal(DistinctiveText, Assert.Single(read.Objects).DocumentationString);
        Assert.True(ContainsSequence(bytes, DistinctiveWindows1252));
        Assert.False(ContainsSequence(bytes, Encoding.UTF8.GetBytes(DistinctiveText)));
        Assert.Equal(bytes, PexWriter.Write(read));
    }

    [Fact]
    public void Read_raw_windows_1252_header_and_string_table()
    {
        byte[] original = new PexBlobBuilder()
            .MagicLe()
            .Version(PexVersion.V32)
            .U16(GameIdMapping.SkyrimRaw)
            .U64(0)
            .StrBytes(0xE9, 0x2E, 0x70, 0x73, 0x63) // é.psc
            .StrBytes(0x93, 0x94)                     // “”
            .StrBytes(0x80)                           // €
            .U16(1)
            .StrBytes(DistinctiveWindows1252)
            .U8(0)
            .U16(0)
            .U16(0)
            .ToArray();

        PexFile file = PexReader.Read(original);
        Assert.Equal("\u00E9.psc", file.SourcePath);
        Assert.Equal("\u201C\u201D", file.UserName);
        Assert.Equal("\u20AC", file.MachineName);
        Assert.Equal(DistinctiveText, Assert.Single(file.StringTable));

        byte[] written = PexWriter.Write(file);
        Assert.Equal(original, written);
    }

    [Fact]
    public void Write_does_not_emit_utf8_for_latin_capital_e_acute()
    {
        var file = MinimalFile();
        file.SourcePath = "caf\u00E9.psc";

        byte[] bytes = PexWriter.Write(file);
        Assert.True(ContainsSequence(bytes, [0x63, 0x61, 0x66, 0xE9]));
        Assert.False(ContainsSequence(bytes, [0x63, 0x61, 0x66, 0xC3, 0xA9]));
        Assert.Equal("caf\u00E9.psc", PexReader.Read(bytes).SourcePath);
    }

    private static PexFile MinimalFile()
    {
        return new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
        };
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || needle.Length > haystack.Length)
        {
            return false;
        }

        var span = haystack.AsSpan();
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (span.Slice(i, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }
}
