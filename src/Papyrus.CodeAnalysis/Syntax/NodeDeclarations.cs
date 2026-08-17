namespace Papyrus.CodeAnalysis.Syntax;

internal abstract partial class NameSyntax
{
}

[SyntaxNode]
internal sealed partial class IdentifierNameSyntax : NameSyntax
{
    public IdentifierToken Identifier { get; }
}

[SyntaxNode]
internal sealed partial class QualifiedNameSyntax : NameSyntax
{
    public NameSyntax Left { get; } = null!;
    public ColonToken ColonToken { get; }
    public IdentifierNameSyntax Right { get; } = null!;
}

[SyntaxNode]
internal sealed partial class TypeNameSyntax
{
    public NameSyntax Name { get; } = null!;
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
