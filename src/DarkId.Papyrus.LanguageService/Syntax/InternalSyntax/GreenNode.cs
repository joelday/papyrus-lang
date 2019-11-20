using System;
using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.Common;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal abstract class GreenNode
    {
        public abstract SyntaxKind Kind { get; }

        private readonly List<DiagnosticInfo> _diagnostics = new List<DiagnosticInfo>();

        protected GreenNode(List<GreenNode> leadingTriviaTokens = null, List<GreenNode> trailingTriviaTokens = null)
        {
            LeadingTriviaNodes = leadingTriviaTokens ?? new List<GreenNode>();
            TrailingTriviaTokens = trailingTriviaTokens ?? new List<GreenNode>();
        }

        public IReadOnlyList<DiagnosticInfo> Diagnostics => _diagnostics;

        public bool IsMissing { get; set; }

        public virtual string FullText => string.Join(string.Empty, Children.Select(c => c.FullText));
        public virtual string Text => FullText.Substring(LeadingTriviaWidth, Width);

        public virtual int FullWidth => FullText.Length;
        public virtual int Width => FullWidth - LeadingTriviaWidth - TrailingTriviaWidth;

        public virtual string LeadingTrivia
        {
            get
            {
                var leadingTrivia = LeadingTriviaNodes.Aggregate(string.Empty, (current, trivia) => current + trivia.FullText);

                foreach (var child in Children)
                {
                    leadingTrivia += child.LeadingTrivia;
                    if (!string.IsNullOrEmpty(child.Text))
                    {
                        break;
                    }
                }

                return leadingTrivia;
            }
        }

        public virtual string TrailingTrivia
        {
            get
            {
                var trailingTrivia = string.Empty;

                foreach (var child in Children.Reverse())
                {
                    trailingTrivia = child.TrailingTrivia + trailingTrivia;
                    if (!string.IsNullOrEmpty(child.Text))
                    {
                        break;
                    }
                }

                trailingTrivia += TrailingTriviaTokens.Aggregate(string.Empty, (current, trivia) => current + trivia.FullText);

                return trailingTrivia;
            }
        }

        public List<GreenNode> LeadingTriviaNodes { get; set; }
        public List<GreenNode> TrailingTriviaTokens { get; set; }

        public virtual int LeadingTriviaWidth => LeadingTrivia.Length;
        public virtual int TrailingTriviaWidth => TrailingTrivia.Length;

        protected abstract IEnumerable<GreenNode> ChildrenInternal { get; }

        public IEnumerable<GreenNode> Children
        {
            get
            {
                foreach (var trivia in LeadingTriviaNodes)
                {
                    yield return trivia;
                }

                foreach (var child in ChildrenInternal.WhereNotNull())
                {
                    yield return child;
                }

                foreach (var trivia in TrailingTriviaTokens)
                {
                    yield return trivia;
                }
            }
        }

        public abstract SyntaxNode CreateRed(SyntaxNode parent, int position);

        public abstract void Accept(IGreenNodeVisitor visitor);
        public abstract T Accept<T>(IGreenNodeVisitor<T> visitor);

        public void AddDiagnostic(DiagnosticInfo diagnosticInfo)
        {
            _diagnostics.Add(diagnosticInfo);
        }
    }
}