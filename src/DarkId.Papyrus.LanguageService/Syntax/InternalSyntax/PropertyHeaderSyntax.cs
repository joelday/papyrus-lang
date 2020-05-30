using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class PropertyHeaderSyntax : GreenNode
    {
        public PropertyHeaderSyntax(TypeIdentifierSyntax typeIdentifier, SyntaxToken propertyKeyword, IdentifierSyntax identifier, SyntaxToken equalsToken, LiteralExpressionSyntax initialValue, IReadOnlyList<SyntaxToken> flags)
        {
            TypeIdentifier = typeIdentifier;
            PropertyKeyword = propertyKeyword;
            Identifier = identifier;
            Flags = flags;
        }

        public override SyntaxKind Kind => SyntaxKind.PropertyHeader;
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
        public SyntaxToken PropertyKeyword { get; }
        public IdentifierSyntax Identifier { get; }
        public SyntaxToken EqualsToken { get; }
        public LiteralExpressionSyntax InitialValue { get; }
        public IReadOnlyList<SyntaxToken> Flags { get; }
        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return TypeIdentifier;
                yield return PropertyKeyword;
                yield return Identifier;

                yield return EqualsToken;
                yield return InitialValue;

                foreach (var flag in Flags)
                {
                    yield return flag;
                }
            }
        }
    }
}