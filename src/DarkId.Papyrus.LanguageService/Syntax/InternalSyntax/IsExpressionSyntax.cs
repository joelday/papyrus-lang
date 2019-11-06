using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class IsExpressionSyntax : ExpressionSyntax
    {
        public IsExpressionSyntax(ExpressionSyntax innerExpression, SyntaxToken isKeyword, TypeIdentifierSyntax typeIdentifier)
        {
            InnerExpression = innerExpression;
            IsKeyword = isKeyword;
            TypeIdentifier = typeIdentifier;
        }

        public override SyntaxKind Kind => SyntaxKind.IsExpression;
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

        public ExpressionSyntax InnerExpression { get; }
        public SyntaxToken IsKeyword { get; }
        public TypeIdentifierSyntax TypeIdentifier { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return InnerExpression;
                yield return IsKeyword;
                yield return TypeIdentifier;
            }
        }
    }
}