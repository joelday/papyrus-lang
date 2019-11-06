using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class GroupDefinitionSyntax : GreenNode
    {
        public GroupDefinitionSyntax(SyntaxToken groupKeyword, IdentifierSyntax identifier, IReadOnlyList<PropertyDefinitionSyntax> definitions, SyntaxToken endGroupKeyword)
        {
            GroupKeyword = groupKeyword;
            Identifier = identifier;
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

        public SyntaxToken GroupKeyword { get; }
        public IdentifierSyntax Identifier { get; }
        public IReadOnlyList<PropertyDefinitionSyntax> Definitions { get; }
        public SyntaxToken EndGroupKeyword { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return GroupKeyword;
                yield return Identifier;

                foreach (var definition in Definitions)
                {
                    yield return definition;
                }

                yield return EndGroupKeyword;
            }
        }
    }
}