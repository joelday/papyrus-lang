using System;
using System.Collections.Generic;
using DarkId.Papyrus.LanguageService.Syntax.Legacy;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class FunctionParameterSyntax : GreenNode
    {
        public FunctionParameterSyntax(TypeIdentifierSyntax typeIdentifier, IdentifierSyntax identifier, SyntaxToken equalsToken, LiteralExpressionSyntax defaultValue, SyntaxToken trailingComma)
        {
            TypeIdentifier = typeIdentifier;
            Identifier = identifier;
            EqualsToken = equalsToken;
            DefaultValue = defaultValue;
            TrailingComma = trailingComma;
        }

        public override SyntaxKind Kind => SyntaxKind.FunctionParameter;

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return TypeIdentifier;
                yield return Identifier;
                yield return EqualsToken;
                yield return DefaultValue;
                yield return TrailingComma;
            }
        }

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

        public TypeIdentifierSyntax TypeIdentifier { get; }
        public IdentifierSyntax Identifier { get; }
        public SyntaxToken EqualsToken { get; }
        public LiteralExpressionSyntax DefaultValue { get; }
        public SyntaxToken TrailingComma { get; }
    }
}