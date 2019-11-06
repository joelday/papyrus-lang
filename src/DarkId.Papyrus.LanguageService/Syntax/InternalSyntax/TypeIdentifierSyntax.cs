using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class TypeIdentifierSyntax : IdentifierSyntax
    {
        public override SyntaxKind Kind => SyntaxKind.TypeIdentifier;

        public SyntaxToken ArrayToken { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                foreach (var child in base.ChildrenInternal)
                {
                    yield return child;
                }

                yield return ArrayToken;
            }
        }

        public override void Accept(IGreenNodeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T Accept<T>(IGreenNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

        public TypeIdentifierSyntax(ScriptToken token, SyntaxToken arrayToken, IEnumerable<ScriptToken> leadingTrivia = null, IEnumerable<ScriptToken> trailingTrivia = null) : base(token, leadingTrivia, trailingTrivia)
        {
            ArrayToken = arrayToken;
        }
    }
}