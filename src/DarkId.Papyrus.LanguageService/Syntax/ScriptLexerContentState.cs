using System;

namespace DarkId.Papyrus.LanguageService.Syntax
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