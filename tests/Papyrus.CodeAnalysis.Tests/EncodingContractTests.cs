using System;
using System.Collections.Generic;
using System.Text;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Emit;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

/// <summary>
/// K19: default decode/emit is Windows-1252. Bytes that differ from UTF-8
/// (é, smart quotes, euro) round-trip through parse and the PEX string table.
/// Champollion's C++ reader is the design contract; these tests use
/// <see cref="PexReader"/> / <see cref="PexWriter"/> only.
/// </summary>
public sealed class EncodingContractTests
{
    // é “ ” € — 0xE9/0x93/0x94/0x80 in Windows-1252; multi-byte in UTF-8.
    private const string DistinctiveText = "\u00E9\u201C\u201D\u20AC";

    private static readonly byte[] DistinctiveWindows1252 = [0xE9, 0x93, 0x94, 0x80];

    [Fact]
    public void Default_parse_options_encoding_is_windows_1252()
    {
        Assert.Equal(PapyrusEncoding.Windows1252, PapyrusParseOptions.Default.Encoding);
        Assert.Equal(PapyrusEncoding.Windows1252, new PapyrusParseOptions().Encoding);
    }

    [Fact]
    public void Default_source_text_encoding_is_windows_1252()
    {
        Assert.Equal(PapyrusEncoding.Windows1252, SourceText.From("ScriptName Foo").Encoding);
        Assert.Equal(PapyrusEncoding.Windows1252, SourceText.From([(byte)'A']).Encoding);
    }

    [Fact]
    public void Distinctive_bytes_differ_between_utf8_and_windows_1252()
    {
        var windows1252 = PapyrusEncodings.GetEncoding(PapyrusEncoding.Windows1252);
        var utf8 = Encoding.UTF8.GetBytes(DistinctiveText);

        Assert.Equal(DistinctiveWindows1252, windows1252.GetBytes(DistinctiveText));
        Assert.NotEqual(DistinctiveWindows1252, utf8);
        Assert.Equal(DistinctiveText, windows1252.GetString(DistinctiveWindows1252));
        Assert.NotEqual(DistinctiveText, Encoding.UTF8.GetString(DistinctiveWindows1252));
    }

    [Fact]
    public void Parse_ToFullString_round_trips_windows_1252_source()
    {
        var source = ScriptWithCommentAndLiteral();
        var bytes = PapyrusEncodings.GetEncoding(PapyrusEncoding.Windows1252).GetBytes(source);
        Assert.NotEqual(Encoding.UTF8.GetBytes(source), bytes);

        var text = SourceText.From(bytes, PapyrusEncoding.Windows1252);
        Assert.Equal(PapyrusEncoding.Windows1252, text.Encoding);
        Assert.Equal(source, text.ToString());

        var tree = SyntaxTree.Parse(
            text,
            PapyrusDialect.SkyrimSe,
            PapyrusParseOptions.Default,
            TestContext.Current.CancellationToken);
        Assert.Equal(PapyrusEncoding.Windows1252, tree.Options.Encoding);
        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.Contains(DistinctiveText, tree.GetRoot().ToFullString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Lexer_round_trips_windows_1252_source()
    {
        var source = ScriptWithCommentAndLiteral();
        var bytes = PapyrusEncodings.GetEncoding(PapyrusEncoding.Windows1252).GetBytes(source);
        var text = SourceText.From(bytes, PapyrusEncoding.Windows1252);
        var lexer = new Lexer(text, PapyrusDialect.SkyrimSe, PapyrusParseOptions.Default);

        var builder = new StringBuilder();
        SyntaxToken token;
        var sawLiteral = false;
        do
        {
            token = lexer.Lex();
            builder.Append(token.ToFullString());
            if (token.Kind == SyntaxKind.StringLiteralToken)
            {
                sawLiteral = true;
                Assert.Contains(DistinctiveText, token.Text, StringComparison.Ordinal);
            }
        }
        while (token.Kind != SyntaxKind.EndOfFileToken);

        Assert.True(sawLiteral);
        Assert.Equal(source, builder.ToString());
    }

    [Fact]
    public void Emit_string_table_uses_windows_1252_not_utf8()
    {
        var source = ScriptWithCommentAndLiteral();
        var file = EmitFile(source);
        var bytes = PexWriter.Write(file);
        var roundTripped = PexReader.Read(bytes);

        Assert.Equal(bytes, PexWriter.Write(roundTripped));
        Assert.Contains(DistinctiveText, roundTripped.StringTable);
        Assert.True(ContainsSequence(bytes, DistinctiveWindows1252));
        Assert.False(ContainsSequence(bytes, Encoding.UTF8.GetBytes(DistinctiveText)));
    }

    private static string ScriptWithCommentAndLiteral()
    {
        return "; " + DistinctiveText + "\n"
            + "ScriptName Foo\n"
            + "String Function Bar()\n"
            + "  Return \"" + DistinctiveText + "\"\n"
            + "EndFunction\n";
    }

    private static PexFile EmitFile(string source)
    {
        var sources = new Dictionary<QualifiedName, SourceText>
        {
            [QualifiedName.Parse("Foo")] = SourceText.From(source),
        };
        var compilation = PapyrusCompilation.Create(PapyrusDialect.SkyrimSe, new TestScriptResolver(sources));
        var script = compilation.GetScript(QualifiedName.Parse("Foo"));
        return new PexEmitter().EmitFile(script);
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
