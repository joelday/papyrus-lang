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
    public partial NameSyntax Left { get; }
    public SyntaxToken ColonToken { get; }
    public partial IdentifierNameSyntax Right { get; }
}

[SyntaxNode]
internal sealed partial class TypeNameSyntax : SyntaxNode
{
    public partial NameSyntax Name { get; }
    public SyntaxToken HashToken { get; }
    public SyntaxToken StructName { get; }
    public SyntaxToken OpenBracketToken { get; }
    public SyntaxToken CloseBracketToken { get; }
}

[SyntaxNode]
internal sealed partial class ExtendsClauseSyntax : SyntaxNode
{
    public SyntaxToken ExtendsKeyword { get; }
    public partial TypeNameSyntax TypeName { get; }
}

[SyntaxNode]
internal sealed partial class ImportDeclarationSyntax : SyntaxNode
{
    public SyntaxToken ImportKeyword { get; }
    public partial TypeNameSyntax Name { get; }
    public SyntaxToken EndOfLineToken { get; }
}

[SyntaxNode]
internal sealed partial class ScriptDeclarationSyntax : SyntaxNode
{
    public SyntaxToken DocComment { get; }
    public SyntaxToken ScriptNameKeyword { get; }
    public partial IdentifierNameSyntax Name { get; }
    public partial ExtendsClauseSyntax? ExtendsClause { get; }
    public SyntaxToken NativeKeyword { get; }
    public SyntaxToken HiddenKeyword { get; }
    public SyntaxToken ConditionalKeyword { get; }
    public SyntaxToken EndOfLineToken { get; }
    public partial SyntaxList<ImportDeclarationSyntax> Imports { get; }
    public SyntaxToken EndOfFileToken { get; }
}
