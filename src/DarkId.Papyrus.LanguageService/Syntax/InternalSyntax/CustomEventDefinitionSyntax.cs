using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class CustomEventDefinitionSyntax : GreenNode
    {
        public CustomEventDefinitionSyntax(SyntaxToken customEventKeyword, IdentifierSyntax identifier)
        {
            CustomEventKeyword = customEventKeyword;
            Identifier = identifier;
        }

        public override SyntaxKind Kind => SyntaxKind.CustomEventDefinition;

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return CustomEventKeyword;
                yield return Identifier;
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

        public SyntaxToken CustomEventKeyword { get; }
        public IdentifierSyntax Identifier { get; }
    }
}