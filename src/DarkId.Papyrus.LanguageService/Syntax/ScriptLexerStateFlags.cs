using System;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    [Flags]
    public enum ScriptLexerStateFlags
    {
        InDocumentationComment = 1,
        InMultilineComment = 2,
        InSingleLineComment = 4,
        InStringLiteral = 8
    }
}