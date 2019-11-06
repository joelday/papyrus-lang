using System;
using System.Collections.Generic;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class IdentifierSyntax : SyntaxToken
    {
        public override SyntaxKind Kind => SyntaxKind.Identifier;

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

        public IdentifierSyntax(ScriptToken token, IEnumerable<ScriptToken> leadingTrivia = null, IEnumerable<ScriptToken> trailingTrivia = null) : base(token, leadingTrivia, trailingTrivia)
        {
        }
    }
}