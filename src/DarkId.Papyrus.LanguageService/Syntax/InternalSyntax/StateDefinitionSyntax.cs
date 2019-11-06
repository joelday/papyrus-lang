using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class StateDefinitionSyntax : GreenNode
    {
        public StateDefinitionSyntax(SyntaxToken stateKeyword, IdentifierSyntax identifier, SyntaxToken autoKeyword, IReadOnlyList<GreenNode> definitions, SyntaxToken endStateKeyword)
        {
            StateKeyword = stateKeyword;
            Identifier = identifier;
            AutoKeyword = autoKeyword;
            Definitions = definitions;
            EndStateKeyword = endStateKeyword;
        }

        public override SyntaxKind Kind => SyntaxKind.StateDefinition;
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

        public SyntaxToken StateKeyword { get; }
        public IdentifierSyntax Identifier { get; }
        public SyntaxToken AutoKeyword { get; }
        public IReadOnlyList<GreenNode> Definitions { get; }
        public SyntaxToken EndStateKeyword { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal => Enumerable.Empty<GreenNode>();
    }
}