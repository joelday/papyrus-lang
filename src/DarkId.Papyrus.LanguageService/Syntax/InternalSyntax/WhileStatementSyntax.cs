using System;
using System.Collections.Generic;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class WhileStatementSyntax : GreenNode
    {
        public WhileStatementSyntax(SyntaxToken whileKeyword, ExpressionSyntax expression, IEnumerable<GreenNode> statements, SyntaxToken endWhileKeyword)
        {
            WhileKeyword = whileKeyword;
            Expression = expression;
            Statements = statements;
            EndWhileKeyword = endWhileKeyword;
        }

        public override SyntaxKind Kind => SyntaxKind.WhileStatement;

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

        public SyntaxToken WhileKeyword { get; }
        public ExpressionSyntax Expression { get; }
        public IEnumerable<GreenNode> Statements { get; }
        public SyntaxToken EndWhileKeyword { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return WhileKeyword;
                yield return Expression;

                foreach (var statement in Statements)
                {
                    yield return statement;
                }

                yield return EndWhileKeyword;
            }
        }
    }
}