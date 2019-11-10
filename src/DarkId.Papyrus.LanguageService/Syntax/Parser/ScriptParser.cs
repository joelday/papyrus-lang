using System;
using System.Collections.Generic;
using System.Text;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Syntax.InternalSyntax;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;
using DarkId.Papyrus.LanguageService.Syntax.Parser;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    public class ScriptParser
    {
        private Scanner<ScriptToken> _scanner;

        public SyntaxNode Parse(IEnumerable<ScriptToken> tokens)
        {
            _scanner = new Scanner<ScriptToken>(tokens);

            return ParseScript();
        }

        private SyntaxNode ParseScript()
        {
            var scriptNameKeyword = _scanner.ExpectNextNonTrivia(SyntaxKind.ScriptNameKeyword);
            var identifier = _scanner.ExpectNextNonTrivia(SyntaxKind.Identifier);
            return null;
        }
    }
}
