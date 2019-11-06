using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class ImportSyntax : GreenNode
    {
        public override SyntaxKind Kind => SyntaxKind.Import;
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

        public SyntaxToken ImportKeyword { get; set; }
        public IdentifierSyntax Identifier { get; set; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return ImportKeyword;
                yield return Identifier;
            }
        }
    }
}