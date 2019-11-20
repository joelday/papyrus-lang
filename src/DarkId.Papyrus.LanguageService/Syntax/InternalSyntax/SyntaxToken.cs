using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class SyntaxToken : GreenNode
    {
        public SyntaxToken(SyntaxKind kind, string text, List<GreenNode> leadingTrivia = null, List<GreenNode> trailingTrivia = null) : base(leadingTrivia, trailingTrivia)
        {
            Kind = kind;
            Text = text;
        }

        public override SyntaxKind Kind { get; }

        public override string Text { get; }
        public override string FullText => LeadingTrivia + Text + TrailingTrivia;

        public override SyntaxNode CreateRed(SyntaxNode parent, int position)
        {
            throw new System.NotImplementedException();
        }

        public override void Accept(IGreenNodeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T Accept<T>(IGreenNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

        protected override IEnumerable<GreenNode> ChildrenInternal => Enumerable.Empty<GreenNode>();
    }
}