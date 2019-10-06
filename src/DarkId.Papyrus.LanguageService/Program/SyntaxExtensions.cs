using System;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Program
{
    public static class SyntaxExtensions
    {
        public static ScriptFile GetScriptFile(this SyntaxNode node)
        {
            return node?.Script.File;
        }

        public static PapyrusProgram GetProgram(this SyntaxNode node)
        {
            return node?.Script.Program;
        }

        public static IEnumerable<SyntaxNode> GetDescendants(this SyntaxNode node, bool includeSelf = false)
        {
            if (includeSelf)
            {
                yield return node;
            }

            foreach (var child in node.Children)
            {
                yield return child;
                foreach (var subChild in GetDescendants(child))
                {
                    yield return subChild;
                }
            }
        }

        private static SyntaxNode GetNodeAtPositionInternal(IEnumerable<SyntaxNode> nodes, TextPosition position)
        {
            foreach (var node in nodes)
            {
                if (node.Range.Start <= position && node.Range.End >= position)
                {
                    return GetNodeAtPositionInternal(node.Children, position) ?? node;
                }
            }

            return null;
        }

        public static bool IsSelfOrDescendantOfNode(this SyntaxNode node, SyntaxNode selfOrAncestor)
        {
            return node == selfOrAncestor || node.GetAncestors().Contains(selfOrAncestor);
        }

        public static MemberAccessExpressionNode GetMemberAccessExpression(this SyntaxNode node)
        {
            foreach (var ancestor in node.GetAncestors(true))
            {
                if (ancestor is FunctionCallExpressionParameterNode)
                {
                    return null;
                }

                if (ancestor is MemberAccessExpressionNode asMemberAccess &&
                    !node.IsSelfOrDescendantOfNode(asMemberAccess.BaseExpression))
                {
                    return asMemberAccess;
                }
            }

            return null;
        }

        public static SyntaxNode GetDescendantNodeAtPosition(this SyntaxNode node, TextPosition position)
        {
            return GetNodeAtPositionInternal(node.Children, position);
        }

        public static SyntaxNode GetNodeAtPosition(this SyntaxNode node, TextPosition position)
        {
            return GetDescendantNodeAtPosition(node, position) ?? node;
        }

        public static IEnumerable<SyntaxNode> GetAncestors(this SyntaxNode node, bool includeSelf = false)
        {
            if (includeSelf)
            {
                yield return node;
            }

            var parent = node.Parent;
            while (parent != null)
            {
                yield return parent;
                parent = parent.Parent;
            }
        }

        public static IStatementBlock GetLocalStatementBlock(this SyntaxNode node)
        {
            return node.GetAncestors().OfType<IStatementBlock>().FirstOrDefault();
        }

        public static T GetDescendantNodeOfTypeAtPosition<T>(this SyntaxNode node, TextPosition position)
            where T : SyntaxNode
        {
            var descendant = GetNodeAtPositionInternal(node.Children, position);
            if (descendant == null)
            {
                return null;
            }

            return descendant as T ?? (T)descendant.GetAncestors().FirstOrDefault(a => a is T);
        }

        public static int GetFunctionParameterIndexAtPosition(this FunctionCallExpressionNode functionCallExpression, TextPosition position)
        {
            throw new NotImplementedException();

            //var intersectingParameterIndex = 0;

            //// Here, we're making the parameter node ranges contiguous
            //var parameterRanges = new List<TextRange>();
            //for (var i = 0; i < functionCallExpression.Parameters.Count; i++)
            //{
            //    var range = functionCallExpression.Parameters[i].Range;
            //    if (i > 0)
            //    {
            //        var previousParameterEnd = functionCallExpression.Parameters[i - 1].Range.End;

            //        range = new TextRange()
            //        {
            //            Start = new TextPosition()
            //            {
            //                Line = previousParameterEnd.Line,
            //                Character = previousParameterEnd.Character + 1
            //            },
            //            End = new TextPosition()
            //            {
            //                Line = range.End.Line,
            //                Character = range.End.Character + 1
            //            }
            //        };
            //    }

            //    parameterRanges.Add(range);
            //}

            //var intersectingRange = parameterRanges.LastOrDefault(r => position >= r.Start);
            //intersectingParameterIndex = parameterRanges.IndexOf(intersectingRange);

            //// If we're intersecting a named call parameter, we want to adjust the index to match the actual signature index.
            //var intersectingCallParameter = functionCallExpression.Parameters.ElementAtOrDefault(intersectingParameterIndex);
            //if (intersectingCallParameter != null)
            //{
            //    if (intersectingCallParameter.Identifier != null)
            //    {
            //        var parameterDefinition = intersectingCallParameter.GetParameterDefinition();
            //        return functionCallExpression.GetDefinition().Header.Parameters.IndexOf(parameterDefinition);
            //    }
            //}

            //if (intersectingParameterIndex == -1)
            //{
            //    intersectingParameterIndex = 0;
            //}

            //return intersectingParameterIndex;
        }

        public static string GetLeadingComments(this SyntaxNode node)
        {
            throw new NotImplementedException();

            //if (string.IsNullOrWhiteSpace(node.LeadingText))
            //{
            //    return string.Empty;
            //}

            //var text = node.LeadingText;
            //var leadingLines = text.Split('\n').Select(t => t.TrimEnd()).ToArray();
            //if (leadingLines.Length > 1 && string.IsNullOrWhiteSpace(leadingLines.Last()))
            //{
            //    leadingLines = leadingLines.Take(leadingLines.Count() - 1).ToArray();
            //}

            //var leadingCommentLines = leadingLines.Reverse().TakeWhile(t => t.StartsWith(";")).Reverse();
            //var trimmedCommentLines = leadingCommentLines.Select(t => t.Substring(t.StartsWith("; ") ? 2 : 1)).ToArray();

            //return trimmedCommentLines.Join("\n");
        }
    }
}