namespace Papyrus.CodeAnalysis.Syntax;

internal abstract partial class NameSyntax : SyntaxNode
{
    private protected NameSyntax(GreenNode green, SyntaxNode? parent, int position)
        : base(green, parent, position)
    {
    }
}

[SyntaxNode]
internal sealed partial class IdentifierNameSyntax : NameSyntax
{
    public SyntaxToken Identifier { get; }
}

[SyntaxNode]
internal sealed partial class QualifiedNameSyntax : NameSyntax
{
    public NameSyntax Left { get; } = null!;
    public SyntaxToken ColonToken { get; }
    public IdentifierNameSyntax Right { get; } = null!;
}

[SyntaxNode]
internal sealed partial class TypeNameSyntax : SyntaxNode
{
    public NameSyntax Name { get; } = null!;
    public SyntaxToken HashToken { get; }
    public SyntaxToken StructName { get; }
    public SyntaxToken OpenBracketToken { get; }
    public SyntaxToken CloseBracketToken { get; }
}

[SyntaxNode]
internal sealed partial class ExtendsClauseSyntax : SyntaxNode
{
    public SyntaxToken ExtendsKeyword { get; }
    public TypeNameSyntax TypeName { get; } = null!;
}

[SyntaxNode]
internal sealed partial class ScriptDeclarationSyntax : SyntaxNode
{
    public SyntaxToken DocComment { get; }
    public SyntaxToken ScriptNameKeyword { get; }
    public IdentifierNameSyntax Name { get; } = null!;
    public ExtendsClauseSyntax ExtendsClause { get; } = null!;
    public SyntaxToken NativeKeyword { get; }
    public SyntaxToken HiddenKeyword { get; }
    public SyntaxToken ConditionalKeyword { get; }
    public SyntaxToken EndOfLineToken { get; }
}
