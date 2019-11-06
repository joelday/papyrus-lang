using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class FunctionDefinitionSyntax : GreenNode
    {
        public FunctionDefinitionSyntax(TypeIdentifierSyntax typeIdentifier, SyntaxToken functionorEventKeyword, ExpressionSyntax identifier, SyntaxToken openParen, IReadOnlyList<FunctionParameterSyntax> parameters, SyntaxToken closeParen, IEnumerable<GreenNode> statements, SyntaxToken endFunctionOrEventKeyword)
        {
            TypeIdentifier = typeIdentifier;
            FunctionorEventKeyword = functionorEventKeyword;
            Identifier = identifier;
            OpenParen = openParen;
            Parameters = parameters;
            CloseParen = closeParen;
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

        public TypeIdentifierSyntax TypeIdentifier { get; }
        public SyntaxToken FunctionorEventKeyword { get; }
        public ExpressionSyntax Identifier { get; }
        public SyntaxToken OpenParen { get; }
        public IReadOnlyList<FunctionParameterSyntax> Parameters { get; }
        public SyntaxToken CloseParen { get; }
        public IEnumerable<GreenNode> Statements { get; }
        public SyntaxToken EndFunctionOrEventKeyword { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return TypeIdentifier;
                yield return FunctionorEventKeyword;
                yield return Identifier;
                yield return OpenParen;

                foreach (var parameter in Parameters)
                {
                    yield return parameter;
                }

                yield return CloseParen;

                foreach (var statement in Statements)
                {
                    yield return statement;
                }

                yield return EndFunctionOrEventKeyword;
            }
        }
    }
}