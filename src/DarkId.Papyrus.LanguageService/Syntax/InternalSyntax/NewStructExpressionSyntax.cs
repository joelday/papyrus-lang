using System;
using System.Collections.Generic;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class NewStructExpressionSyntax : ExpressionSyntax
    {
        public NewStructExpressionSyntax(SyntaxToken newKeyword, IdentifierSyntax typeIdentifier, SyntaxToken openBracketToken, LiteralExpressionSyntax size, SyntaxToken closeBracketToken)
        {
            NewKeyword = newKeyword;
            TypeIdentifier = typeIdentifier;
            OpenBracketToken = openBracketToken;
            Size = size;
            CloseBracketToken = closeBracketToken;
        }

        public override SyntaxKind Kind => SyntaxKind.NewStructExpression;

        public SyntaxToken NewKeyword { get; }
        public IdentifierSyntax TypeIdentifier { get; }
        public SyntaxToken OpenBracketToken { get; }
        public LiteralExpressionSyntax Size { get; }
        public SyntaxToken CloseBracketToken { get; }

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

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return NewKeyword;
                yield return TypeIdentifier;
                yield return OpenBracketToken;
                yield return Size;
                yield return CloseBracketToken;
            }
        }
    }
}