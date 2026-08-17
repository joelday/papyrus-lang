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
        var name = _dialect.SupportsNamespaces ? ParseName() : ParseIdentifierName();

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
            statements = SyntaxFactory.List(ParseStatements(SyntaxKind.EndFunctionKeyword));
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
            statements = SyntaxFactory.List(ParseStatements(SyntaxKind.EndEventKeyword));
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
        return SyntaxFactory.EqualsValueClause(equals, ParseSignatureExpression());
    }

    private ExpressionSyntax ParseSignatureExpression()
    {
        if (_current.Kind == SyntaxKind.MinusToken)
        {
            var operatorToken = Take();
            return SyntaxFactory.PrefixUnaryExpression(operatorToken, ParseLiteralExpression());
        }

        return ParseLiteralExpression();
    }

    private LiteralExpressionSyntax ParseLiteralExpression()
    {
        if (IsLiteral(_current.Kind))
        {
            return SyntaxFactory.LiteralExpression(Take());
        }

        return SyntaxFactory.LiteralExpression(Missing(SyntaxKind.IntegerLiteralToken));
    }

    private List<StatementSyntax> ParseStatements(SyntaxKind terminator)
    {
        var statements = new List<StatementSyntax>();
        while (!_cancellationToken.IsCancellationRequested)
        {
            SkipEndOfLines();
            if (_cancellationToken.IsCancellationRequested
                || _current.Kind == terminator
                || _current.Kind == SyntaxKind.EndOfFileToken
                || IsEndKeyword(_current.Kind))
            {
                break;
            }

            // Caprica: Guard / TryGuard ... EndGuard are statements, not Lock/Unlock.
            if (_dialect.SupportsGuards
                && _current.Kind is SyntaxKind.GuardKeyword or SyntaxKind.TryGuardKeyword)
            {
                statements.Add(ParseGuardOrTryGuardStatement());
                continue;
            }

            if (LooksLikeDeclarationStart())
            {
                break;
            }

            statements.Add(ParseSkippedStatement());
        }

        return statements;
    }

    private StatementSyntax ParseGuardOrTryGuardStatement()
    {
        if (_current.Kind == SyntaxKind.TryGuardKeyword)
        {
            var tryGuardKeyword = Take();
            var lockParameters = ParseLockParameters();
            var endOfLine = EatEndOfLine();
            var body = ParseStatements(SyntaxKind.EndGuardKeyword);
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
        var statements = ParseStatements(SyntaxKind.EndGuardKeyword);
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
        // Type keywords are line terminators for declaration recovery, but soup
        // must still consume them (`Int x`, `as Int`, `new Int[5]`).
        if (!IsStatementStop(_current.Kind))
        {
            Skip();
        }

        while (!IsStatementStop(_current.Kind))
        {
            Skip();
        }

        return SyntaxFactory.SkippedStatement(EatEndOfLine());
    }

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
        if (_current.Kind == SyntaxKind.OpenBracketToken)
        {
            openBracket = Take();
            closeBracket = Eat(SyntaxKind.CloseBracketToken);
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
            or SyntaxKind.EndStructKeyword
            or SyntaxKind.EndGroupKeyword
            or SyntaxKind.EndGuardKeyword;

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
            || IsEndKeyword(kind);

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
}
