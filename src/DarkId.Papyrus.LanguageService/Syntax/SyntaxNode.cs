using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using DarkId.Papyrus.LanguageService.Syntax.InternalSyntax;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    public abstract class SyntaxNode
    {
        internal GreenNode Green { get; }

        private readonly Lazy<IReadOnlyList<SyntaxNode>> _children;

        public virtual SyntaxKind Kind => Green.Kind;
        public SyntaxNode Parent { get; }

        public string Text { get; }
        public string FullText { get; }
        public string LeadingTrivia { get; }
        public string TrailingTrivia { get; }

        public Range FullRange { get; }
        public Range Range { get; }

        internal SyntaxNode(SyntaxNode parent, GreenNode green, int position)
        {
            Parent = parent;
            Green = green;

            LeadingTrivia = Green.LeadingTrivia;
            TrailingTrivia = Green.TrailingTrivia;

            FullRange =  new Range(position, position + Green.FullWidth);
            Range = new Range(position + LeadingTrivia.Length, position + LeadingTrivia.Length + Green.Width);

            FullText = Green.FullText;
            Text = FullText.Substring(Range.Start.Value - position, Green.Width);

            _children = new Lazy<IReadOnlyList<SyntaxNode>>(() =>
            {
                var list = new List<SyntaxNode>();
                var childPosition = position;

                foreach (var child in Green.Children)
                {
                    list.Add(child.CreateRed(this, childPosition));
                    childPosition += child.FullWidth;
                }

                return list.ToImmutableArray();
            });
        }

        public IReadOnlyList<Diagnostic> Diagnostics =>
            Green.Diagnostics.Select(d => new Diagnostic(d, Range)).ToList();

        public IReadOnlyList<SyntaxNode> Children => _children.Value;
    }
}
