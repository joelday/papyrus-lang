using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class StructHeaderSyntax : GreenNode
    {
        public StructHeaderSyntax(SyntaxToken structKeyword, IdentifierSyntax identifier)
        {
            StructKeyword = structKeyword;
            Identifier = identifier;
        }

        public override SyntaxKind Kind => SyntaxKind.StructHeader;
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

        public SyntaxToken StructKeyword { get; }
        public IdentifierSyntax Identifier { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return StructKeyword;
                yield return Identifier;
            }
        }
    }
}