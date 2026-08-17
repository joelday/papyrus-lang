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
    [InlineData("FunctionKeyword")]
    [InlineData("StructKeyword")]
    [InlineData("VarKeyword")]
    [InlineData("GuardKeyword")]
    [InlineData("LockKeyword")]
    [InlineData("IsKeyword")]
    [InlineData("LengthKeyword")]
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
    [InlineData("List")]
    [InlineData("IdentifierName")]
    [InlineData("QualifiedName")]
    [InlineData("TypeName")]
    [InlineData("ExtendsClause")]
    [InlineData("ScriptDeclaration")]
    [InlineData("Flag")]
    [InlineData("ImportKeyword")]
    [InlineData("ImportDeclaration")]
    [InlineData("FunctionKeyword")]
    [InlineData("EndFunctionKeyword")]
    [InlineData("EventKeyword")]
    [InlineData("EndEventKeyword")]
    [InlineData("PropertyKeyword")]
    [InlineData("EndPropertyKeyword")]
    [InlineData("StateKeyword")]
    [InlineData("EndStateKeyword")]
    [InlineData("AutoKeyword")]
    [InlineData("AutoReadOnlyKeyword")]
    [InlineData("GlobalKeyword")]
    [InlineData("FunctionDeclaration")]
    [InlineData("EventDeclaration")]
    [InlineData("PropertyDeclaration")]
    [InlineData("VariableDeclaration")]
    [InlineData("StateDeclaration")]
    [InlineData("ParameterList")]
    [InlineData("Parameter")]
    [InlineData("EqualsValueClause")]
    [InlineData("LiteralExpression")]
    [InlineData("PrefixUnaryExpression")]
    [InlineData("SkippedStatement")]
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
