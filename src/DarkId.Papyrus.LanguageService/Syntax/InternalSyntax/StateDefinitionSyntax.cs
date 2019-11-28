using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class StateDefinitionSyntax : GreenNode
    {
        public StateDefinitionSyntax(StateHeaderSyntax header, IReadOnlyList<GreenNode> definitions, SyntaxToken endStateKeyword)
        {
            Header = header;
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

        public StateHeaderSyntax Header { get; }
        public IReadOnlyList<GreenNode> Definitions { get; }
        public SyntaxToken EndStateKeyword { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return Header;

                foreach (var definition in Definitions)
                {
                    yield return definition;
                }

                yield return EndStateKeyword;
            }
        }
    }
}