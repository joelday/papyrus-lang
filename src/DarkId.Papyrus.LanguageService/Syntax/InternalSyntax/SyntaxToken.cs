using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class SyntaxToken : GreenNode
    {
        private readonly ScriptToken _token;

        public SyntaxToken(SyntaxKind kind)
        {
            _token = new ScriptToken(kind, string.Empty);
            IsMissing = true;
        }

        public SyntaxToken(ScriptToken token, IEnumerable<ScriptToken> leadingTrivia = null, IEnumerable<ScriptToken> trailingTrivia = null)
        {
            _token = token;

            LeadingTriviaTokens = leadingTrivia ?? Enumerable.Empty<ScriptToken>();
            TrailingTriviaTokens = trailingTrivia ?? Enumerable.Empty<ScriptToken>();
        }

        public override bool IsMissing { get; }
        public override SyntaxKind Kind => _token.Kind;
        public override string Text => _token.Text;
        public override string FullText => LeadingTrivia + Text + TrailingTrivia;

        public IEnumerable<ScriptToken> LeadingTriviaTokens { get; }
        public IEnumerable<ScriptToken> TrailingTriviaTokens { get; }

        public override string LeadingTrivia => string.Join(string.Empty, LeadingTriviaTokens.Select(c => c.Text));
        public override string TrailingTrivia => string.Join(string.Empty, TrailingTriviaTokens.Select(c => c.Text));

        public override int FullWidth => FullText.Length;
        public override int Width => Text.Length;

        public override SyntaxNode CreateRed(SyntaxNode parent, int position)
        {
            throw new System.NotImplementedException();
        }

        public override void Accept(IGreenNodeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T Accept<T>(IGreenNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

        protected override IEnumerable<GreenNode> ChildrenInternal => Enumerable.Empty<GreenNode>();
    }
}