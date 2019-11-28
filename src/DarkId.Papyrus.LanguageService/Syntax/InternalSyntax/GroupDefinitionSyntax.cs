using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class GroupDefinitionSyntax : GreenNode
    {
        public GroupDefinitionSyntax(GroupHeaderSyntax header, IReadOnlyList<PropertyDefinitionSyntax> definitions, SyntaxToken endGroupKeyword)
        {
            Header = header;
            Definitions = definitions;
            EndGroupKeyword = endGroupKeyword;
        }

        public override SyntaxKind Kind => SyntaxKind.GroupDefinition;
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

        public GroupHeaderSyntax Header { get; }
        public IReadOnlyList<PropertyDefinitionSyntax> Definitions { get; }
        public SyntaxToken EndGroupKeyword { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return Header;

                foreach (var definition in Definitions)
                {
                    yield return definition;
                }

                yield return EndGroupKeyword;
            }
        }
    }
}