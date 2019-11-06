using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class DeclareStatementSyntax : GreenNode
    {
        public DeclareStatementSyntax(TypeIdentifierSyntax typeIdentifier, IdentifierSyntax identifier, ExpressionSyntax initialValue)
        {
            TypeIdentifier = typeIdentifier;
            Identifier = identifier;
            InitialValue = initialValue;
        }

        public override SyntaxKind Kind => SyntaxKind.DeclareStatement;
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
        public TypeIdentifierSyntax TypeIdentifier { get; }
        public ExpressionSyntax InitialValue { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return Identifier;
                yield return TypeIdentifier;
                yield return InitialValue;
            }
        }
    }
}