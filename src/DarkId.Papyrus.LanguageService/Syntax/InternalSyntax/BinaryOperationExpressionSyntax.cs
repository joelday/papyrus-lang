using System;
using System.Collections.Generic;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class BinaryOperationExpressionSyntax : BinaryExpressionSyntax
    {
        public override SyntaxKind Kind => SyntaxKind.BinaryOperationExpression;
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

        public BinaryOperationExpressionSyntax(ExpressionSyntax left, SyntaxToken operatorToken, ExpressionSyntax right) : base(left, operatorToken, right)
        {
        }
    }
}