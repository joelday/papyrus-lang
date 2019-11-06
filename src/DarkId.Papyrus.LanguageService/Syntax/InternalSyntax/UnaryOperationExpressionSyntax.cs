using System;
using System.Collections.Generic;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class UnaryOperationExpressionSyntax : ExpressionSyntax
    {
        public UnaryOperationExpressionSyntax(SyntaxToken operatorToken, ExpressionSyntax innerExpression)
        {
            OperatorToken = operatorToken;
            InnerExpression = innerExpression;
        }

        public override SyntaxKind Kind => SyntaxKind.UnaryOperationExpression;


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

        public SyntaxToken OperatorToken { get; }
        public ExpressionSyntax InnerExpression { get; }
        
        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return OperatorToken;
                yield return InnerExpression;
            }
        }
    }
}