using System;
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
            var trailingTrivia = new List<GreenNode>();
            var leadingTrivia = new List<GreenNode>();

            SyntaxToken currentNonTriviaToken = null;

            foreach (var token in tokens)
            {
                if (token.Kind.IsTrivia())
                {
                    if (currentNonTriviaToken == null)
                    {
                        leadingTrivia.Add(token);
                    }
                    else
                    {
                        trailingTrivia.Add(token);
                    }
                }
                else
                {
                    if (currentNonTriviaToken != null)
                    {
                        yield return new SyntaxToken(currentNonTriviaToken.Kind, currentNonTriviaToken.Text, leadingTrivia, trailingTrivia);
                        leadingTrivia = null;
                    }

                    currentNonTriviaToken = token;
                    trailingTrivia = new List<GreenNode>();
                }
            }
        }

        public static IEnumerable<IEnumerable<SyntaxToken>> ToLogicalLines(this IEnumerable<SyntaxToken> tokens)
        {
            var currentLine = new List<SyntaxToken>();

            foreach (var token in ToInlinedTriviaTokens(tokens))
            {
                currentLine.Add(token);

                if (token.Children.All(t => t.Kind != SyntaxKind.NewLineTrivia)) continue;

                yield return currentLine;
                currentLine = new List<SyntaxToken>();
            }

            if (currentLine.Count > 0)
            {
                yield return currentLine;
            }
        }
    }
}