using System;
using System.Collections.Generic;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class AssignmentStatementSyntax : BinaryExpressionSyntax
    {
        public override SyntaxKind Kind => SyntaxKind.AssignmentStatement;
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

        public AssignmentStatementSyntax(ExpressionSyntax left, SyntaxToken operatorToken, ExpressionSyntax right) : base(left, operatorToken, right)
        {
        }
    }
}