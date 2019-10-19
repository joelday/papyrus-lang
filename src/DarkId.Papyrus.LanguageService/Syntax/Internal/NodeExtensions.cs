using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkId.Papyrus.LanguageService.Syntax.Internal
{
    internal static class NodeExtensions
    {
        public static IEnumerable<SyntaxNode> GetDescendantNodes(this SyntaxNode node, bool includeSelf = true)
        {
            if (node == null)
            { 
                yield break;
            }

            if (includeSelf)
            {
                yield return node;
            }

            foreach (var descendant in node.Children.SelectMany(child => child.GetDescendantNodes()))
            {
                yield return descendant;
            }
        }

        public static IEnumerable<SyntaxNode> GetLeafNodes(this SyntaxNode node, bool includeSelf = true)
        {
            return node.GetDescendantNodes(includeSelf).Where(descendant => descendant.Children.Count == 0);
        }
    }
}
