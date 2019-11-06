using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class MemberAccessExpressionSyntax : ExpressionSyntax
    {
        public MemberAccessExpressionSyntax(ExpressionSyntax baseExpression, SyntaxToken dotToken, ExpressionSyntax accessExpression)
        {
            BaseExpression = baseExpression;
            DotToken = dotToken;
            AccessExpression = accessExpression;
        }

        public override SyntaxKind Kind => SyntaxKind.MemberAccessExpression;
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

        public ExpressionSyntax BaseExpression { get; }
        public SyntaxToken DotToken { get; }
        public ExpressionSyntax AccessExpression { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return BaseExpression;
                yield return DotToken;
                yield return AccessExpression;
            }
        }
    }
}