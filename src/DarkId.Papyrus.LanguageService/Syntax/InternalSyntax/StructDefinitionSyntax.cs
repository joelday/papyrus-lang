using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class StructDefinitionSyntax : GreenNode
    {
        public StructDefinitionSyntax(StructHeaderSyntax header, IEnumerable<GreenNode> definitions, SyntaxToken endStructKeyword)
        {
            Header = header;
            Definitions = definitions;
            EndStructKeyword = endStructKeyword;
        }

        public override SyntaxKind Kind => SyntaxKind.StructDefinition;
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

        public StructHeaderSyntax Header { get; }

        public IEnumerable<GreenNode> Definitions { get; }

        public SyntaxToken EndStructKeyword { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return Header;

                foreach (var definition in Definitions)
                {
                    yield return definition;
                }

                yield return EndStructKeyword;
            }
        }
    }
}