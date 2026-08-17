using System;
using Papyrus.CodeAnalysis.Syntax;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class SyntaxKindTests
{
    [Theory]
    [InlineData("ScriptNameKeyword")]
    [InlineData("ExtendsKeyword")]
    [InlineData("NativeKeyword")]
    [InlineData("HiddenKeyword")]
    [InlineData("ConditionalKeyword")]
    [InlineData("IdentifierToken")]
    [InlineData("EndOfLineToken")]
    [InlineData("EndOfFileToken")]
    [InlineData("DocCommentToken")]
    [InlineData("IntegerLiteralToken")]
    [InlineData("HexLiteralToken")]
    [InlineData("FloatLiteralToken")]
    [InlineData("StringLiteralToken")]
    [InlineData("MissingToken")]
    [InlineData("ColonToken")]
    [InlineData("HashToken")]
    [InlineData("OpenBracketToken")]
    [InlineData("CloseBracketToken")]
    [InlineData("WhitespaceTrivia")]
    [InlineData("EndOfLineTrivia")]
    [InlineData("LineCommentTrivia")]
    [InlineData("BlockCommentTrivia")]
    [InlineData("LineContinuationTrivia")]
    [InlineData("SkippedTokensTrivia")]
    [InlineData("ScriptDeclaration")]
    public void Contains_core_script_header_member(string name)
    {
        Assert.True(Enum.TryParse<SyntaxKind>(name, out var kind), "Missing SyntaxKind." + name);
        Assert.NotEqual(SyntaxKind.None, kind);
    }

    [Fact]
    public void None_is_zero()
    {
        Assert.Equal((ushort)0, (ushort)SyntaxKind.None);
    }

    [Fact]
    public void ScriptNameKeyword_is_a_keyword_token()
    {
        Assert.True(SyntaxFacts.IsKeyword(SyntaxKind.ScriptNameKeyword));
        Assert.True(SyntaxFacts.IsToken(SyntaxKind.ScriptNameKeyword));
        Assert.False(SyntaxFacts.IsTrivia(SyntaxKind.ScriptNameKeyword));
        Assert.Equal("ScriptName", SyntaxFacts.GetText(SyntaxKind.ScriptNameKeyword));
    }

    [Fact]
    public void IdentifierToken_is_a_token()
    {
        Assert.True(SyntaxFacts.IsToken(SyntaxKind.IdentifierToken));
        Assert.False(SyntaxFacts.IsKeyword(SyntaxKind.IdentifierToken));
        Assert.False(SyntaxFacts.IsTrivia(SyntaxKind.IdentifierToken));
    }

    [Fact]
    public void WhitespaceTrivia_is_trivia()
    {
        Assert.True(SyntaxFacts.IsTrivia(SyntaxKind.WhitespaceTrivia));
        Assert.False(SyntaxFacts.IsToken(SyntaxKind.WhitespaceTrivia));
        Assert.False(SyntaxFacts.IsKeyword(SyntaxKind.WhitespaceTrivia));
    }

    [Fact]
    public void Punctuation_and_flag_keywords_have_canonical_text()
    {
        Assert.Equal(":", SyntaxFacts.GetText(SyntaxKind.ColonToken));
        Assert.Equal("#", SyntaxFacts.GetText(SyntaxKind.HashToken));
        Assert.Equal("[", SyntaxFacts.GetText(SyntaxKind.OpenBracketToken));
        Assert.Equal("]", SyntaxFacts.GetText(SyntaxKind.CloseBracketToken));
        Assert.Equal("Extends", SyntaxFacts.GetText(SyntaxKind.ExtendsKeyword));
        Assert.Equal("Native", SyntaxFacts.GetText(SyntaxKind.NativeKeyword));
        Assert.Equal("Hidden", SyntaxFacts.GetText(SyntaxKind.HiddenKeyword));
        Assert.Equal("Conditional", SyntaxFacts.GetText(SyntaxKind.ConditionalKeyword));
    }
}
