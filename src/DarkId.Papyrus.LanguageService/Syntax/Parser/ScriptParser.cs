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
        private LanguageVersion _languageVersion;

        public SyntaxNode Parse(IEnumerable<ScriptToken> tokens, LanguageVersion languageVersion)
        {
            _scanner = new Scanner<ScriptToken>(tokens);
            _languageVersion = languageVersion;

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
