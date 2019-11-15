using System.Collections.Generic;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax.Parser
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

            var current = _scanner.NextNonTrivia();
            var extendsKeyword = current.Kind == SyntaxKind.ExtendsKeyword ? current : null;

            var extended = _scanner.ExpectNextNonTrivia(SyntaxKind.Identifier);

            var flags = _scanner.TakeWhile(t => t.Kind.IsFlagOrIdentifier());


            return null;
        }
    }
}
