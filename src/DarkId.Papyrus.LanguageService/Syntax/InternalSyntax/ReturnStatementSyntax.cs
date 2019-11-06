using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class ReturnStatementSyntax : GreenNode
    {
        public ReturnStatementSyntax(SyntaxToken returnKeyword, ExpressionSyntax returnValue)
        {
            ReturnKeyword = returnKeyword;
            ReturnValue = returnValue;
        }

        public override SyntaxKind Kind => SyntaxKind.ReturnStatement;
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

        public SyntaxToken ReturnKeyword { get; }
        public ExpressionSyntax ReturnValue { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return ReturnKeyword;
                yield return ReturnValue;
            }
        }
    }
}