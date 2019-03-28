using Antlr.Runtime;
using Antlr.Runtime.Tree;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.External;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Program
{
    static class AntlrExtensions
    {
        public static IEnumerable<CommonTree> GetAncestors(this CommonTree node)
        {
            var parent = node.Parent as CommonTree;
            while (parent != null)
            {
                yield return parent;
                parent = parent.Parent as CommonTree;
            }
        }

        public static IEnumerable<CommonTree> GetDescendants(this CommonTree node)
        {
            foreach (var child in node.GetChildren())
            {
                yield return child;
                foreach (var subChild in GetDescendants(child))
                {
                    yield return subChild;
                }
            }
        }

        public static int GetIndexForTypeInParent(this CommonTree node)
        {
            if (node.Parent == null)
            {
                return -1;
            }

            var parent = (CommonTree)node.Parent;
            return parent.GetChildren().Where(c => c.Type == node.Type).IndexOf(node);
        }

        public static string GetLocalNodeId(this CommonTree node)
        {
            return (node.Parent != null ? $"[{node.GetIndexForTypeInParent()}]" : string.Empty) + $"({node.Type}){node.ToString()}";
        }

        public static string GetFullNodeId(this CommonTree node)
        {
            return string.Join(".", GetAncestors(node).Select(a => a.GetLocalNodeId()).Reverse()) + "." + node.GetLocalNodeId();
        }

        public static Range GetRange(this CommonTree node, ITokenStream tokenStream, IReadOnlyScriptText scriptText)
        {
            if (node is CommonErrorNode errorNode)
            {
                return new Range()
                {
                    Start = scriptText.PositionAt(((CommonToken)tokenStream.Get(errorNode.start.TokenIndex)).StartIndex),
                    End = scriptText.PositionAt(((CommonToken)tokenStream.Get(errorNode.stop.TokenIndex)).StopIndex + 1)
                };
            }

            if (node.TokenStartIndex == -1 || node.TokenStopIndex == -1)
            {
                return Range.Empty;
            }

            return new Range()
            {
                Start = scriptText.PositionAt(((CommonToken)tokenStream.Get(node.TokenStartIndex)).StartIndex),
                End = scriptText.PositionAt(((CommonToken)tokenStream.Get(node.TokenStopIndex)).StopIndex + 1)
            };
        }

        public static IEnumerable<CommonTree> GetChildren(this CommonTree node)
        {
            for (var i = 0; i < node.ChildCount; i++)
            {
                yield return (CommonTree)node.GetChild(i);
            }
        }

        public static AstType PeekType(this Scanner<CommonTree> scanner)
        {
            var next = scanner.Peek();
            if (next != null)
            {
                return next.GetAstType();
            }

            return AstType.Invalid;
        }
    }
}