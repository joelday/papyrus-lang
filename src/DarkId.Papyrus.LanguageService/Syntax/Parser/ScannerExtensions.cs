using System;
using System.Collections.Generic;
using System.Text;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Syntax.InternalSyntax;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax.Parser
{
    internal static class ScannerExtensions
    {
        public static IEnumerable<T> TakeWhile<T>(this Scanner<T> scanner, Func<T, bool> whileFunc)
        {
            while (!scanner.Done && whileFunc(scanner.Peek()))
            {
                scanner.Next();
                yield return scanner.Current;
            }
        }

        public static SyntaxToken NextNonTrivia(this Scanner<ScriptToken> scanner, bool multiLine = false)
        {
            var leadingTrivia = scanner.TakeWhile(t => t.Kind.IsTrivia(multiLine));

        }
    }
}
