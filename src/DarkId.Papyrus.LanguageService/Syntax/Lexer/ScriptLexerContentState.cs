namespace DarkId.Papyrus.LanguageService.Syntax.Lexer
{
    internal enum ScriptLexerContentState
    {
        InSource = 0,
        InDocumentationComment,
        InMultilineComment,
        InSingleLineComment,
        InStringLiteral
    }
}