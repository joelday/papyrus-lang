using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    public abstract class SyntaxNode
    {
        internal Internal.SyntaxNode Node { get; }
        protected int Position { get; }

        public SyntaxKind Kind { get; }
        public SyntaxNode Parent { get; }

        public string Text => Node.Text;
        public string FullText => Node.FullText;
        public string LeadingTrivia => Node.LeadingTrivia;
        public string TrailingTrivia => Node.TrailingTrivia;
        public int Width => Node.Width;
        public int FullWidth => Node.FullWidth;

        public Range FullRange => new Range(Position, Position + FullWidth);
        public Range Range => new Range(Position + LeadingTrivia.Length, Position + LeadingTrivia.Length + Width);

        public bool IsMissing => Node.IsMissing;
        public bool IsTrivia => Kind.IsTrivia();

        internal SyntaxNode(Internal.SyntaxNode node, SyntaxNode parent, int position)
            : this(node.Kind, node, parent, position)
        {
        }

        internal SyntaxNode(SyntaxKind kind, Internal.SyntaxNode node, SyntaxNode parent, int position)
        {
            Kind = kind;
            Node = node;
            Parent = parent;
            Position = position;
        }

        public IEnumerable<SyntaxNode> EnumerateChildren()
        {
            var position = Position;
            foreach (var child in Node.Children)
            {
                yield return child.CreateRed(this, position);
                position += child.FullWidth;
            }
        }
    }
}
