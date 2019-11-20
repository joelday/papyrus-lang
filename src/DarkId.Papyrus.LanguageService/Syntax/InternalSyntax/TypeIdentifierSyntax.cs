using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class TypeIdentifierSyntax : GreenNode
    {
        public override SyntaxKind Kind => SyntaxKind.TypeIdentifier;

        public IdentifierSyntax Identifier { get; }
        public SyntaxToken ArrayToken { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return Identifier;
                yield return ArrayToken;
            }
        }

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

        public TypeIdentifierSyntax(IdentifierSyntax identifier, SyntaxToken arrayToken)
        {
            Identifier = identifier;
            ArrayToken = arrayToken;
        }
    }
}