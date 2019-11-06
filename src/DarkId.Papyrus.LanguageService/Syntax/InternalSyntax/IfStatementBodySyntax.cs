using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class IfStatementBodySyntax : GreenNode
    {
        public IfStatementBodySyntax(SyntaxToken ifOrElseKeyword, ExpressionSyntax condition, IReadOnlyList<GreenNode> statements)
        {
            IfOrElseKeyword = ifOrElseKeyword;
            Condition = condition;
            Statements = statements;
        }

        public override SyntaxKind Kind => SyntaxKind.IfStatementBody;
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

        public SyntaxToken IfOrElseKeyword { get; }
        public ExpressionSyntax Condition { get; }
        public IReadOnlyList<GreenNode> Statements { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return IfOrElseKeyword;
                yield return Condition;

                foreach (var statement in Statements)
                {
                    yield return statement;
                }
            }
        }
    }
}