namespace Papyrus.CodeAnalysis.Syntax;

[SyntaxNode]
internal sealed partial class IdentifierNameSyntax
{
    public IdentifierToken Identifier { get; }
}

[SyntaxNode]
internal sealed partial class QualifiedNameSyntax
{
    public IdentifierNameSyntax Left { get; } = null!;
    public ColonToken ColonToken { get; }
    public IdentifierNameSyntax Right { get; } = null!;
}

[SyntaxNode]
internal sealed partial class TypeNameSyntax
{
    public QualifiedNameSyntax Name { get; } = null!;
    public HashToken HashToken { get; }
    public IdentifierToken StructName { get; }
    public OpenBracketToken OpenBracketToken { get; }
    public CloseBracketToken CloseBracketToken { get; }
}

[SyntaxNode]
internal sealed partial class ExtendsClauseSyntax
{
    public ExtendsKeyword ExtendsKeyword { get; }
    public TypeNameSyntax TypeName { get; } = null!;
}

[SyntaxNode]
internal sealed partial class ScriptDeclarationSyntax
{
    public DocCommentToken DocComment { get; }
    public ScriptNameKeyword ScriptNameKeyword { get; }
    public IdentifierNameSyntax Name { get; } = null!;
    public ExtendsClauseSyntax ExtendsClause { get; } = null!;
    public NativeKeyword NativeKeyword { get; }
    public HiddenKeyword HiddenKeyword { get; }
    public ConditionalKeyword ConditionalKeyword { get; }
    public EndOfLineToken EndOfLineToken { get; }
}
