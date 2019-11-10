using System;
using System.Collections.Generic;
using System.Text;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Syntax.InternalSyntax;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    public class ScriptParser
    {
        private Scanner<ScriptToken> _scanner;

        public SyntaxNode Parse(IEnumerable<ScriptToken> tokens)
        {
            _scanner = new Scanner<ScriptToken>(tokens);

            return null;
        }
    }
}
