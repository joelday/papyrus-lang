using System;
using System.Collections.Generic;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class ErrorSyntax : GreenNode
    {
        public override SyntaxKind Kind => SyntaxKind.Error;
        public SyntaxToken Token { get; }

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

        public ErrorSyntax(SyntaxToken token)
        {
            Token = token;
        }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return Token;
            }
        }
    }
}