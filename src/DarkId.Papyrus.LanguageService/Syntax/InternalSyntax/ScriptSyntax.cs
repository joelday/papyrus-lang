using System;
using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService.Program;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal class ScriptSyntax : GreenNode
    {
        public ScriptSyntax(ScriptFile file, PapyrusProgram program, SyntaxToken scriptKeyword, IdentifierSyntax identifier, SyntaxToken extendsKeyword, TypeIdentifierSyntax typeIdentifier, IReadOnlyList<SyntaxToken> flags, IReadOnlyList<GreenNode> definitions)
        {
            File = file;
            Program = program;

            ScriptKeyword = scriptKeyword;
            Identifier = identifier;
            ExtendsKeyword = extendsKeyword;
            TypeIdentifier = typeIdentifier;
            Flags = flags;
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

        public ScriptFile File { get; }
        public PapyrusProgram Program { get; }

        public SyntaxToken ScriptKeyword { get; }
        public IdentifierSyntax Identifier { get; }
        public SyntaxToken ExtendsKeyword { get; }
        public TypeIdentifierSyntax TypeIdentifier { get; }
        
        public IReadOnlyList<SyntaxToken> Flags { get; }
        public IReadOnlyList<GreenNode> Definitions { get; }

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

                foreach (var definition in Definitions)
                {
                    yield return definition;
                }
            }
        }
    }
}