namespace Papyrus.CodeAnalysis.Syntax;

[SyntaxToken]
internal readonly partial struct IdentifierToken;

[SyntaxToken]
internal readonly partial struct IntegerLiteralToken;

[SyntaxToken]
internal readonly partial struct HexLiteralToken;

[SyntaxToken]
internal readonly partial struct FloatLiteralToken;

[SyntaxToken]
internal readonly partial struct StringLiteralToken;

[SyntaxToken]
internal readonly partial struct DocCommentToken;

[SyntaxToken]
internal readonly partial struct EndOfLineToken;

[SyntaxToken]
internal readonly partial struct EndOfFileToken;

[SyntaxToken]
internal readonly partial struct MissingToken;

[SyntaxToken(Text = ":")]
internal readonly partial struct ColonToken;

[SyntaxToken(Text = "#")]
internal readonly partial struct HashToken;

[SyntaxToken(Text = "[")]
internal readonly partial struct OpenBracketToken;

[SyntaxToken(Text = "]")]
internal readonly partial struct CloseBracketToken;

[SyntaxToken]
internal readonly partial struct ScriptNameKeyword;

[SyntaxToken]
internal readonly partial struct ExtendsKeyword;

[SyntaxToken]
internal readonly partial struct NativeKeyword;

[SyntaxToken]
internal readonly partial struct HiddenKeyword;

[SyntaxToken]
internal readonly partial struct ConditionalKeyword;
