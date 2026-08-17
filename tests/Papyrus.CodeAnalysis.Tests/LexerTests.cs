using System;
using System.Collections.Generic;
using System.Text;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class LexerTests
{
    [Fact]
    public void Lexes_script_name_header()
    {
        var tokens = LexAll("ScriptName Foo\n");

        Assert.Collection(
            tokens,
            token =>
            {
                Assert.Equal(SyntaxKind.ScriptNameKeyword, token.Kind);
                Assert.Equal("ScriptName", token.Text);
                Assert.Equal("ScriptName", token.ToFullString());
            },
            token =>
            {
                Assert.Equal(SyntaxKind.IdentifierToken, token.Kind);
                Assert.Equal("Foo", token.Text);
                Assert.Equal(" Foo", token.ToFullString());
            },
            token =>
            {
                Assert.Equal(SyntaxKind.EndOfLineToken, token.Kind);
                Assert.Equal("\n", token.Text);
            },
            token => Assert.Equal(SyntaxKind.EndOfFileToken, token.Kind));
    }

    [Fact]
    public void Lexes_line_and_block_comments_as_trivia()
    {
        var tokens = LexAll("; line\n;/ block /;\n");

        Assert.Equal(SyntaxKind.EndOfLineToken, tokens[0].Kind);
        Assert.Equal(SyntaxKind.LineCommentTrivia, tokens[0].LeadingTrivia.Node!.Kind);
        Assert.Equal("; line", tokens[0].LeadingTrivia.ToFullString());

        Assert.Equal(SyntaxKind.EndOfLineToken, tokens[1].Kind);
        Assert.Equal(SyntaxKind.BlockCommentTrivia, tokens[1].LeadingTrivia.Node!.Kind);
        Assert.Equal(";/ block /;", tokens[1].LeadingTrivia.ToFullString());

        Assert.Equal(SyntaxKind.EndOfFileToken, tokens[2].Kind);
    }

    [Fact]
    public void Lexes_doc_comment_as_token()
    {
        var tokens = LexAll("{doc}\n");

        Assert.Equal(SyntaxKind.DocCommentToken, tokens[0].Kind);
        Assert.Equal("{doc}", tokens[0].Text);
        Assert.Equal(SyntaxKind.EndOfLineToken, tokens[1].Kind);
        Assert.Equal(SyntaxKind.EndOfFileToken, tokens[2].Kind);
    }

    [Fact]
    public void Struct_is_identifier_when_fo4_keywords_are_off()
    {
        var skyrim = LexAll("Struct\n", PapyrusDialect.SkyrimSe);
        Assert.Equal(SyntaxKind.IdentifierToken, skyrim[0].Kind);
        Assert.Equal("Struct", skyrim[0].Text);

        var fallout = LexAll("Struct\n", PapyrusDialect.Fallout4);
        Assert.Equal(SyntaxKind.StructKeyword, fallout[0].Kind);
        Assert.Equal("Struct", fallout[0].Text);
    }

    [Fact]
    public void Guard_is_identifier_unless_starfield_keywords_are_on()
    {
        Assert.Equal(SyntaxKind.IdentifierToken, LexAll("Guard\n", PapyrusDialect.Fallout4)[0].Kind);
        Assert.Equal(SyntaxKind.IdentifierToken, LexAll("Guard\n", PapyrusDialect.SkyrimSe)[0].Kind);
        Assert.Equal(SyntaxKind.GuardKeyword, LexAll("Guard\n", PapyrusDialect.Starfield)[0].Kind);
    }

    [Fact]
    public void Lock_is_always_an_identifier()
    {
        Assert.Equal(SyntaxKind.IdentifierToken, LexAll("Lock\n", PapyrusDialect.SkyrimSe)[0].Kind);
        Assert.Equal(SyntaxKind.IdentifierToken, LexAll("Lock\n", PapyrusDialect.Fallout4)[0].Kind);
        Assert.Equal(SyntaxKind.IdentifierToken, LexAll("Lock\n", PapyrusDialect.Starfield)[0].Kind);
        Assert.Equal(SyntaxKind.IdentifierToken, LexAll("Unlock\n", PapyrusDialect.Starfield)[0].Kind);
        Assert.Equal(SyntaxKind.IdentifierToken, LexAll("TryLock\n", PapyrusDialect.Starfield)[0].Kind);
        Assert.Equal(SyntaxKind.IdentifierToken, LexAll("EndLock\n", PapyrusDialect.Starfield)[0].Kind);
    }

    [Fact]
    public void Garbage_input_does_not_throw_and_stays_in_the_stream()
    {
        var source = "@#$\n";
        var tokens = LexAll(source);

        Assert.All(tokens, token => Assert.False(token.IsMissing));
        Assert.Contains(tokens, token => token.Kind == SyntaxKind.BadToken);
        Assert.Equal(source, Concatenate(tokens));
    }

    [Fact]
    public void Line_continuation_suppresses_newline_token()
    {
        var tokens = LexAll("ScriptName\\\n Foo\n");

        Assert.Collection(
            tokens,
            token => Assert.Equal(SyntaxKind.ScriptNameKeyword, token.Kind),
            token =>
            {
                Assert.Equal(SyntaxKind.IdentifierToken, token.Kind);
                Assert.Equal("Foo", token.Text);
                Assert.Contains("\\", token.LeadingTrivia.ToFullString(), StringComparison.Ordinal);
                Assert.DoesNotContain(tokens, t => t.Kind == SyntaxKind.EndOfLineToken && t == token);
            },
            token => Assert.Equal(SyntaxKind.EndOfLineToken, token.Kind),
            token => Assert.Equal(SyntaxKind.EndOfFileToken, token.Kind));

        Assert.Equal(1, Count(tokens, SyntaxKind.EndOfLineToken));
    }

    [Fact]
    public void Keywords_are_matched_case_insensitively()
    {
        var tokens = LexAll("scriptname function\n");

        Assert.Equal(SyntaxKind.ScriptNameKeyword, tokens[0].Kind);
        Assert.Equal("scriptname", tokens[0].Text);
        Assert.Equal(SyntaxKind.FunctionKeyword, tokens[1].Kind);
        Assert.Equal("function", tokens[1].Text);
    }

    [Fact]
    public void Concatenated_tokens_round_trip_source()
    {
        const string source = "{header}\nScriptName Foo ; hi\n;/ block\ncomment /;\nInt Property X = 1 Auto\n";
        Assert.Equal(source, Concatenate(LexAll(source)));
    }

    private static List<SyntaxToken> LexAll(string source, PapyrusDialect? dialect = null)
    {
        var lexer = new Lexer(
            SourceText.From(source),
            dialect ?? PapyrusDialect.SkyrimSe,
            PapyrusParseOptions.Default);

        var tokens = new List<SyntaxToken>();
        SyntaxToken token;
        do
        {
            token = lexer.Lex();
            tokens.Add(token);
        }
        while (token.Kind != SyntaxKind.EndOfFileToken);

        return tokens;
    }

    private static string Concatenate(List<SyntaxToken> tokens)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < tokens.Count; i++)
        {
            builder.Append(tokens[i].ToFullString());
        }

        return builder.ToString();
    }

    private static int Count(List<SyntaxToken> tokens, SyntaxKind kind)
    {
        var count = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == kind)
            {
                count++;
            }
        }

        return count;
    }
}
