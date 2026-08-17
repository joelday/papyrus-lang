using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Papyrus.CodeAnalysis.Syntax.InternalSyntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Syntax;

internal sealed class Parser
{
    private readonly Lexer _lexer;
    private readonly PapyrusDialect _dialect;
    private readonly CancellationToken _cancellationToken;
    private readonly List<SyntaxToken> _skipped = new();
    private readonly List<SyntaxToken> _ahead = new();
    private SyntaxToken _current;

    public Parser(
        SourceText text,
        PapyrusDialect dialect,
        PapyrusParseOptions options,
        CancellationToken cancellationToken)
    {
        options ??= PapyrusParseOptions.Default;
        _dialect = dialect ?? PapyrusDialect.FromOptions(options);
        _lexer = new Lexer(text, _dialect, options);
        _cancellationToken = cancellationToken;
        _current = _lexer.Lex();
    }

    public ScriptDeclarationSyntax ParseScript()
    {
        SkipEndOfLines();

        var docComment = default(SyntaxToken);
        if (_current.Kind == SyntaxKind.DocCommentToken)
        {
            docComment = Take();
            SkipEndOfLines();
        }

        var scriptNameKeyword = Eat(SyntaxKind.ScriptNameKeyword);
        // Always parse colons; Skyrim bind reports PAP1010 instead of treating them as a path.
        var name = ParseName();

        ExtendsClauseSyntax? extendsClause = null;
        if (_current.Kind == SyntaxKind.ExtendsKeyword)
        {
            extendsClause = ParseExtendsClause();
        }

        var flags = ParseFlags();
        var endOfLineToken = EatEndOfLine();
        var imports = ParseImports();
        var members = ParseMembers(SyntaxKind.EndOfFileToken);
        var endOfFileToken = EatRemainder();

        return SyntaxFactory.ScriptDeclaration(
            docComment,
            scriptNameKeyword,
            name,
            extendsClause,
            SyntaxFactory.List(flags),
            endOfLineToken,
            SyntaxFactory.List(imports),
            SyntaxFactory.List(members),
            endOfFileToken);
    }

    private List<FlagSyntax> ParseFlags()
    {
        var flags = new List<FlagSyntax>();
        while (IsFlag(_current.Kind))
        {
            flags.Add(SyntaxFactory.Flag(TakeFlag()));
        }

        return flags;
    }

    private SyntaxToken TakeFlag()
    {
        var token = Take();
        if (token.Kind != SyntaxKind.IdentifierToken)
        {
            return token;
        }

        var kind = FlagKeywordKind(token.Text);
        return kind == SyntaxKind.None ? token : WithKind(token, kind);
    }

    private static SyntaxKind FlagKeywordKind(string text)
    {
        // Hidden / Conditional are .flg user flags, so the lexer emits identifiers.
        if (text.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
        {
            return SyntaxKind.HiddenKeyword;
        }

        if (text.Equals("Conditional", StringComparison.OrdinalIgnoreCase))
        {
            return SyntaxKind.ConditionalKeyword;
        }

        return SyntaxKind.None;
    }

    private static SyntaxToken WithKind(SyntaxToken token, SyntaxKind kind)
    {
        if (token.Node is not GreenToken green)
        {
            return token;
        }

        var updated = SyntaxTokenCache.Get(kind, green.Text, green.Leading, green.Trailing, green.IsMissing);
        return new SyntaxToken(updated, token.Parent, token.Position);
    }

    private ExtendsClauseSyntax ParseExtendsClause()
    {
        var keyword = Take();
        var typeName = ParseTypeName();
        return SyntaxFactory.ExtendsClause(keyword, typeName);
    }

    private List<ImportDeclarationSyntax> ParseImports()
    {
        var imports = new List<ImportDeclarationSyntax>();
        while (!_cancellationToken.IsCancellationRequested)
        {
            SkipEndOfLines();
            if (_cancellationToken.IsCancellationRequested || _current.Kind != SyntaxKind.ImportKeyword)
            {
                break;
            }

            imports.Add(ParseImport());
        }

        return imports;
    }

    private ImportDeclarationSyntax ParseImport()
    {
        var keyword = Take();
        var name = ParseTypeName();
        var endOfLine = EatEndOfLine();
        return SyntaxFactory.ImportDeclaration(keyword, name, endOfLine);
    }

    private List<MemberDeclarationSyntax> ParseMembers(SyntaxKind terminator)
    {
        var members = new List<MemberDeclarationSyntax>();
        while (!_cancellationToken.IsCancellationRequested)
        {
            SkipEndOfLines();
            if (_cancellationToken.IsCancellationRequested
                || _current.Kind == SyntaxKind.EndOfFileToken
                || _current.Kind == terminator)
            {
                break;
            }

            if (IsMemberStart(_current.Kind))
            {
                members.Add(ParseMember());
                continue;
            }

            Skip();
        }

        return members;
    }

    private MemberDeclarationSyntax ParseMember()
    {
        var docComment = default(SyntaxToken);
        if (_current.Kind == SyntaxKind.DocCommentToken)
        {
            docComment = Take();
            SkipEndOfLines();
        }

        if (_current.Kind is SyntaxKind.AutoKeyword or SyntaxKind.StateKeyword)
        {
            return ParseState(docComment);
        }

        if (_dialect.SupportsStructs && _current.Kind == SyntaxKind.StructKeyword)
        {
            return ParseStruct(docComment);
        }

        if (_dialect.SupportsGroups && _current.Kind == SyntaxKind.GroupKeyword)
        {
            return ParseGroup(docComment);
        }

        if (_dialect.SupportsCustomEvents && _current.Kind == SyntaxKind.CustomEventKeyword)
        {
            return ParseCustomEvent(docComment);
        }

        if (_dialect.SupportsGuards && _current.Kind == SyntaxKind.GuardKeyword)
        {
            return ParseGuard(docComment);
        }

        if (_current.Kind == SyntaxKind.FunctionKeyword)
        {
            return ParseFunction(docComment, returnType: null);
        }

        if (_current.Kind == SyntaxKind.EventKeyword)
        {
            return ParseEvent(docComment);
        }

        if (_current.Kind == SyntaxKind.PropertyKeyword)
        {
            return ParseProperty(docComment, MissingTypeName());
        }

        var typeName = ParseTypeName();
        if (_current.Kind == SyntaxKind.FunctionKeyword)
        {
            return ParseFunction(docComment, typeName);
        }

        if (_current.Kind == SyntaxKind.PropertyKeyword)
        {
            return ParseProperty(docComment, typeName);
        }

        return ParseVariable(docComment, typeName);
    }

    private StateDeclarationSyntax ParseState(SyntaxToken docComment)
    {
        var autoKeyword = _current.Kind == SyntaxKind.AutoKeyword ? Take() : default;
        var stateKeyword = Eat(SyntaxKind.StateKeyword);
        var name = ParseIdentifierName();
        var endOfLine = EatEndOfLine();
        var members = ParseMembers(SyntaxKind.EndStateKeyword);
        var endState = Eat(SyntaxKind.EndStateKeyword);
        return SyntaxFactory.StateDeclaration(
            docComment,
            autoKeyword,
            stateKeyword,
            name,
            endOfLine,
            SyntaxFactory.List(members),
            endState);
    }

    private StructDeclarationSyntax ParseStruct(SyntaxToken docComment)
    {
        var structKeyword = Eat(SyntaxKind.StructKeyword);
        var name = ParseIdentifierName();
        var endOfLine = EatEndOfLine();
        var members = ParseStructMembers();
        var endStruct = Eat(SyntaxKind.EndStructKeyword);
        return SyntaxFactory.StructDeclaration(
            docComment,
            structKeyword,
            name,
            endOfLine,
            SyntaxFactory.List(members),
            endStruct);
    }

    private List<VariableDeclarationSyntax> ParseStructMembers()
    {
        var members = new List<VariableDeclarationSyntax>();
        while (!_cancellationToken.IsCancellationRequested)
        {
            SkipEndOfLines();
            if (_cancellationToken.IsCancellationRequested
                || _current.Kind == SyntaxKind.EndStructKeyword
                || _current.Kind == SyntaxKind.EndOfFileToken
                || IsEndKeyword(_current.Kind)
                || IsDialectMemberKeyword(_current.Kind)
                || IsNonVariableMemberStart(_current.Kind))
            {
                break;
            }

            if (_current.Kind == SyntaxKind.DocCommentToken)
            {
                if (!LooksLikeVariableDeclarationAt(SkipDocCommentLookahead(0)))
                {
                    break;
                }

                var docComment = Take();
                SkipEndOfLines();
                members.Add(ParseVariable(docComment, ParseTypeName()));
                continue;
            }

            if (LooksLikeTypedDeclaration() || (LooksLikeDeclarationStart() && !LooksLikeVariableDeclaration()))
            {
                break;
            }

            if (IsTypeStart(_current.Kind))
            {
                members.Add(ParseVariable(docComment: default, ParseTypeName()));
                continue;
            }

            Skip();
        }

        return members;
    }

    private GroupDeclarationSyntax ParseGroup(SyntaxToken docComment)
    {
        var groupKeyword = Eat(SyntaxKind.GroupKeyword);
        var name = ParseIdentifierName();
        var flags = ParseFlags();
        var endOfLine = EatEndOfLine();
        var members = ParseGroupMembers();
        var endGroup = Eat(SyntaxKind.EndGroupKeyword);
        return SyntaxFactory.GroupDeclaration(
            docComment,
            groupKeyword,
            name,
            SyntaxFactory.List(flags),
            endOfLine,
            SyntaxFactory.List(members),
            endGroup);
    }

    private List<PropertyDeclarationSyntax> ParseGroupMembers()
    {
        var members = new List<PropertyDeclarationSyntax>();
        while (!_cancellationToken.IsCancellationRequested)
        {
            SkipEndOfLines();
            if (_cancellationToken.IsCancellationRequested
                || _current.Kind == SyntaxKind.EndGroupKeyword
                || _current.Kind == SyntaxKind.EndOfFileToken
                || IsEndKeyword(_current.Kind)
                || IsDialectMemberKeyword(_current.Kind)
                || IsNonPropertyMemberStart(_current.Kind))
            {
                break;
            }

            if (_current.Kind == SyntaxKind.DocCommentToken)
            {
                if (!LooksLikePropertyDeclarationAt(SkipDocCommentLookahead(0)))
                {
                    break;
                }

                var docComment = Take();
                SkipEndOfLines();
                members.Add(ParseProperty(docComment, TryParsePropertyType()));
                continue;
            }

            if (LooksLikeDeclarationStart() && !LooksLikePropertyDeclaration())
            {
                break;
            }

            if (_current.Kind == SyntaxKind.PropertyKeyword || IsTypeStart(_current.Kind))
            {
                members.Add(ParseProperty(docComment: default, TryParsePropertyType()));
                continue;
            }

            Skip();
        }

        return members;
    }

    private CustomEventDeclarationSyntax ParseCustomEvent(SyntaxToken docComment)
    {
        var customEventKeyword = Eat(SyntaxKind.CustomEventKeyword);
        var name = ParseIdentifierName();
        var endOfLine = EatEndOfLine();
        return SyntaxFactory.CustomEventDeclaration(docComment, customEventKeyword, name, endOfLine);
    }

    private GuardDeclarationSyntax ParseGuard(SyntaxToken docComment)
    {
        var guardKeyword = Eat(SyntaxKind.GuardKeyword);
        var name = ParseIdentifierName();
        var endOfLine = EatEndOfLine();
        return SyntaxFactory.GuardDeclaration(docComment, guardKeyword, name, endOfLine);
    }

    private TypeNameSyntax TryParsePropertyType()
        => _current.Kind == SyntaxKind.PropertyKeyword ? MissingTypeName() : ParseTypeName();

    private FunctionDeclarationSyntax ParseFunction(SyntaxToken docComment, TypeNameSyntax? returnType)
    {
        var functionKeyword = Eat(SyntaxKind.FunctionKeyword);
        var name = ParseIdentifierName();
        var parameterList = ParseParameterList();
        var flags = ParseFlags();
        var endOfLine = EatEndOfLine();

        var statements = default(SyntaxList<StatementSyntax>);
        var endFunction = default(SyntaxToken);
        if (HasFlag(flags, SyntaxKind.NativeKeyword))
        {
            if (_current.Kind == SyntaxKind.EndFunctionKeyword)
            {
                endFunction = Take();
            }
        }
        else
        {
            statements = SyntaxFactory.List(ParseStatements(StatementScope.Function));
            endFunction = Eat(SyntaxKind.EndFunctionKeyword);
        }

        return SyntaxFactory.FunctionDeclaration(
            docComment,
            returnType,
            functionKeyword,
            name,
            parameterList,
            SyntaxFactory.List(flags),
            endOfLine,
            statements,
            endFunction);
    }

    private EventDeclarationSyntax ParseEvent(SyntaxToken docComment)
    {
        var eventKeyword = Eat(SyntaxKind.EventKeyword);
        var name = ParseIdentifierName();
        var parameterList = ParseParameterList();
        var flags = ParseFlags();
        var endOfLine = EatEndOfLine();

        var statements = default(SyntaxList<StatementSyntax>);
        var endEvent = default(SyntaxToken);
        if (HasFlag(flags, SyntaxKind.NativeKeyword))
        {
            if (_current.Kind == SyntaxKind.EndEventKeyword)
            {
                endEvent = Take();
            }
        }
        else
        {
            statements = SyntaxFactory.List(ParseStatements(StatementScope.Event));
            endEvent = Eat(SyntaxKind.EndEventKeyword);
        }

        return SyntaxFactory.EventDeclaration(
            docComment,
            eventKeyword,
            name,
            parameterList,
            SyntaxFactory.List(flags),
            endOfLine,
            statements,
            endEvent);
    }

    private PropertyDeclarationSyntax ParseProperty(SyntaxToken docComment, TypeNameSyntax typeName)
    {
        var propertyKeyword = Eat(SyntaxKind.PropertyKeyword);
        var name = ParseIdentifierName();
        var equalsValue = TryParseEqualsValue();
        var flags = ParseFlags();
        var endOfLine = EatEndOfLine();

        var accessors = default(SyntaxList<FunctionDeclarationSyntax>);
        var endProperty = default(SyntaxToken);
        if (HasFlag(flags, SyntaxKind.AutoKeyword) || HasFlag(flags, SyntaxKind.AutoReadOnlyKeyword))
        {
            if (_current.Kind == SyntaxKind.EndPropertyKeyword)
            {
                endProperty = Take();
            }
        }
        else
        {
            accessors = SyntaxFactory.List(ParsePropertyAccessors());
            endProperty = Eat(SyntaxKind.EndPropertyKeyword);
        }

        return SyntaxFactory.PropertyDeclaration(
            docComment,
            typeName,
            propertyKeyword,
            name,
            equalsValue,
            SyntaxFactory.List(flags),
            endOfLine,
            accessors,
            endProperty);
    }

    private List<FunctionDeclarationSyntax> ParsePropertyAccessors()
    {
        var accessors = new List<FunctionDeclarationSyntax>();
        while (!_cancellationToken.IsCancellationRequested)
        {
            SkipEndOfLines();
            if (_cancellationToken.IsCancellationRequested
                || _current.Kind is SyntaxKind.EndPropertyKeyword
                    or SyntaxKind.EndOfFileToken
                    or SyntaxKind.EndStateKeyword
                    or SyntaxKind.EndFunctionKeyword
                    or SyntaxKind.EndEventKeyword
                    or SyntaxKind.EndStructKeyword
                    or SyntaxKind.EndGroupKeyword
                    or SyntaxKind.EndGuardKeyword)
            {
                break;
            }

            if (_current.Kind == SyntaxKind.DocCommentToken)
            {
                if (!LooksLikeFunctionDeclarationAt(SkipDocCommentLookahead(0)))
                {
                    break;
                }

                var docComment = Take();
                SkipEndOfLines();
                accessors.Add(ParseFunction(docComment, TryParseFunctionReturnType()));
                continue;
            }

            if (LooksLikeDeclarationStart() && !LooksLikeFunctionDeclaration())
            {
                break;
            }

            if (_current.Kind == SyntaxKind.FunctionKeyword || IsTypeStart(_current.Kind))
            {
                accessors.Add(ParseFunction(docComment: default, TryParseFunctionReturnType()));
                continue;
            }

            Skip();
        }

        return accessors;
    }

    private VariableDeclarationSyntax ParseVariable(SyntaxToken docComment, TypeNameSyntax typeName)
    {
        var name = ParseIdentifierName();
        var equalsValue = TryParseEqualsValue();
        SkipUntilFlagOrLineEnd();
        var flags = ParseFlags();
        var endOfLine = EatEndOfLine();
        return SyntaxFactory.VariableDeclaration(
            docComment,
            typeName,
            name,
            equalsValue,
            SyntaxFactory.List(flags),
            endOfLine);
    }

    private ParameterListSyntax ParseParameterList()
    {
        var openParen = Eat(SyntaxKind.OpenParenToken);
        var parameters = new List<ParameterSyntax>();
        if (!openParen.IsMissing
            && _current.Kind != SyntaxKind.CloseParenToken
            && !IsParameterListStop(_current.Kind))
        {
            parameters.Add(ParseParameter(comma: default));
            while (_current.Kind == SyntaxKind.CommaToken)
            {
                var comma = Take();
                parameters.Add(ParseParameter(comma));
            }
        }

        if (openParen.IsMissing && _current.Kind != SyntaxKind.CloseParenToken)
        {
            return SyntaxFactory.ParameterList(
                openParen,
                SyntaxFactory.List(parameters),
                Missing(SyntaxKind.CloseParenToken));
        }

        var closeParen = Eat(SyntaxKind.CloseParenToken);
        return SyntaxFactory.ParameterList(openParen, SyntaxFactory.List(parameters), closeParen);
    }

    private ParameterSyntax ParseParameter(SyntaxToken comma)
    {
        var typeName = ParseTypeName();
        var name = ParseIdentifierName();
        var equalsValue = TryParseEqualsValue();
        SkipUntilParameterDelimiter();
        return SyntaxFactory.Parameter(comma, typeName, name, equalsValue);
    }

    private EqualsValueClauseSyntax? TryParseEqualsValue()
    {
        if (_current.Kind != SyntaxKind.EqualsToken)
        {
            return null;
        }

        var equals = Take();
        return SyntaxFactory.EqualsValueClause(equals, ParseExpression());
    }

    private List<StatementSyntax> ParseStatements(StatementScope scope)
    {
        var statements = new List<StatementSyntax>();
        while (!_cancellationToken.IsCancellationRequested)
        {
            SkipEndOfLines();
            if (_cancellationToken.IsCancellationRequested
                || IsStatementListStop(scope, _current.Kind)
                || (LooksLikeDeclarationStart() && !IsGuardStatementStart(_current.Kind)))
            {
                break;
            }

            statements.Add(ParseStatement());
        }

        return statements;
    }

    private StatementSyntax ParseStatement()
    {
        if (_dialect.SupportsGuards
            && _current.Kind is SyntaxKind.GuardKeyword or SyntaxKind.TryGuardKeyword)
        {
            return ParseGuardOrTryGuardStatement();
        }
        if (_current.Kind == SyntaxKind.IfKeyword)
        {
            return ParseIfStatement();
        }

        if (_current.Kind == SyntaxKind.WhileKeyword)
        {
            return ParseWhileStatement();
        }

        if (_current.Kind == SyntaxKind.ReturnKeyword)
        {
            return ParseReturnStatement();
        }

        if (LooksLikeLocalDeclare())
        {
            return ParseDeclareStatement();
        }

        if (LooksLikeAssignment())
        {
            return ParseAssignmentStatement();
        }

        if (IsExpressionStart(_current.Kind))
        {
            return ParseExpressionStatement();
        }

        return ParseSkippedStatement();
    }

    private IfStatementSyntax ParseIfStatement()
    {
        var ifKeyword = Take();
        var condition = ParseLineExpression();
        var endOfLine = EatEndOfLine();
        var statements = ParseStatements(StatementScope.If);

        var elseIfClauses = new List<ElseIfClauseSyntax>();
        while (_current.Kind == SyntaxKind.ElseIfKeyword)
        {
            elseIfClauses.Add(ParseElseIfClause());
        }

        ElseClauseSyntax? elseClause = null;
        if (_current.Kind == SyntaxKind.ElseKeyword)
        {
            elseClause = ParseElseClause();
        }

        var endIf = Eat(SyntaxKind.EndIfKeyword);
        return SyntaxFactory.IfStatement(
            ifKeyword,
            condition,
            endOfLine,
            SyntaxFactory.List(statements),
            SyntaxFactory.List(elseIfClauses),
            elseClause,
            endIf);
    }

    private ElseIfClauseSyntax ParseElseIfClause()
    {
        var elseIfKeyword = Take();
        var condition = ParseLineExpression();
        var endOfLine = EatEndOfLine();
        var statements = ParseStatements(StatementScope.If);
        return SyntaxFactory.ElseIfClause(
            elseIfKeyword,
            condition,
            endOfLine,
            SyntaxFactory.List(statements));
    }

    private ElseClauseSyntax ParseElseClause()
    {
        var elseKeyword = Take();
        var endOfLine = EatEndOfLine();
        var statements = ParseStatements(StatementScope.If);
        return SyntaxFactory.ElseClause(elseKeyword, endOfLine, SyntaxFactory.List(statements));
    }

    private WhileStatementSyntax ParseWhileStatement()
    {
        var whileKeyword = Take();
        var condition = ParseLineExpression();
        var endOfLine = EatEndOfLine();
        var statements = ParseStatements(StatementScope.While);
        var endWhile = Eat(SyntaxKind.EndWhileKeyword);
        return SyntaxFactory.WhileStatement(
            whileKeyword,
            condition,
            endOfLine,
            SyntaxFactory.List(statements),
            endWhile);
    }

    private ReturnStatementSyntax ParseReturnStatement()
    {
        var returnKeyword = Take();
        ExpressionSyntax? expression = null;
        if (!IsLineTerminator(_current.Kind) && !IsStatementKeyword(_current.Kind))
        {
            expression = ParseLineExpression();
        }

        return SyntaxFactory.ReturnStatement(returnKeyword, expression, EatEndOfLine());
    }

    private DeclareStatementSyntax ParseDeclareStatement()
    {
        var typeName = ParseTypeName();
        var name = ParseIdentifierName();
        EqualsValueClauseSyntax? equalsValue = null;
        if (_current.Kind == SyntaxKind.EqualsToken)
        {
            var equals = Take();
            equalsValue = SyntaxFactory.EqualsValueClause(equals, ParseLineExpression());
        }

        return SyntaxFactory.DeclareStatement(typeName, name, equalsValue, EatEndOfLine());
    }

    private AssignmentStatementSyntax ParseAssignmentStatement()
    {
        var left = ParseExpression();
        var operatorToken = IsAssignmentOperator(_current.Kind)
            ? Take()
            : Missing(SyntaxKind.EqualsToken);
        var right = ParseExpression();
        return SyntaxFactory.AssignmentStatement(left, operatorToken, right, EatEndOfLine());
    }

    private ExpressionStatementSyntax ParseExpressionStatement()
    {
        var expression = ParseLineExpression();
        return SyntaxFactory.ExpressionStatement(expression, EatEndOfLine());
    }

    private StatementSyntax ParseGuardOrTryGuardStatement()
    {
        if (_current.Kind == SyntaxKind.TryGuardKeyword)
        {
            var tryGuardKeyword = Take();
            var lockParameters = ParseLockParameters();
            var endOfLine = EatEndOfLine();
            var body = ParseStatements(StatementScope.Guard);
            var endGuard = Eat(SyntaxKind.EndGuardKeyword);
            return SyntaxFactory.TryGuardStatement(
                tryGuardKeyword,
                SyntaxFactory.List(lockParameters),
                endOfLine,
                SyntaxFactory.List(body),
                endGuard);
        }

        var guardKeyword = Eat(SyntaxKind.GuardKeyword);
        var parameters = ParseLockParameters();
        var headerEndOfLine = EatEndOfLine();
        var statements = ParseStatements(StatementScope.Guard);
        var endGuardKeyword = Eat(SyntaxKind.EndGuardKeyword);
        return SyntaxFactory.GuardStatement(
            guardKeyword,
            SyntaxFactory.List(parameters),
            headerEndOfLine,
            SyntaxFactory.List(statements),
            endGuardKeyword);
    }

    private List<LockParameterSyntax> ParseLockParameters()
    {
        var parameters = new List<LockParameterSyntax>();
        if (IsLockParameterListStop(_current.Kind))
        {
            parameters.Add(SyntaxFactory.LockParameter(default, SyntaxFactory.IdentifierName(Missing(SyntaxKind.IdentifierToken))));
            return parameters;
        }

        parameters.Add(ParseLockParameter(comma: default));
        while (_current.Kind == SyntaxKind.CommaToken)
        {
            var comma = Take();
            parameters.Add(ParseLockParameter(comma));
        }

        return parameters;
    }

    private LockParameterSyntax ParseLockParameter(SyntaxToken comma)
    {
        var name = ParseIdentifierName();
        while (!IsLockParameterListStop(_current.Kind) && _current.Kind != SyntaxKind.CommaToken)
        {
            Skip();
        }

        return SyntaxFactory.LockParameter(comma, name);
    }

    private static bool IsLockParameterListStop(SyntaxKind kind)
        => IsLineTerminator(kind) || kind == SyntaxKind.EndGuardKeyword;

    private SkippedStatementSyntax ParseSkippedStatement()
    {
        // Always consume the current token so stray EndIf/EndWhile cannot
        // sit at a list-stop boundary and spin. Type keywords are member
        // terminators, but soup still has to eat `Int x` / `as Int`.
        if (_current.Kind is not SyntaxKind.EndOfLineToken and not SyntaxKind.EndOfFileToken)
        {
            Skip();
        }

        while (!IsStatementStop(_current.Kind))
        {
            Skip();
        }

        return SyntaxFactory.SkippedStatement(EatEndOfLine());
    }

    private ExpressionSyntax ParseLineExpression() => ParseExpression();

    private ExpressionSyntax ParseExpression() => ParseLogicalOrExpression();

    private ExpressionSyntax ParseLogicalOrExpression()
    {
        var left = ParseLogicalAndExpression();
        while (_current.Kind == SyntaxKind.BarBarToken)
        {
            var operatorToken = Take();
            left = SyntaxFactory.BinaryExpression(left, operatorToken, ParseLogicalAndExpression());
        }

        return left;
    }

    private ExpressionSyntax ParseLogicalAndExpression()
    {
        var left = ParseComparisonExpression();
        while (_current.Kind == SyntaxKind.AmpersandAmpersandToken)
        {
            var operatorToken = Take();
            left = SyntaxFactory.BinaryExpression(left, operatorToken, ParseComparisonExpression());
        }

        return left;
    }

    private ExpressionSyntax ParseComparisonExpression()
    {
        var left = ParseAdditiveExpression();
        while (IsComparisonOperator(_current.Kind))
        {
            var operatorToken = Take();
            left = SyntaxFactory.BinaryExpression(left, operatorToken, ParseAdditiveExpression());
        }

        return left;
    }

    private ExpressionSyntax ParseAdditiveExpression()
    {
        var left = ParseMultiplicativeExpression();
        while (_current.Kind is SyntaxKind.PlusToken or SyntaxKind.MinusToken)
        {
            var operatorToken = Take();
            left = SyntaxFactory.BinaryExpression(left, operatorToken, ParseMultiplicativeExpression());
        }

        return left;
    }

    private ExpressionSyntax ParseMultiplicativeExpression()
    {
        var left = ParseUnaryExpression();
        while (_current.Kind is SyntaxKind.AsteriskToken or SyntaxKind.SlashToken or SyntaxKind.PercentToken)
        {
            var operatorToken = Take();
            left = SyntaxFactory.BinaryExpression(left, operatorToken, ParseUnaryExpression());
        }

        return left;
    }

    private ExpressionSyntax ParseUnaryExpression()
    {
        if (_current.Kind is SyntaxKind.MinusToken or SyntaxKind.ExclamationToken)
        {
            var operatorToken = Take();
            return SyntaxFactory.PrefixUnaryExpression(operatorToken, ParseUnaryExpression());
        }

        return ParseCastExpression();
    }

    private ExpressionSyntax ParseCastExpression()
    {
        var expression = ParsePostfixExpression();
        if (_current.Kind == SyntaxKind.AsKeyword || LooksLikeIsOperator())
        {
            var operatorToken = Take();
            if (operatorToken.Kind == SyntaxKind.IdentifierToken)
            {
                operatorToken = WithKind(operatorToken, SyntaxKind.IsKeyword);
            }

            return SyntaxFactory.CastExpression(expression, operatorToken, ParseTypeName());
        }

        return expression;
    }

    private ExpressionSyntax ParsePostfixExpression()
    {
        var expression = ParsePrimaryExpression();
        while (true)
        {
            if (_current.Kind == SyntaxKind.DotToken)
            {
                var dot = Take();
                expression = SyntaxFactory.MemberAccessExpression(expression, dot, ParseMemberName());
                continue;
            }

            if (_current.Kind == SyntaxKind.OpenParenToken && IsInvocable(expression))
            {
                expression = SyntaxFactory.InvocationExpression(expression, ParseArgumentList());
                continue;
            }

            if (_current.Kind == SyntaxKind.OpenBracketToken)
            {
                var openBracket = Take();
                var argument = ParseExpression();
                var closeBracket = Eat(SyntaxKind.CloseBracketToken);
                expression = SyntaxFactory.ElementAccessExpression(
                    expression,
                    openBracket,
                    argument,
                    closeBracket);
                continue;
            }

            break;
        }

        return expression;
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        if (IsLiteral(_current.Kind))
        {
            return SyntaxFactory.LiteralExpression(Take());
        }

        if (IsNameToken(_current.Kind))
        {
            return SyntaxFactory.NameExpression(ParseName());
        }

        if (_current.Kind == SyntaxKind.LengthKeyword)
        {
            return SyntaxFactory.NameExpression(SyntaxFactory.IdentifierName(Take()));
        }

        if (_current.Kind == SyntaxKind.NewKeyword)
        {
            return ParseNewExpression();
        }

        if (_current.Kind == SyntaxKind.OpenParenToken)
        {
            return ParseParenthesizedExpression();
        }

        return MissingExpression();
    }

    private ExpressionSyntax ParseNewExpression()
    {
        var newKeyword = Take();
        var typeName = ParseTypeName();
        if (_current.Kind != SyntaxKind.OpenBracketToken)
        {
            return SyntaxFactory.NewExpression(
                newKeyword,
                typeName,
                default,
                size: null,
                default);
        }

        var openBracket = Take();
        var size = ParseExpression();
        var closeBracket = Eat(SyntaxKind.CloseBracketToken);
        return SyntaxFactory.NewExpression(newKeyword, typeName, openBracket, size, closeBracket);
    }

    private ExpressionSyntax ParseParenthesizedExpression()
    {
        var openParen = Take();
        var expression = ParseExpression();
        var closeParen = Eat(SyntaxKind.CloseParenToken);
        return SyntaxFactory.ParenthesizedExpression(openParen, expression, closeParen);
    }

    private ArgumentListSyntax ParseArgumentList()
    {
        var openParen = Eat(SyntaxKind.OpenParenToken);
        var arguments = new List<ArgumentSyntax>();
        if (!openParen.IsMissing
            && _current.Kind != SyntaxKind.CloseParenToken
            && !IsArgumentListStop(_current.Kind))
        {
            arguments.Add(ParseArgument(comma: default));
            while (_current.Kind == SyntaxKind.CommaToken)
            {
                var comma = Take();
                arguments.Add(ParseArgument(comma));
            }
        }

        if (openParen.IsMissing && _current.Kind != SyntaxKind.CloseParenToken)
        {
            return SyntaxFactory.ArgumentList(
                openParen,
                SyntaxFactory.List(arguments),
                Missing(SyntaxKind.CloseParenToken));
        }

        var closeParen = Eat(SyntaxKind.CloseParenToken);
        return SyntaxFactory.ArgumentList(openParen, SyntaxFactory.List(arguments), closeParen);
    }

    private ArgumentSyntax ParseArgument(SyntaxToken comma)
    {
        IdentifierNameSyntax? name = null;
        var equals = default(SyntaxToken);
        if (IsNameToken(_current.Kind) && PeekToken(1).Kind == SyntaxKind.EqualsToken)
        {
            name = ParseIdentifierName();
            equals = Take();
        }

        return SyntaxFactory.Argument(comma, name, equals, ParseExpression());
    }

    private IdentifierNameSyntax ParseMemberName()
    {
        if (IsMemberNameToken(_current.Kind))
        {
            return SyntaxFactory.IdentifierName(Take());
        }

        return SyntaxFactory.IdentifierName(Missing(SyntaxKind.IdentifierToken));
    }

    private static ExpressionSyntax MissingExpression()
        => SyntaxFactory.LiteralExpression(Missing(SyntaxKind.IntegerLiteralToken));

    private static bool IsInvocable(ExpressionSyntax expression)
        => expression is NameExpressionSyntax or MemberAccessExpressionSyntax;

    private TypeNameSyntax? TryParseFunctionReturnType()
    {
        if (_current.Kind == SyntaxKind.FunctionKeyword)
        {
            return null;
        }

        return ParseTypeName();
    }

    private TypeNameSyntax ParseTypeName()
    {
        var name = ParseName();

        var hashToken = default(SyntaxToken);
        var structName = default(SyntaxToken);
        if (_current.Kind == SyntaxKind.HashToken)
        {
            hashToken = Take();
            structName = Eat(SyntaxKind.IdentifierToken);
        }

        var openBracket = default(SyntaxToken);
        var closeBracket = default(SyntaxToken);
        // typename allows only empty `[]`; `[n]` belongs to `new Type[n]`.
        if (_current.Kind == SyntaxKind.OpenBracketToken
            && PeekToken(1).Kind == SyntaxKind.CloseBracketToken)
        {
            openBracket = Take();
            closeBracket = Take();
        }

        return SyntaxFactory.TypeName(name, hashToken, structName, openBracket, closeBracket);
    }

    private TypeNameSyntax MissingTypeName()
        => SyntaxFactory.TypeName(
            SyntaxFactory.IdentifierName(Missing(SyntaxKind.IdentifierToken)),
            default,
            default,
            default,
            default);

    private NameSyntax ParseName()
    {
        NameSyntax left = ParseIdentifierName();
        while (_current.Kind == SyntaxKind.ColonToken)
        {
            var colon = Take();
            var right = ParseIdentifierName();
            left = SyntaxFactory.QualifiedName(left, colon, right);
        }

        return left;
    }

    private IdentifierNameSyntax ParseIdentifierName()
    {
        if (IsNameToken(_current.Kind))
        {
            return SyntaxFactory.IdentifierName(Take());
        }

        return SyntaxFactory.IdentifierName(Missing(SyntaxKind.IdentifierToken));
    }

    private SyntaxToken Eat(SyntaxKind kind)
    {
        if (_current.Kind == kind)
        {
            return Take();
        }

        return Missing(kind);
    }

    private SyntaxToken EatEndOfLine()
    {
        if (_current.Kind == SyntaxKind.EndOfLineToken)
        {
            return Take();
        }

        if (IsLineTerminator(_current.Kind))
        {
            return Missing(SyntaxKind.EndOfLineToken);
        }

        while (!IsLineTerminator(_current.Kind))
        {
            Skip();
        }

        if (_current.Kind == SyntaxKind.EndOfLineToken)
        {
            return Take();
        }

        return Missing(SyntaxKind.EndOfLineToken);
    }

    private SyntaxToken EatRemainder()
    {
        while (_current.Kind != SyntaxKind.EndOfFileToken)
        {
            Skip();
        }

        return Take();
    }

    private void SkipEndOfLines()
    {
        while (_current.Kind == SyntaxKind.EndOfLineToken)
        {
            Skip();
        }
    }

    private void SkipUntilFlagOrLineEnd()
    {
        while (!IsFlag(_current.Kind) && !IsLineTerminator(_current.Kind))
        {
            Skip();
        }
    }

    private void SkipUntilParameterDelimiter()
    {
        while (_current.Kind != SyntaxKind.CommaToken && !IsParameterListStop(_current.Kind))
        {
            Skip();
        }
    }

    private SyntaxToken Take()
    {
        var token = AttachSkipped(_current);
        _current = NextToken();
        return token;
    }

    private void Skip()
    {
        if (_current.Kind == SyntaxKind.EndOfFileToken)
        {
            return;
        }

        _skipped.Add(_current);
        _current = NextToken();
    }

    private SyntaxToken NextToken()
    {
        if (_ahead.Count == 0)
        {
            return _lexer.Lex();
        }

        var token = _ahead[0];
        _ahead.RemoveAt(0);
        return token;
    }

    private SyntaxToken PeekToken(int distance)
    {
        if (distance == 0)
        {
            return _current;
        }

        while (_ahead.Count < distance)
        {
            var next = _lexer.Lex();
            _ahead.Add(next);
            if (next.Kind == SyntaxKind.EndOfFileToken)
            {
                break;
            }
        }

        var index = distance - 1;
        if (index >= _ahead.Count)
        {
            return _ahead.Count > 0 ? _ahead[_ahead.Count - 1] : _current;
        }

        return _ahead[index];
    }

    private bool LooksLikeDeclarationStart()
    {
        if (_current.Kind is SyntaxKind.FunctionKeyword
            or SyntaxKind.EventKeyword
            or SyntaxKind.StateKeyword
            or SyntaxKind.AutoKeyword
            or SyntaxKind.PropertyKeyword
            or SyntaxKind.ImportKeyword
            or SyntaxKind.DocCommentToken)
        {
            return true;
        }

        if (IsDialectMemberKeyword(_current.Kind))
        {
            return true;
        }

        return LooksLikeTypedDeclaration();
    }

    private bool LooksLikeVariableDeclaration()
        => LooksLikeVariableDeclarationAt(0);

    private bool LooksLikeVariableDeclarationAt(int index)
    {
        if (!IsTypeStart(PeekToken(index).Kind))
        {
            return false;
        }

        var afterType = PeekToken(SkipTypeLookahead(index)).Kind;
        if (afterType is SyntaxKind.FunctionKeyword or SyntaxKind.PropertyKeyword
            || IsDialectMemberKeyword(afterType)
            || IsEndKeyword(afterType)
            || IsNonVariableMemberStart(afterType))
        {
            return false;
        }

        return IsNameToken(afterType)
            || afterType is SyntaxKind.EqualsToken
            || IsKnownFlagKeyword(afterType)
            || afterType is SyntaxKind.EndOfLineToken;
    }

    private bool LooksLikePropertyDeclaration()
        => LooksLikePropertyDeclarationAt(0);

    private bool LooksLikePropertyDeclarationAt(int index)
    {
        var kind = PeekToken(index).Kind;
        if (kind == SyntaxKind.PropertyKeyword)
        {
            return true;
        }

        if (!IsTypeStart(kind))
        {
            return false;
        }

        return PeekToken(SkipTypeLookahead(index)).Kind == SyntaxKind.PropertyKeyword;
    }

    private bool LooksLikeFunctionDeclaration()
        => LooksLikeFunctionDeclarationAt(0);

    private bool LooksLikeFunctionDeclarationAt(int index)
    {
        var kind = PeekToken(index).Kind;
        if (kind == SyntaxKind.FunctionKeyword)
        {
            return true;
        }

        if (!IsTypeStart(kind))
        {
            return false;
        }

        return PeekToken(SkipTypeLookahead(index)).Kind == SyntaxKind.FunctionKeyword;
    }

    private int SkipDocCommentLookahead(int index)
    {
        if (PeekToken(index).Kind != SyntaxKind.DocCommentToken)
        {
            return index;
        }

        index++;
        while (PeekToken(index).Kind == SyntaxKind.EndOfLineToken)
        {
            index++;
        }

        return index;
    }

    private bool LooksLikeTypedDeclaration()
    {
        if (!IsTypeStart(_current.Kind))
        {
            return false;
        }

        var next = PeekToken(SkipTypeLookahead(0)).Kind;
        return next is SyntaxKind.FunctionKeyword or SyntaxKind.PropertyKeyword;
    }

    private bool LooksLikeLocalDeclare()
    {
        if (!IsTypeStart(_current.Kind))
        {
            return false;
        }

        return IsNameToken(PeekToken(SkipTypeLookahead(0)).Kind);
    }

    private bool LooksLikeAssignment()
    {
        var depth = 0;
        for (var i = 0; ; i++)
        {
            var kind = PeekToken(i).Kind;
            if (kind is SyntaxKind.EndOfLineToken or SyntaxKind.EndOfFileToken
                || IsStatementKeyword(kind)
                || IsEndKeyword(kind))
            {
                return false;
            }

            if (kind is SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken)
            {
                depth++;
            }
            else if (kind is SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken)
            {
                if (depth > 0)
                {
                    depth--;
                }
            }
            else if (depth == 0 && IsAssignmentOperator(kind))
            {
                return true;
            }
        }
    }

    private int SkipTypeLookahead(int index)
    {
        if (!IsNameToken(PeekToken(index).Kind))
        {
            return index;
        }

        index++;
        while (PeekToken(index).Kind == SyntaxKind.ColonToken)
        {
            index++;
            if (IsNameToken(PeekToken(index).Kind))
            {
                index++;
            }
            else
            {
                break;
            }
        }

        if (PeekToken(index).Kind == SyntaxKind.HashToken)
        {
            index++;
            if (PeekToken(index).Kind == SyntaxKind.IdentifierToken)
            {
                index++;
            }
        }

        if (PeekToken(index).Kind == SyntaxKind.OpenBracketToken)
        {
            index++;
            if (PeekToken(index).Kind == SyntaxKind.CloseBracketToken)
            {
                index++;
            }
        }

        return index;
    }

    private SyntaxToken AttachSkipped(SyntaxToken token)
    {
        var extra = BuildSkippedTrivia();
        if (extra is null || token.Node is not GreenToken green)
        {
            return token;
        }

        var leading = ConcatTrivia(extra, green.Leading);
        var updated = SyntaxTokenCache.Get(green.Kind, green.Text, leading, green.Trailing, green.IsMissing);
        return new SyntaxToken(updated, token.Parent, token.Position);
    }

    private GreenNode? BuildSkippedTrivia()
    {
        if (_skipped.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < _skipped.Count; i++)
        {
            builder.Append(_skipped[i].ToFullString());
        }

        _skipped.Clear();
        var text = builder.ToString();
        return text.Length == 0 ? null : new GreenTrivia(SyntaxKind.SkippedTokensTrivia, text);
    }

    private static GreenNode ConcatTrivia(GreenNode extra, GreenNode? existing)
    {
        if (existing is null)
        {
            return extra;
        }

        var items = new List<GreenNode>();
        AppendFlattened(items, extra);
        AppendFlattened(items, existing);
        return GreenList.From(items)!;
    }

    private static void AppendFlattened(List<GreenNode> items, GreenNode node)
    {
        if (node.Kind == SyntaxKind.List)
        {
            var n = node.SlotCount;
            for (var i = 0; i < n; i++)
            {
                var child = node.GetSlot(i);
                if (child is not null)
                {
                    items.Add(child);
                }
            }

            return;
        }

        items.Add(node);
    }

    private static SyntaxToken Missing(SyntaxKind kind) => SyntaxFactory.MissingToken(kind);

    private static bool HasFlag(List<FlagSyntax> flags, SyntaxKind kind)
    {
        for (var i = 0; i < flags.Count; i++)
        {
            if (flags[i].FlagToken.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsMemberStart(SyntaxKind kind)
        => kind is SyntaxKind.DocCommentToken
            or SyntaxKind.AutoKeyword
            or SyntaxKind.StateKeyword
            or SyntaxKind.FunctionKeyword
            or SyntaxKind.EventKeyword
            or SyntaxKind.PropertyKeyword
            || IsTypeStart(kind)
            || IsDialectMemberKeyword(kind);

    private bool IsDialectMemberKeyword(SyntaxKind kind)
        => IsFo4MemberKeyword(kind) || IsStarfieldMemberKeyword(kind);

    private bool IsFo4MemberKeyword(SyntaxKind kind)
        => (_dialect.SupportsStructs && kind == SyntaxKind.StructKeyword)
            || (_dialect.SupportsGroups && kind == SyntaxKind.GroupKeyword)
            || (_dialect.SupportsCustomEvents && kind == SyntaxKind.CustomEventKeyword);

    private bool IsStarfieldMemberKeyword(SyntaxKind kind)
        => _dialect.SupportsGuards && kind == SyntaxKind.GuardKeyword;

    // Guard is a script-level member *and* a statement. Statement lists must
    // keep parsing it so Function/If/While do not end at the first Guard.
    private bool IsGuardStatementStart(SyntaxKind kind)
        => _dialect.SupportsGuards && kind is SyntaxKind.GuardKeyword or SyntaxKind.TryGuardKeyword;

    private static bool IsNonVariableMemberStart(SyntaxKind kind)
        => kind is SyntaxKind.FunctionKeyword
            or SyntaxKind.EventKeyword
            or SyntaxKind.StateKeyword
            or SyntaxKind.AutoKeyword
            or SyntaxKind.PropertyKeyword
            or SyntaxKind.ImportKeyword;

    private static bool IsNonPropertyMemberStart(SyntaxKind kind)
        => kind is SyntaxKind.FunctionKeyword
            or SyntaxKind.EventKeyword
            or SyntaxKind.StateKeyword
            or SyntaxKind.AutoKeyword
            or SyntaxKind.ImportKeyword;

    // Skyrim lexes `is` as an identifier; recover it as a cast so bind can emit PAP1004.
    private bool LooksLikeIsOperator()
        => _current.Kind == SyntaxKind.IsKeyword
            || (_current.Kind == SyntaxKind.IdentifierToken
                && _current.Text.Equals("is", StringComparison.OrdinalIgnoreCase));

    private static bool IsTypeStart(SyntaxKind kind) => IsNameToken(kind);

    private static bool IsNameToken(SyntaxKind kind)
        => kind is SyntaxKind.IdentifierToken
            or SyntaxKind.IntKeyword
            or SyntaxKind.FloatKeyword
            or SyntaxKind.StringKeyword
            or SyntaxKind.BoolKeyword
            or SyntaxKind.VarKeyword
            or SyntaxKind.CustomEventNameKeyword
            or SyntaxKind.ScriptEventNameKeyword;

    private static bool IsKnownFlagKeyword(SyntaxKind kind)
        => kind is SyntaxKind.NativeKeyword
            or SyntaxKind.HiddenKeyword
            or SyntaxKind.ConditionalKeyword
            or SyntaxKind.GlobalKeyword
            or SyntaxKind.AutoKeyword
            or SyntaxKind.AutoReadOnlyKeyword
            or SyntaxKind.ConstKeyword
            or SyntaxKind.MandatoryKeyword
            or SyntaxKind.DebugOnlyKeyword
            or SyntaxKind.BetaOnlyKeyword;

    private static bool IsFlag(SyntaxKind kind)
        => IsKnownFlagKeyword(kind) || kind == SyntaxKind.IdentifierToken;

    private static bool IsLiteral(SyntaxKind kind)
        => kind is SyntaxKind.IntegerLiteralToken
            or SyntaxKind.HexLiteralToken
            or SyntaxKind.FloatLiteralToken
            or SyntaxKind.StringLiteralToken
            or SyntaxKind.TrueKeyword
            or SyntaxKind.FalseKeyword
            or SyntaxKind.NoneKeyword;

    private static bool IsEndKeyword(SyntaxKind kind)
        => kind is SyntaxKind.EndFunctionKeyword
            or SyntaxKind.EndEventKeyword
            or SyntaxKind.EndStateKeyword
            or SyntaxKind.EndPropertyKeyword
            or SyntaxKind.EndIfKeyword
            or SyntaxKind.EndWhileKeyword
            or SyntaxKind.EndStructKeyword
            or SyntaxKind.EndGroupKeyword
            or SyntaxKind.EndGuardKeyword;

    private static bool IsMemberEndKeyword(SyntaxKind kind)
        => kind is SyntaxKind.EndFunctionKeyword
            or SyntaxKind.EndEventKeyword
            or SyntaxKind.EndStateKeyword
            or SyntaxKind.EndPropertyKeyword
            or SyntaxKind.EndStructKeyword
            or SyntaxKind.EndGroupKeyword;

    private static bool IsStatementListStop(StatementScope scope, SyntaxKind kind)
    {
        if (kind == SyntaxKind.EndOfFileToken || IsMemberEndKeyword(kind))
        {
            return true;
        }

        return scope switch
        {
            StatementScope.If => kind is SyntaxKind.ElseIfKeyword
                or SyntaxKind.ElseKeyword
                or SyntaxKind.EndIfKeyword
                or SyntaxKind.EndWhileKeyword
                or SyntaxKind.EndGuardKeyword,
            StatementScope.While => kind is SyntaxKind.EndWhileKeyword
                or SyntaxKind.EndIfKeyword
                or SyntaxKind.ElseIfKeyword
                or SyntaxKind.ElseKeyword
                or SyntaxKind.EndGuardKeyword,
            StatementScope.Guard => kind is SyntaxKind.EndGuardKeyword
                or SyntaxKind.EndIfKeyword
                or SyntaxKind.EndWhileKeyword
                or SyntaxKind.ElseKeyword
                or SyntaxKind.ElseIfKeyword,
            _ => false,
        };
    }

    private static bool IsAssignmentOperator(SyntaxKind kind)
        => kind is SyntaxKind.EqualsToken
            or SyntaxKind.PlusEqualsToken
            or SyntaxKind.MinusEqualsToken
            or SyntaxKind.AsteriskEqualsToken
            or SyntaxKind.SlashEqualsToken
            or SyntaxKind.PercentEqualsToken;

    private static bool IsComparisonOperator(SyntaxKind kind)
        => kind is SyntaxKind.EqualsEqualsToken
            or SyntaxKind.ExclamationEqualsToken
            or SyntaxKind.LessThanToken
            or SyntaxKind.GreaterThanToken
            or SyntaxKind.LessThanEqualsToken
            or SyntaxKind.GreaterThanEqualsToken;

    private static bool IsMemberNameToken(SyntaxKind kind)
        => IsNameToken(kind) || kind == SyntaxKind.LengthKeyword;

    private static bool IsExpressionStart(SyntaxKind kind)
        => IsLiteral(kind)
            || IsNameToken(kind)
            || kind is SyntaxKind.MinusToken
                or SyntaxKind.ExclamationToken
                or SyntaxKind.OpenParenToken
                or SyntaxKind.NewKeyword
                or SyntaxKind.AsKeyword
                or SyntaxKind.IsKeyword
                or SyntaxKind.LengthKeyword;

    private static bool IsStatementKeyword(SyntaxKind kind)
        => kind is SyntaxKind.IfKeyword
            or SyntaxKind.WhileKeyword
            or SyntaxKind.ReturnKeyword
            or SyntaxKind.ElseKeyword
            or SyntaxKind.ElseIfKeyword
            or SyntaxKind.GuardKeyword
            or SyntaxKind.TryGuardKeyword;

    private static bool IsArgumentListStop(SyntaxKind kind)
        => kind is SyntaxKind.CloseParenToken
            or SyntaxKind.EndOfLineToken
            or SyntaxKind.EndOfFileToken
            || IsStatementKeyword(kind)
            || IsEndKeyword(kind);

    private static bool IsParameterListStop(SyntaxKind kind)
        => kind is SyntaxKind.CloseParenToken
            or SyntaxKind.EndOfLineToken
            or SyntaxKind.EndOfFileToken
            or SyntaxKind.FunctionKeyword
            or SyntaxKind.EventKeyword
            or SyntaxKind.StateKeyword
            or SyntaxKind.PropertyKeyword
            or SyntaxKind.StructKeyword
            or SyntaxKind.GroupKeyword
            or SyntaxKind.CustomEventKeyword
            or SyntaxKind.GuardKeyword
            or SyntaxKind.TryGuardKeyword
            || IsKnownFlagKeyword(kind)
            || IsEndKeyword(kind);

    private static bool IsStatementStop(SyntaxKind kind)
        => kind is SyntaxKind.EndOfLineToken or SyntaxKind.EndOfFileToken
            || IsMemberEndKeyword(kind);

    private static bool IsLineTerminator(SyntaxKind kind)
        => kind is SyntaxKind.EndOfLineToken
            or SyntaxKind.EndOfFileToken
            or SyntaxKind.ImportKeyword
            or SyntaxKind.FunctionKeyword
            or SyntaxKind.EndFunctionKeyword
            or SyntaxKind.EventKeyword
            or SyntaxKind.EndEventKeyword
            or SyntaxKind.StateKeyword
            or SyntaxKind.EndStateKeyword
            or SyntaxKind.PropertyKeyword
            or SyntaxKind.EndPropertyKeyword
            or SyntaxKind.AutoKeyword
            or SyntaxKind.DocCommentToken
            or SyntaxKind.IfKeyword
            or SyntaxKind.WhileKeyword
            or SyntaxKind.ReturnKeyword
            or SyntaxKind.ElseKeyword
            or SyntaxKind.ElseIfKeyword
            or SyntaxKind.EndIfKeyword
            or SyntaxKind.EndWhileKeyword
            or SyntaxKind.IntKeyword
            or SyntaxKind.FloatKeyword
            or SyntaxKind.StringKeyword
            or SyntaxKind.BoolKeyword
            or SyntaxKind.VarKeyword
            or SyntaxKind.StructKeyword
            or SyntaxKind.EndStructKeyword
            or SyntaxKind.GroupKeyword
            or SyntaxKind.EndGroupKeyword
            or SyntaxKind.CustomEventKeyword
            or SyntaxKind.GuardKeyword
            or SyntaxKind.EndGuardKeyword
            or SyntaxKind.TryGuardKeyword;

    private enum StatementScope
    {
        Function,
        Event,
        If,
        While,
        Guard,
    }
}
