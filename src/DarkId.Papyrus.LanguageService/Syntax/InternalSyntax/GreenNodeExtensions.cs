using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal static class GreenNodeExtensions
    {
        public static IEnumerable<GreenNode> EnumerateDescendants(this GreenNode node, bool includeSelf = true)
        {
            if (includeSelf)
            {
                yield return node;
            }

            foreach (var subchild in node.Children.SelectMany(child => child.EnumerateDescendants()))
            {
                yield return subchild;
            }
        }
    }
}
