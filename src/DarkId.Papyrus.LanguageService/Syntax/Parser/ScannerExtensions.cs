using System;
using System.Collections.Generic;
using System.Linq;
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

        //public static SyntaxToken NextNonTrivia(this Scanner<ScriptToken> scanner, bool multiline = false)
        //{
        //    var leadingTrivia = scanner.TakeWhile(t => t.Kind.IsTrivia(multiline)).ToList();

        //    if (scanner.Done)
        //    {
        //        return new SyntaxToken(default, leadingTrivia);
        //    }

        //    var token = scanner.Current;

        //    if (multiline || scanner.Done || scanner.Peek().Kind != SyntaxKind.NewLineTrivia)
        //    {
        //        return new SyntaxToken(token, leadingTrivia);
        //    }

        //    scanner.Next();
        //    var trailingTrivia = new List<ScriptToken>
        //    {
        //        scanner.Current
        //    };

        //    return new SyntaxToken(token, leadingTrivia, trailingTrivia);
        //}

        //public static SyntaxToken ExpectNextNonTrivia(this Scanner<ScriptToken> scanner, SyntaxKind kind,
        //    bool multiline = false)
        //{
        //    var token = scanner.NextNonTrivia(multiline);
        //    if (token != null && token.Kind == kind)
        //    { 
        //        return token;
        //    }

        //    token ??= new SyntaxToken(kind);

        //    var expected = ScriptLexer.StringTokenMap.ContainsKey(kind)
        //        ? ScriptLexer.StringTokenMap[kind]
        //        : nameof(kind);

        //    token.AddDiagnostic(new DiagnosticInfo(DiagnosticLevel.Error, 0, $"Expected {expected}."));

        //    return token;
        //}
    }
}
