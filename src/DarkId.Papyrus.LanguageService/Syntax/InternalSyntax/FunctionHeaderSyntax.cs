using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class FunctionHeaderSyntax : GreenNode
    {
        public FunctionHeaderSyntax(TypeIdentifierSyntax typeIdentifier, SyntaxToken functionOrEventKeyword, ExpressionSyntax identifier, SyntaxToken openParen, IReadOnlyList<FunctionParameterSyntax> parameters, SyntaxToken closeParen, IReadOnlyList<SyntaxToken> flags)
        {
            TypeIdentifier = typeIdentifier;
            FunctionOrEventKeyword = functionOrEventKeyword;
            Identifier = identifier;
            OpenParen = openParen;
            Parameters = parameters;
            CloseParen = closeParen;
            Flags = flags;
        }

        public override SyntaxKind Kind => SyntaxKind.FunctionHeader;
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
        public SyntaxToken FunctionOrEventKeyword { get; }
        public ExpressionSyntax Identifier { get; }
        public SyntaxToken OpenParen { get; }
        public IReadOnlyList<FunctionParameterSyntax> Parameters { get; }
        public SyntaxToken CloseParen { get; }
        public IReadOnlyList<SyntaxToken> Flags { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return TypeIdentifier;
                yield return FunctionOrEventKeyword;
                yield return Identifier;
                yield return OpenParen;

                foreach (var parameter in Parameters)
                {
                    yield return parameter;
                }

                yield return CloseParen;

                foreach (var flag in Flags)
                {
                    yield return flag;
                }
            }
        }
    }
}