using System;
using System.Collections.Generic;
using System.Text;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax.Internal
{
    internal class TokenNode : SyntaxNode
    {
        private readonly ScriptToken _token;

        public TokenNode(in ScriptToken token)
        {
            _token = token;
        }

        public override SyntaxKind Kind => _token.Kind;

        public override int Width => Text.Length;
        public override int FullWidth => Width;
        public override string FullText => Text;
        public override string Text => _token.Text;

        public override string LeadingTrivia => string.Empty;
        public override string TrailingTrivia => string.Empty;

        public override Syntax.SyntaxNode CreateRed(Syntax.SyntaxNode parent, int position)
        {
            return new Syntax.TokenNode(this, parent, position);
        }
    }
}
