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
        public IReadOnlyList<DiagnosticInfo> Diagnostics => _diagnostics;

        public virtual string FullText => string.Join(string.Empty, Children.Select(c => c.FullText));
        public virtual string Text => FullText.Substring(LeadingTriviaWidth, Width);

        public virtual int FullWidth => FullText.Length;
        public virtual int Width => FullWidth - LeadingTriviaWidth - TrailingTriviaWidth;

        public virtual string LeadingTrivia
        {
            get
            {
                var node = Children.FirstOrDefault();
                while (node != null)
                {
                    if (!node.Children.Any() && node.LeadingTriviaWidth > 0)
                    {
                        return node.LeadingTrivia;
                    }

                    node = node.Children.FirstOrDefault();
                }

                return string.Empty;
            }
        }

        public virtual string TrailingTrivia
        {
            get
            {
                var node = Children.LastOrDefault();
                while (node != null)
                {
                    if (!node.Children.Any() && node.TrailingTriviaWidth > 0)
                    {
                        return node.TrailingTrivia;
                    }

                    node = node.Children.LastOrDefault();
                }

                return string.Empty;
            }
        }

        public virtual int LeadingTriviaWidth => LeadingTrivia.Length;
        public virtual int TrailingTriviaWidth => TrailingTrivia.Length;

        protected abstract IEnumerable<GreenNode> ChildrenInternal { get; }

        public IEnumerable<GreenNode> Children => ChildrenInternal.WhereNotNull().ToList();

        public abstract SyntaxNode CreateRed(SyntaxNode parent, int position);

        public abstract void Accept(IGreenNodeVisitor visitor);
        public abstract T Accept<T>(IGreenNodeVisitor<T> visitor);

        public void AddDiagnostic(DiagnosticInfo diagnosticInfo)
        {
            _diagnostics.Add(diagnosticInfo);
        }
    }
}