using System;
using System.Linq;
using System.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class FlagsFileTests
{
    // Institute_Papyrus_Flags.flg as shipped with the Fallout 4 Creation Kit (CRLF, C comments,
    // location lists, and a composite flag).
    private const string Fallout4Flags =
        "/*\r\n"
        + "\tFormat is as follows (whitespace is completely ignored, index must be between 0 and 31 inclusive):\r\n"
        + "\t\tFlag <name> <index>\t\t// flag is allowed on all types\r\n"
        + "*/\r\n"
        + "\r\n"
        + "// List of flags for Institute - DO NOT EDIT\r\n"
        + "\r\n"
        + "// Flag hides the script or property from the game editor\r\n"
        + "Flag Hidden 0\r\n{\r\n\tScript\r\n\tProperty\r\n\tStructVar\r\n}\r\n\r\n"
        + "Flag Conditional 1\r\n{\r\n\tScript\r\n\tVariable\r\n}\r\n\r\n"
        + "Flag Default 2\r\n{\r\n\tScript\r\n}\r\n\r\n"
        + "Flag CollapsedOnRef 3\r\n{\r\n\tGroup\r\n}\r\n\r\n"
        + "Flag CollapsedOnBase 4\r\n{\r\n\tGroup\r\n}\r\n\r\n"
        + "// Flags this group as collapsed (on both ref and base) in the editor\r\n"
        + "Flag Collapsed CollapsedOnRef & CollapsedOnBase\r\n\r\n"
        + "Flag Mandatory 5\r\n{\r\n\tProperty\r\n}";

    [Fact]
    public void Parses_the_fallout4_creation_kit_flags_file()
    {
        var file = FlagsFile.Parse(Fallout4Flags);

        Assert.Empty(file.Diagnostics);
        Assert.Equal(
            ["Hidden", "Conditional", "Default", "CollapsedOnRef", "CollapsedOnBase", "Collapsed", "Mandatory"],
            file.Flags.Select(static f => f.Name));

        var hidden = file.Find("hidden")!;
        Assert.Equal(0, hidden.BitIndex);
        Assert.Equal(
            UserFlagLocations.Script | UserFlagLocations.Property | UserFlagLocations.StructVar,
            hidden.Locations);

        var mandatory = file.Find("Mandatory")!;
        Assert.Equal(5, mandatory.BitIndex);
        Assert.Equal(1u << 5, mandatory.Mask);
        Assert.Equal(UserFlagLocations.Property, mandatory.Locations);
    }

    [Fact]
    public void Composite_flag_combines_child_bits_and_common_locations()
    {
        var collapsed = FlagsFile.Parse(Fallout4Flags).Find("Collapsed")!;

        Assert.True(collapsed.IsComposite);
        Assert.Equal(-1, collapsed.BitIndex);
        Assert.Equal((1u << 3) | (1u << 4), collapsed.Mask);
        Assert.Equal(UserFlagLocations.Group, collapsed.Locations);
    }

    [Fact]
    public void Flag_without_location_list_is_allowed_everywhere()
    {
        var file = FlagsFile.Parse("flag Hidden 0\nFLAG Conditional 1 { script variable }\n");

        Assert.Empty(file.Diagnostics);
        Assert.Equal(UserFlagLocations.All, file.Flags[0].Locations);
        Assert.Equal(UserFlagLocations.Script | UserFlagLocations.Variable, file.Flags[1].Locations);
    }

    [Fact]
    public void Reports_33rd_flag_with_its_own_bit_but_not_composites()
    {
        var text = new StringBuilder();
        for (var i = 0; i < 32; i++)
        {
            text.Append("Flag F").Append(i).Append(' ').Append(i).AppendLine();
        }

        text.AppendLine("Flag Both F0 & F1");
        Assert.Empty(FlagsFile.Parse(text.ToString()).Diagnostics);

        text.AppendLine("Flag Extra 32");
        var file = FlagsFile.Parse(text.ToString());
        Assert.Contains(file.Diagnostics, static d => d.Id == FlagsFile.TooManyFlagsDiagnosticId);
    }

    [Fact]
    public void Reports_bit_index_greater_than_31()
    {
        var file = FlagsFile.Parse("Flag Hidden 32\n");

        Assert.Equal(32, file.Flags[0].BitIndex);
        Assert.Equal(0u, file.Flags[0].Mask);
        var diagnostic = Assert.Single(file.Diagnostics);
        Assert.Equal(FlagsFile.BitIndexOutOfRangeDiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("Flag Hidden 0\nFlag Hidden 1\n", "already defined")]
    [InlineData("Flag Hidden 0\nFlag Other 0\n", "reuses bit index 0")]
    [InlineData("Flag Hidden 0\nFlag Both Missing & Hidden\n", "unknown flag 'Missing'")]
    [InlineData("Flag Hidden 0 { Script Bogus }\n", "unexpected 'Bogus'")]
    [InlineData("Flag A 0 { Script }\nFlag B 1 { Property }\nFlag C A & B\n", "no valid locations in common")]
    [InlineData("Hidden {0}\n", "expected 'Flag'")]
    public void Reports_invalid_definitions(string source, string message)
    {
        var file = FlagsFile.Parse(source);

        var diagnostic = Assert.Single(file.Diagnostics, static d => d.Id == FlagsFile.InvalidSyntaxDiagnosticId);
        Assert.Contains(message, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recovers_at_the_next_flag_after_an_error()
    {
        var file = FlagsFile.Parse("Flag Broken { Script }\nFlag Hidden 0\n");

        Assert.Single(file.Diagnostics);
        Assert.Equal("Hidden", Assert.Single(file.Flags).Name);
    }

    [Fact]
    public void Does_not_throw_on_null_empty_or_garbage()
    {
        Assert.Empty(FlagsFile.Parse(null).Flags);
        Assert.Empty(FlagsFile.Parse(string.Empty).Flags);
        Assert.Empty(FlagsFile.Parse("  // only a comment\n/* and\na block */").Flags);
        Assert.Empty(FlagsFile.Parse("/* unterminated").Flags);

        var garbage = FlagsFile.Parse("@#$\n{99}\n");
        Assert.Empty(garbage.Flags);
        Assert.NotEmpty(garbage.Diagnostics);
    }
}
