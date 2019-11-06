using System;
using System.Collections.Generic;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class ArrayIndexExpressionSyntax : ExpressionSyntax
    {
        public ArrayIndexExpressionSyntax(ExpressionSyntax arrayExpression, SyntaxToken openBracketToken, ExpressionSyntax indexExpression, SyntaxToken closeBracketToken)
        {
            ArrayExpression = arrayExpression;
            OpenBracketToken = openBracketToken;
            IndexExpression = indexExpression;
            CloseBracketToken = closeBracketToken;
        }

        public override SyntaxKind Kind => SyntaxKind.ArrayIndexExpression;

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

        public ExpressionSyntax ArrayExpression { get; }
        public SyntaxToken OpenBracketToken { get; }
        public ExpressionSyntax IndexExpression { get; }
        public SyntaxToken CloseBracketToken { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return ArrayExpression;
                yield return OpenBracketToken;
                yield return IndexExpression;
                yield return CloseBracketToken;
            }
        }
    }
}