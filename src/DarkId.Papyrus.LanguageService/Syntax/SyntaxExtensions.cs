using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    public static class SyntaxExtensions
    {
        public static bool IsTrivia(this SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.LineContinuationTrivia => true,
                SyntaxKind.NewLineTrivia => true,
                SyntaxKind.WhitespaceTrivia => true,
                _ => false
            };
        }
    }
}
