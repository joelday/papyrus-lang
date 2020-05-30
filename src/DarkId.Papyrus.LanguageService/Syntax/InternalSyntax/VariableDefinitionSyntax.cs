using System;
using System.Collections.Generic;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class VariableDefinitionSyntax : GreenNode
    {
        public VariableDefinitionSyntax(TypeIdentifierSyntax typeIdentifier, IdentifierSyntax identifier, SyntaxToken equalsToken, LiteralExpressionSyntax initialValue, IReadOnlyList<SyntaxToken> flags)
        {
            TypeIdentifier = typeIdentifier;
            Identifier = identifier;
            EqualsToken = equalsToken;
            InitialValue = initialValue;
            Flags = flags;
        }

        public override SyntaxKind Kind => SyntaxKind.VariableDefinition;

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return TypeIdentifier;
                yield return Identifier;
                yield return EqualsToken;
                yield return InitialValue;

                foreach (var flag in Flags)
                {
                    yield return flag;
                }
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
        public LiteralExpressionSyntax InitialValue { get; }
        public IReadOnlyList<SyntaxToken> Flags { get; }
    }
}