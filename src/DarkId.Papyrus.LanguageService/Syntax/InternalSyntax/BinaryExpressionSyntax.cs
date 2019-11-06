using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal abstract class BinaryExpressionSyntax<TLeft, TRight> : ExpressionSyntax
        where TLeft : GreenNode
        where TRight : GreenNode
    {
        public TLeft Left { get; }
        public SyntaxToken OperatorToken { get; }
        public TRight Right { get; }

        protected BinaryExpressionSyntax(TLeft left, SyntaxToken operatorToken, TRight right)
        {
            Left = left;
            OperatorToken = operatorToken;
            Right = right;
        }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return Left;
                yield return OperatorToken;
                yield return Right;
            }
        }
    }

    internal abstract class BinaryExpressionSyntax : BinaryExpressionSyntax<ExpressionSyntax, ExpressionSyntax>
    {
        protected BinaryExpressionSyntax(ExpressionSyntax left, SyntaxToken operatorToken, ExpressionSyntax right) : base(left, operatorToken, right)
        {
        }

        public override void Accept(IGreenNodeVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
