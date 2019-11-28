using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class StateHeaderSyntax : GreenNode
    {
        public StateHeaderSyntax(SyntaxToken autoKeyword, SyntaxToken stateKeyword, IdentifierSyntax identifier)
        {
            AutoKeyword = autoKeyword;
            StateKeyword = stateKeyword;
            Identifier = identifier;
        }

        public override SyntaxKind Kind => SyntaxKind.StateHeader;
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

        public SyntaxToken AutoKeyword { get; }
        public SyntaxToken StateKeyword { get; }
        public IdentifierSyntax Identifier { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return AutoKeyword;
                yield return StateKeyword;
                yield return Identifier;
            }
        }
    }
}