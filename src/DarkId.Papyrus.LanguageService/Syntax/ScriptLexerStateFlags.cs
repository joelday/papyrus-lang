using System;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    [Flags]
    public enum ScriptLexerStateFlags
    {
        InDocumentationComment = 0x1,
        InMultilineComment = 0x2,
        InSingleLineComment = 0x3,
        InStringLiteral = 0x4
    }
}