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

        private static string PrintTreeInternal(GreenNode tree, string indent, bool last)
        {
            var output = indent + "+- " + tree.Text.Trim() + " (" + tree.Kind + ") " + "\r\n";
            indent += last ? "   " : "|  ";

            var children = tree.Children.ToList();
            for (var i = 0; i < children.Count; i++)
            {
                output += PrintTreeInternal(children[i], indent, i == children.Count - 1);
            }

            return output;
        }

        public static string PrintTree(this GreenNode tree)
        {
            return PrintTreeInternal(tree, string.Empty, false);
        }
    }
}
