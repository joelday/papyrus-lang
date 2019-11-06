using System;
using System.Collections.Generic;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class CastExpressionSyntax : BinaryExpressionSyntax<ExpressionSyntax, TypeIdentifierSyntax>
    {
        public override SyntaxKind Kind => SyntaxKind.CastExpression;

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

        public CastExpressionSyntax(ExpressionSyntax left, SyntaxToken operatorToken, TypeIdentifierSyntax right) : base(left, operatorToken, right)
        {
        }
    }
}