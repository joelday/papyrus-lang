using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class FunctionDefinitionSyntax : GreenNode
    {
        public FunctionDefinitionSyntax(FunctionHeaderSyntax functionHeader, IEnumerable<GreenNode> statements, SyntaxToken endFunctionOrEventKeyword)
        {
            Header = functionHeader;
            Statements = statements;
            EndFunctionOrEventKeyword = endFunctionOrEventKeyword;
        }

        public override SyntaxKind Kind => SyntaxKind.FunctionDefinition;
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

        public FunctionHeaderSyntax Header { get; }
        public IEnumerable<GreenNode> Statements { get; }
        public SyntaxToken EndFunctionOrEventKeyword { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return Header;

                foreach (var statement in Statements)
                {
                    yield return statement;
                }

                yield return EndFunctionOrEventKeyword;
            }
        }
    }
}