using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService.Syntax;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal static class SyntaxExtensions
    {
        private static IEnumerable<SyntaxToken> ToInlinedTriviaTokens(IEnumerable<SyntaxToken> tokens)
        {
            var currentTrivia = new List<GreenNode>();
            foreach (var token in tokens)
            {
                if (token.Kind.IsTrivia())
                {
                    currentTrivia.Add(new SyntaxToken(token.Kind, token.Text));
                    continue;
                }

                yield return new SyntaxToken(token.Kind, token.Text, currentTrivia);
                currentTrivia = new List<GreenNode>();
            }
        }

        public static IEnumerable<IEnumerable<SyntaxToken>> ToLogicalLines(this IEnumerable<SyntaxToken> tokens)
        {
            var currentLine = new List<SyntaxToken>();

            foreach (var token in ToInlinedTriviaTokens(tokens))
            {
                if (token.Children.All(t => t.Kind != SyntaxKind.NewLineTrivia))
                {
                    currentLine.Add(token);
                    continue;
                }

                yield return currentLine;
                currentLine = new List<SyntaxToken>() { token };
            }

            if (currentLine.Count > 0)
            {
                yield return currentLine;
            }
        }
    }
}
