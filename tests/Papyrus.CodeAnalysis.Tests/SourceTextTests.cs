using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class SourceTextTests
{
    [Fact]
    public void From_bytes_decodes_windows_1252()
    {
        var text = SourceText.From(new byte[] { 0x48, 0x69, 0x80 }, PapyrusEncoding.Windows1252);

        Assert.Equal("Hi€", text.ToString());
        Assert.Equal(PapyrusEncoding.Windows1252, text.Encoding);
        Assert.Equal(3, text.Length);
        Assert.Equal('€', text.GetChar(2));
    }

    [Fact]
    public void From_bytes_decodes_windows_1252_high_latin()
    {
        var text = SourceText.From(new byte[] { 0xC9, 0xE9 }, PapyrusEncoding.Windows1252);

        Assert.Equal("Éé", text.ToString());
    }

    [Fact]
    public void From_bytes_utf8_bom_overrides_default_to_utf8()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'H', (byte)'i' };
        var text = SourceText.From(bytes, PapyrusEncoding.Windows1252);

        Assert.Equal(PapyrusEncoding.Utf8, text.Encoding);
        Assert.Equal("\uFEFFHi", text.ToString());
    }

    [Fact]
    public void From_string_is_utf16_in_memory_with_declared_encoding()
    {
        var text = SourceText.From("ScriptName Hello", PapyrusEncoding.Windows1252);

        Assert.Equal("ScriptName Hello", text.ToString());
        Assert.Equal(PapyrusEncoding.Windows1252, text.Encoding);
        Assert.Equal("Hello", text.ToString(new TextSpan(11, 5)));
    }

    [Fact]
    public void Windows_1252_round_trips_euro_and_undefined_slots()
    {
        var encoding = PapyrusEncodings.GetEncoding(PapyrusEncoding.Windows1252);
        var original = new byte[] { 0x80, 0x81, 0xE9 };
        var decoded = encoding.GetString(original);
        var encoded = encoding.GetBytes(decoded);

        Assert.Equal("€\u0081é", decoded);
        Assert.Equal(original, encoded);
    }
}
