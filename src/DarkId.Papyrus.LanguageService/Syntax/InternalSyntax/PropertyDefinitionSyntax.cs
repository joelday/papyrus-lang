using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class PropertyDefinitionSyntax : GreenNode
    {
        public PropertyDefinitionSyntax(TypeIdentifierSyntax typeIdentifier, SyntaxToken propertyKeyword, IdentifierSyntax identifier, IReadOnlyList<SyntaxToken> flags, FunctionDefinitionSyntax getFunction, FunctionDefinitionSyntax setFunction, SyntaxToken endPropertyKeyword)
        {
            TypeIdentifier = typeIdentifier;
            PropertyKeyword = propertyKeyword;
            Identifier = identifier;
            Flags = flags;
            GetFunction = getFunction;
            SetFunction = setFunction;
            EndPropertyKeyword = endPropertyKeyword;
        }

        public override SyntaxKind Kind => SyntaxKind.PropertyDefinition;
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
        public IReadOnlyList<SyntaxToken> Flags { get; }

        public FunctionDefinitionSyntax GetFunction { get; }
        public FunctionDefinitionSyntax SetFunction { get; }

        public SyntaxToken EndPropertyKeyword { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return TypeIdentifier;
                yield return PropertyKeyword;
                yield return Identifier;

                foreach (var flag in Flags)
                {
                    yield return flag;
                }

                yield return GetFunction;
                yield return SetFunction;

                yield return EndPropertyKeyword;
            }
        }
    }
}