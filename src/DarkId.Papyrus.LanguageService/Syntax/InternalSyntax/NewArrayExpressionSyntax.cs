using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class NewArrayExpressionSyntax : ExpressionSyntax
    {
        public NewArrayExpressionSyntax(SyntaxToken newKeyword, TypeIdentifierSyntax typeIdentifier, SyntaxToken openBracketToken, ExpressionSyntax lengthExpression, SyntaxToken closeBracketToken)
        {
            NewKeyword = newKeyword;
            TypeIdentifier = typeIdentifier;
            OpenBracketToken = openBracketToken;
            LengthExpression = lengthExpression;
            CloseBracketToken = closeBracketToken;
        }

        public override SyntaxKind Kind => SyntaxKind.NewArrayExpression;
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

        public SyntaxToken NewKeyword { get; }
        public TypeIdentifierSyntax TypeIdentifier { get; }
        public SyntaxToken OpenBracketToken { get; }
        public ExpressionSyntax LengthExpression { get; }
        public SyntaxToken CloseBracketToken { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return NewKeyword;
                yield return TypeIdentifier;
                yield return OpenBracketToken;
                yield return LengthExpression;
                yield return CloseBracketToken;
            }
        }
    }
}