namespace DarkId.Papyrus.LanguageService.Syntax.Lexer
{
    public enum ScriptLexerContentState
    {
        InSource = 0,
        InDocumentationComment,
        InMultilineComment,
        InSingleLineComment,
        InStringLiteral
    }
}