using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class FunctionCallExpressionSyntax : ExpressionSyntax
    {
        public FunctionCallExpressionSyntax(IdentifierSyntax identifier, SyntaxToken openParenToken, IReadOnlyList<FunctionCallExpressionParameterSyntax> parameters, SyntaxToken closeParenToken)
        {
            Identifier = identifier;
            OpenParenToken = openParenToken;
            Parameters = parameters;
            CloseParenToken = closeParenToken;
        }

        public override SyntaxKind Kind => SyntaxKind.FunctionCallExpression;
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

        public IdentifierSyntax Identifier { get; }
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<FunctionCallExpressionParameterSyntax> Parameters { get; }
        public SyntaxToken CloseParenToken { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return Identifier;
                yield return OpenParenToken;

                foreach (var parameter in Parameters)
                {
                    yield return parameter;
                }

                yield return CloseParenToken;
            }
        }
    }
}