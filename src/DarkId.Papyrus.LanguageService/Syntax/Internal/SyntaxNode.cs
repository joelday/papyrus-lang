using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkId.Papyrus.LanguageService.Syntax.Internal
{
    internal abstract class SyntaxNode
    {
        public abstract SyntaxKind Kind { get; }

        public bool IsTrivia => Kind.IsTrivia();

        public virtual int Width => Children.Sum(c => c.Width);
        public virtual int FullWidth => Width + LeadingTrivia.Length + TrailingTrivia.Length;

        public bool IsMissing { get; set; }

        public List<SyntaxNode> Children { get; } = new List<SyntaxNode>();

        public virtual string LeadingTrivia
        {
            get { return string.Join(string.Empty, this.GetLeafNodes(false)
                .TakeWhile(t => t.IsTrivia).Select(t => t.FullText)); }
        }

        public virtual string TrailingTrivia
        {
            get { return string.Join(string.Empty, this.GetLeafNodes(false)
                .Reverse().TakeWhile(t => t.IsTrivia).Reverse().Select(t => t.FullText)); }
        }

        public virtual string FullText => string.Join(string.Empty, LeadingTrivia, Text, TrailingTrivia);
        public virtual string Text => string.Join(string.Empty, Children.Select(c => c.Text));

        public Syntax.SyntaxNode CreateRed()
        {
            return CreateRed(null, 0);
        }

        public abstract Syntax.SyntaxNode CreateRed(Syntax.SyntaxNode parent, int position);
    }
}
