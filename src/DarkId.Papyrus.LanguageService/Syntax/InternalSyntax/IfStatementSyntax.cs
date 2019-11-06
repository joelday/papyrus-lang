using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class IfStatementSyntax : GreenNode
    {
        public IfStatementSyntax(IReadOnlyList<IfStatementBodySyntax> bodies, SyntaxToken endIfKeyword)
        {
            Bodies = bodies;
            EndIfKeyword = endIfKeyword;
        }

        public override SyntaxKind Kind => SyntaxKind.IfStatement;
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

        public IReadOnlyList<IfStatementBodySyntax> Bodies { get; }
        public SyntaxToken EndIfKeyword { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                foreach (var body in Bodies)
                {
                    yield return body;
                }

                yield return EndIfKeyword;
            }
        }
    }
}