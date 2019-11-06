using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class StructDefinitionSyntax : GreenNode
    {
        public StructDefinitionSyntax(SyntaxToken structKeyword, IdentifierSyntax identifier, IEnumerable<GreenNode> definitions, SyntaxToken endStructKeyword)
        {
            StructKeyword = structKeyword;
            Identifier = identifier;
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

        public SyntaxToken StructKeyword { get; }
        public IdentifierSyntax Identifier { get; }

        public IEnumerable<GreenNode> Definitions { get;  }

        public SyntaxToken EndStructKeyword { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return StructKeyword;
                yield return Identifier;

                foreach (var definition in Definitions)
                {
                    yield return definition;
                }

                yield return EndStructKeyword;
            }
        }
    }
}