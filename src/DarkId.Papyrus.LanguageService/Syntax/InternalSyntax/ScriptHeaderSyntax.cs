using System;
using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService.Program;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class ScriptSyntax : GreenNode
    {
        public ScriptSyntax(ScriptHeaderSyntax header, IReadOnlyList<GreenNode> definitions)
        {
            Header = header;
            Definitions = definitions;
        }

        public override SyntaxKind Kind => SyntaxKind.Script;
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

        public ScriptHeaderSyntax Header { get; }
        public IReadOnlyList<GreenNode> Definitions { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return Header;

                foreach (var definition in Definitions)
                {
                    yield return definition;
                }
            }
        }
    }
}