using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class UnknownSyntax : GreenNode
    {
        public UnknownSyntax(SyntaxToken unknown)
        {
            Unknown = unknown;
        }

        public override SyntaxKind Kind => SyntaxKind.Unknown;
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

        public SyntaxToken Unknown { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return Unknown;
            }
        }
    }
}