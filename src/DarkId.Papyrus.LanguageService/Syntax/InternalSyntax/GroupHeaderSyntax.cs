using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class GroupHeaderSyntax : GreenNode
    {
        public GroupHeaderSyntax(SyntaxToken groupKeyword, IdentifierSyntax identifier)
        {
            GroupKeyword = groupKeyword;
            Identifier = identifier;
        }

        public override SyntaxKind Kind => SyntaxKind.GroupDefinition;
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

        public SyntaxToken GroupKeyword { get; }
        public IdentifierSyntax Identifier { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return GroupKeyword;
                yield return Identifier;
            }
        }
    }
}