using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class ParenExpressionSyntax : ExpressionSyntax
    {
        public ParenExpressionSyntax(SyntaxToken openParenToken, ExpressionSyntax innerExpression, SyntaxToken closeParenToken)
        {
            OpenParenToken = openParenToken;
            InnerExpression = innerExpression;
            CloseParenToken = closeParenToken;
        }

        public override SyntaxKind Kind => SyntaxKind.ParenExpression;
        public override SyntaxNode CreateRed(SyntaxNode parent, int position)
        {
            throw new NotImplementedException();
        }

        public override void Accept(IGreenNodeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T Accept<T>(IGreenNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

        public SyntaxToken OpenParenToken { get; }
        public ExpressionSyntax InnerExpression { get; }
        public SyntaxToken CloseParenToken { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return OpenParenToken;
                yield return InnerExpression;
                yield return CloseParenToken;
            }
        }
    }
}