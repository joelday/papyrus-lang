using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class LiteralExpressionSyntax : ExpressionSyntax
    {
        public override SyntaxKind Kind => SyntaxKind.LiteralExpression;
        public override SyntaxNode CreateRed(SyntaxNode parent, int position)
        {
            throw new NotImplementedException();
        }

        public SyntaxToken LeadingNegative { get; }
        public SyntaxToken Value { get; }
        public LiteralExpressionSyntax(SyntaxToken leadingNegative, SyntaxToken value)
        {
            LeadingNegative = leadingNegative;
            Value = value;
        }

        public override void Accept(IGreenNodeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T Accept<T>(IGreenNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return LeadingNegative;
                yield return Value;
            }
        }
    }
}