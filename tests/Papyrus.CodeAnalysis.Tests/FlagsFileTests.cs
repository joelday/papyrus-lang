using System;
using System.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class FlagsFileTests
{
    [Theory]
    [InlineData("Hidden {0}\nConditional {1}\n")]
    [InlineData("Hidden {0} Conditional {1}")]
    public void Parse_explicit_bit_indices(string source)
    {
        var file = FlagsFile.Parse(source);

        Assert.Equal(2, file.Flags.Length);
        Assert.Equal("Hidden", file.Flags[0].Name);
        Assert.Equal(0, file.Flags[0].BitIndex);
        Assert.Equal("Conditional", file.Flags[1].Name);
        Assert.Equal(1, file.Flags[1].BitIndex);
        Assert.Empty(file.Diagnostics);
    }

    [Fact]
    public void Parse_assigns_omitted_bits_in_declaration_order()
    {
        var file = FlagsFile.Parse("Hidden\nConditional\nDefault\n");

        Assert.Equal(3, file.Flags.Length);
        Assert.Equal("Hidden", file.Flags[0].Name);
        Assert.Equal(0, file.Flags[0].BitIndex);
        Assert.Equal("Conditional", file.Flags[1].Name);
        Assert.Equal(1, file.Flags[1].BitIndex);
        Assert.Equal("Default", file.Flags[2].Name);
        Assert.Equal(2, file.Flags[2].BitIndex);
        Assert.Empty(file.Diagnostics);
    }

    [Fact]
    public void Parse_reports_33rd_named_flag()
    {
        var text = new StringBuilder();
        for (var i = 0; i < 33; i++)
        {
            text.Append("Flag").Append(i).AppendLine();
        }

        var file = FlagsFile.Parse(text.ToString());

        Assert.Equal(33, file.Flags.Length);
        Assert.Contains(file.Diagnostics, static d => d.Id == FlagsFile.TooManyFlagsDiagnosticId);
        Assert.All(file.Diagnostics, static d => Assert.StartsWith("PAP20", d.Id, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_allows_32_named_flags()
    {
        var text = new StringBuilder();
        for (var i = 0; i < 32; i++)
        {
            text.Append("Flag").Append(i).Append(" {").Append(i).Append('}').AppendLine();
        }

        var file = FlagsFile.Parse(text.ToString());

        Assert.Equal(32, file.Flags.Length);
        Assert.Empty(file.Diagnostics);
        Assert.Equal(0, file.Flags[0].BitIndex);
        Assert.Equal(31, file.Flags[31].BitIndex);
    }

    [Fact]
    public void Parse_reports_bit_index_greater_than_31()
    {
        var file = FlagsFile.Parse("Hidden {32}\n");

        Assert.Equal("Hidden", file.Flags[0].Name);
        Assert.Equal(32, file.Flags[0].BitIndex);
        Assert.Contains(file.Diagnostics, static d => d.Id == FlagsFile.BitIndexOutOfRangeDiagnosticId);
        Assert.All(file.Diagnostics, static d => Assert.StartsWith("PAP20", d.Id, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_skips_line_and_block_comments()
    {
        const string source = "; line comment\n;/ block\ncomment /;\nHidden {0}\n";
        var file = FlagsFile.Parse(source);

        Assert.Single(file.Flags);
        Assert.Equal("Hidden", file.Flags[0].Name);
        Assert.Equal(0, file.Flags[0].BitIndex);
        Assert.Empty(file.Diagnostics);
    }

    [Fact]
    public void Parse_does_not_throw_on_null_empty_or_garbage()
    {
        Assert.Empty(FlagsFile.Parse(null).Flags);
        Assert.Empty(FlagsFile.Parse(string.Empty).Flags);
        Assert.Empty(FlagsFile.Parse("   \n; only comment\n").Flags);

        var garbage = FlagsFile.Parse("@#$\n{99}\n");
        Assert.NotNull(garbage);
        Assert.Empty(garbage.Flags);
    }
}
