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
internal sealed partial class FlagSyntax : SyntaxNode
{
    public SyntaxToken FlagToken { get; }
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
    public partial NameSyntax Name { get; }
    public partial ExtendsClauseSyntax? ExtendsClause { get; }
    public partial SyntaxList<FlagSyntax> Flags { get; }
    public SyntaxToken EndOfLineToken { get; }
    public partial SyntaxList<ImportDeclarationSyntax> Imports { get; }
    public partial SyntaxList<MemberDeclarationSyntax> Members { get; }
    public SyntaxToken EndOfFileToken { get; }

    public SyntaxToken NativeKeyword => FlagToken(SyntaxKind.NativeKeyword);

    public SyntaxToken HiddenKeyword => FlagToken(SyntaxKind.HiddenKeyword);

    public SyntaxToken ConditionalKeyword => FlagToken(SyntaxKind.ConditionalKeyword);

    private SyntaxToken FlagToken(SyntaxKind kind)
    {
        var count = Flags.Count;
        for (var i = 0; i < count; i++)
        {
            var token = Flags.Get(i).FlagToken;
            if (token.Kind == kind)
            {
                return token;
            }
        }

        return default;
    }
}

internal abstract partial class MemberDeclarationSyntax : SyntaxNode
{
    private protected MemberDeclarationSyntax(GreenNode green, SyntaxNode? parent, int position)
        : base(green, parent, position)
    {
    }

    protected SyntaxToken FlagToken(SyntaxList<FlagSyntax> flags, SyntaxKind kind)
    {
        var count = flags.Count;
        for (var i = 0; i < count; i++)
        {
            var token = flags.Get(i).FlagToken;
            if (token.Kind == kind)
            {
                return token;
            }
        }

        return default;
    }
}

internal abstract partial class StatementSyntax : SyntaxNode
{
    private protected StatementSyntax(GreenNode green, SyntaxNode? parent, int position)
        : base(green, parent, position)
    {
    }
}

internal abstract partial class ExpressionSyntax : SyntaxNode
{
    private protected ExpressionSyntax(GreenNode green, SyntaxNode? parent, int position)
        : base(green, parent, position)
    {
    }
}

[SyntaxNode]
internal sealed partial class LiteralExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken Token { get; }
}

[SyntaxNode]
internal sealed partial class PrefixUnaryExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken OperatorToken { get; }
    public partial ExpressionSyntax Operand { get; }
}

[SyntaxNode]
internal sealed partial class EqualsValueClauseSyntax : SyntaxNode
{
    public SyntaxToken EqualsToken { get; }
    public partial ExpressionSyntax Value { get; }
}

[SyntaxNode]
internal sealed partial class ParameterSyntax : SyntaxNode
{
    public SyntaxToken CommaToken { get; }
    public partial TypeNameSyntax TypeName { get; }
    public partial IdentifierNameSyntax Name { get; }
    public partial EqualsValueClauseSyntax? EqualsValue { get; }
}

[SyntaxNode]
internal sealed partial class ParameterListSyntax : SyntaxNode
{
    public SyntaxToken OpenParenToken { get; }
    public partial SyntaxList<ParameterSyntax> Parameters { get; }
    public SyntaxToken CloseParenToken { get; }
}

[SyntaxNode]
internal sealed partial class SkippedStatementSyntax : StatementSyntax
{
    public SyntaxToken EndOfLineToken { get; }
}

[SyntaxNode]
internal sealed partial class FunctionDeclarationSyntax : MemberDeclarationSyntax
{
    public SyntaxToken DocComment { get; }
    public partial TypeNameSyntax? ReturnType { get; }
    public SyntaxToken FunctionKeyword { get; }
    public partial IdentifierNameSyntax Name { get; }
    public partial ParameterListSyntax ParameterList { get; }
    public partial SyntaxList<FlagSyntax> Flags { get; }
    public SyntaxToken EndOfLineToken { get; }
    public partial SyntaxList<StatementSyntax> Statements { get; }
    public SyntaxToken EndFunctionKeyword { get; }

    public SyntaxToken NativeKeyword => FlagToken(Flags, SyntaxKind.NativeKeyword);

    public SyntaxToken GlobalKeyword => FlagToken(Flags, SyntaxKind.GlobalKeyword);
}

[SyntaxNode]
internal sealed partial class EventDeclarationSyntax : MemberDeclarationSyntax
{
    public SyntaxToken DocComment { get; }
    public SyntaxToken EventKeyword { get; }
    public partial IdentifierNameSyntax Name { get; }
    public partial ParameterListSyntax ParameterList { get; }
    public partial SyntaxList<FlagSyntax> Flags { get; }
    public SyntaxToken EndOfLineToken { get; }
    public partial SyntaxList<StatementSyntax> Statements { get; }
    public SyntaxToken EndEventKeyword { get; }

    public SyntaxToken NativeKeyword => FlagToken(Flags, SyntaxKind.NativeKeyword);
}

[SyntaxNode]
internal sealed partial class PropertyDeclarationSyntax : MemberDeclarationSyntax
{
    public SyntaxToken DocComment { get; }
    public partial TypeNameSyntax TypeName { get; }
    public SyntaxToken PropertyKeyword { get; }
    public partial IdentifierNameSyntax Name { get; }
    public partial EqualsValueClauseSyntax? EqualsValue { get; }
    public partial SyntaxList<FlagSyntax> Flags { get; }
    public SyntaxToken EndOfLineToken { get; }
    public partial SyntaxList<FunctionDeclarationSyntax> Accessors { get; }
    public SyntaxToken EndPropertyKeyword { get; }

    public SyntaxToken AutoKeyword => FlagToken(Flags, SyntaxKind.AutoKeyword);

    public SyntaxToken AutoReadOnlyKeyword => FlagToken(Flags, SyntaxKind.AutoReadOnlyKeyword);
}

[SyntaxNode]
internal sealed partial class VariableDeclarationSyntax : MemberDeclarationSyntax
{
    public SyntaxToken DocComment { get; }
    public partial TypeNameSyntax TypeName { get; }
    public partial IdentifierNameSyntax Name { get; }
    public partial EqualsValueClauseSyntax? EqualsValue { get; }
    public partial SyntaxList<FlagSyntax> Flags { get; }
    public SyntaxToken EndOfLineToken { get; }

    public SyntaxToken ConditionalKeyword => FlagToken(Flags, SyntaxKind.ConditionalKeyword);

    public SyntaxToken HiddenKeyword => FlagToken(Flags, SyntaxKind.HiddenKeyword);
}

[SyntaxNode]
internal sealed partial class StateDeclarationSyntax : MemberDeclarationSyntax
{
    public SyntaxToken DocComment { get; }
    public SyntaxToken AutoKeyword { get; }
    public SyntaxToken StateKeyword { get; }
    public partial IdentifierNameSyntax Name { get; }
    public SyntaxToken EndOfLineToken { get; }
    public partial SyntaxList<MemberDeclarationSyntax> Members { get; }
    public SyntaxToken EndStateKeyword { get; }
}

[SyntaxNode]
internal sealed partial class StructDeclarationSyntax : MemberDeclarationSyntax
{
    public SyntaxToken DocComment { get; }
    public SyntaxToken StructKeyword { get; }
    public partial IdentifierNameSyntax Name { get; }
    public SyntaxToken EndOfLineToken { get; }
    public partial SyntaxList<VariableDeclarationSyntax> Members { get; }
    public SyntaxToken EndStructKeyword { get; }
}

[SyntaxNode]
internal sealed partial class GroupDeclarationSyntax : MemberDeclarationSyntax
{
    public SyntaxToken DocComment { get; }
    public SyntaxToken GroupKeyword { get; }
    public partial IdentifierNameSyntax Name { get; }
    public partial SyntaxList<FlagSyntax> Flags { get; }
    public SyntaxToken EndOfLineToken { get; }
    public partial SyntaxList<PropertyDeclarationSyntax> Members { get; }
    public SyntaxToken EndGroupKeyword { get; }
}

[SyntaxNode]
internal sealed partial class CustomEventDeclarationSyntax : MemberDeclarationSyntax
{
    public SyntaxToken DocComment { get; }
    public SyntaxToken CustomEventKeyword { get; }
    public partial IdentifierNameSyntax Name { get; }
    public SyntaxToken EndOfLineToken { get; }
}
