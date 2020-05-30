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
        public static IEnumerable<SyntaxToken> ToMergedOfKind(this IEnumerable<SyntaxToken> tokens, SyntaxKind kind)
        {
            SyntaxToken current = null;

            foreach (var token in tokens)
            {
                if (token.Kind == kind)
                {
                    if (current == null)
                    {
                        current = token;
                    }
                    else
                    {
                        current = new SyntaxToken(kind, current.Text + token.Text);
                    }
                }
                else
                {
                    if (current != null)
                    {
                        yield return current;
                        current = null;
                    }

                    yield return token;
                }
            }

            if (current != null)
            {
                yield return current;
            }
        }

        public static IEnumerable<SyntaxToken> ToInlinedTriviaTokens(this IEnumerable<SyntaxToken> tokens)
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

            if (currentNonTriviaToken != null)
            {
                yield return new SyntaxToken(currentNonTriviaToken.Kind, currentNonTriviaToken.Text, leadingTrivia, trailingTrivia);
                leadingTrivia = null;
            }
        }
    }
}