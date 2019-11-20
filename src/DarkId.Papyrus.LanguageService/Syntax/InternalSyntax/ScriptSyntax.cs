using System;
using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService.Program;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class ScriptHeaderSyntax : GreenNode
    {
        public ScriptHeaderSyntax(SyntaxToken scriptKeyword, IdentifierSyntax identifier, SyntaxToken extendsKeyword, TypeIdentifierSyntax typeIdentifier, IReadOnlyList<SyntaxToken> flags)
        {
            ScriptKeyword = scriptKeyword;
            Identifier = identifier;
            ExtendsKeyword = extendsKeyword;
            TypeIdentifier = typeIdentifier;
            Flags = flags;
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

        public SyntaxToken ScriptKeyword { get; }
        public IdentifierSyntax Identifier { get; }
        public SyntaxToken ExtendsKeyword { get; }
        public TypeIdentifierSyntax TypeIdentifier { get; }
        
        public IReadOnlyList<SyntaxToken> Flags { get; }

        protected override IEnumerable<GreenNode> ChildrenInternal
        {
            get
            {
                yield return ScriptKeyword;
                yield return Identifier;
                yield return ExtendsKeyword;
                yield return TypeIdentifier;

                foreach (var flag in Flags)
                {
                    yield return flag;
                }
            }
        }
    }
}