using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class EventDefinitionSyntax : FunctionDefinitionSyntax
    {
        public override SyntaxKind Kind => SyntaxKind.EventDefinition;

        public override void Accept(IGreenNodeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T Accept<T>(IGreenNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

        public EventDefinitionSyntax(FunctionHeaderSyntax functionHeader, IEnumerable<GreenNode> statements, SyntaxToken endFunctionOrEventKeyword) : base(functionHeader, statements, endFunctionOrEventKeyword)
        {
        }
    }
}