using System;
using System.Collections.Generic;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class FunctionCallExpressionParameterSyntax : ExpressionSyntax
    {
        public FunctionCallExpressionParameterSyntax(IdentifierSyntax identifier, SyntaxToken openParenToken, ExpressionSyntax value, SyntaxToken closeParenToken)
        {
            Identifier = identifier;
            OpenParenToken = openParenToken;
            Value = value;
            CloseParenToken = closeParenToken;
        }

        public override void Accept(IGreenNodeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T Accept<T>(IGreenNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

        public override SyntaxKind Kind => SyntaxKind.FunctionCallExpressionParameter;

        public override SyntaxNode CreateRed(SyntaxNode parent, int position)
        {
            throw new NotImplementedException();
        }

        public IdentifierSyntax Identifier { get; }
        public SyntaxToken OpenParenToken { get; }
        public ExpressionSyntax Value { get; }
        public SyntaxToken CloseParenToken { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return Identifier;
                yield return OpenParenToken;
                yield return Value;
                yield return CloseParenToken;
            }
        }
    }
}