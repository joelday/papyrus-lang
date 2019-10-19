using System;
using System.Collections.Generic;
using System.Text;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax.Internal
{
    internal static class SyntaxFactory
    {
        public static TokenNode CreateTokenNode(SyntaxKind kind, string text)
        {
            return new TokenNode(new ScriptToken(kind, text, new TextRange(), new ScriptLexerState()));
        }

        public static TokenNode CreateTokenNode(ScriptToken token)
        {
            return new TokenNode(token);
        }
    }
}
