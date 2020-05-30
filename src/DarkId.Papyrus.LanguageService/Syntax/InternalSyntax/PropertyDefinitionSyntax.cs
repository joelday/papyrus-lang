using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class PropertyDefinitionSyntax : GreenNode
    {
        public PropertyDefinitionSyntax(PropertyHeaderSyntax header, IReadOnlyList<GreenNode> accessors, SyntaxToken endPropertyKeyword)
        {
            Header = header;
            Accessors = accessors;
            EndPropertyKeyword = endPropertyKeyword;
        }

        public override SyntaxKind Kind => SyntaxKind.PropertyDefinition;
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

        public PropertyHeaderSyntax Header { get; }

        public IReadOnlyList<GreenNode> Accessors { get; }

        public SyntaxToken EndPropertyKeyword { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return Header;

                foreach (var accessor in Accessors)
                {
                    yield return accessor;
                }

                yield return EndPropertyKeyword;
            }
        }
    }
}