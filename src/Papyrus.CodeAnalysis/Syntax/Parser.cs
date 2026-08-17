using System.Collections.Generic;
using System.Text;
using System.Threading;
using Papyrus.CodeAnalysis.Syntax.InternalSyntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Syntax;

internal sealed class Parser
{
    private readonly Lexer _lexer;
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
        _lexer = new Lexer(text, dialect, options);
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
        var name = ParseIdentifierName();

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
            flags.Add(SyntaxFactory.Flag(Take()));
        }

        return flags;
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
                    or SyntaxKind.EndEventKeyword)
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
        return SyntaxFactory.EqualsValueClause(equals, ParsePrimaryExpression());
    }

    private List<StatementSyntax> ParseStatements(StatementScope scope)
    {
        var statements = new List<StatementSyntax>();
        while (!_cancellationToken.IsCancellationRequested)
        {
            SkipEndOfLines();
            if (_cancellationToken.IsCancellationRequested
                || IsStatementListStop(scope, _current.Kind)
                || LooksLikeDeclarationStart())
            {
                break;
            }

            statements.Add(ParseStatement());
        }

        return statements;
    }

    private StatementSyntax ParseStatement()
    {
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
        var left = ParseExpression(stopAtAssignment: true);
        var operatorToken = IsAssignmentOperator(_current.Kind)
            ? Take()
            : Missing(SyntaxKind.EqualsToken);
        var right = ParseLineExpression();
        return SyntaxFactory.AssignmentStatement(left, operatorToken, right, EatEndOfLine());
    }

    private ExpressionStatementSyntax ParseExpressionStatement()
    {
        var expression = ParseLineExpression();
        return SyntaxFactory.ExpressionStatement(expression, EatEndOfLine());
    }

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

    private ExpressionSyntax ParseLineExpression()
        => ParseExpression(stopAtAssignment: false);

    private ExpressionSyntax ParseExpression(bool stopAtAssignment)
    {
        var expression = ParseBinaryExpression(stopAtAssignment);
        SkipExpressionRemainder(stopAtAssignment);
        return expression;
    }

    private ExpressionSyntax ParseBinaryExpression(bool stopAtAssignment)
    {
        var left = ParsePrimaryExpression();
        while (IsBinaryOperator(_current.Kind)
            && !(stopAtAssignment && IsAssignmentOperator(_current.Kind)))
        {
            var operatorToken = Take();
            var right = ParsePrimaryExpression();
            left = SyntaxFactory.BinaryExpression(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        if (_current.Kind is SyntaxKind.MinusToken or SyntaxKind.ExclamationToken)
        {
            var operatorToken = Take();
            return SyntaxFactory.PrefixUnaryExpression(operatorToken, ParsePrimaryExpression());
        }

        if (IsLiteral(_current.Kind))
        {
            return SyntaxFactory.LiteralExpression(Take());
        }

        if (IsNameToken(_current.Kind))
        {
            return SyntaxFactory.NameExpression(ParseName());
        }

        return SyntaxFactory.LiteralExpression(Missing(SyntaxKind.IntegerLiteralToken));
    }

    private void SkipExpressionRemainder(bool stopAtAssignment)
    {
        var depth = 0;
        while (!IsExpressionRemainderStop(_current.Kind, depth, stopAtAssignment))
        {
            if (_current.Kind is SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken)
            {
                depth++;
            }
            else if (depth > 0
                && _current.Kind is SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken)
            {
                depth--;
            }

            Skip();
        }
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

        return LooksLikeTypedDeclaration();
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

    private static bool IsMemberStart(SyntaxKind kind)
        => kind is SyntaxKind.DocCommentToken
            or SyntaxKind.AutoKeyword
            or SyntaxKind.StateKeyword
            or SyntaxKind.FunctionKeyword
            or SyntaxKind.EventKeyword
            or SyntaxKind.PropertyKeyword
            || IsTypeStart(kind);

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
            or SyntaxKind.EndWhileKeyword;

    private static bool IsMemberEndKeyword(SyntaxKind kind)
        => kind is SyntaxKind.EndFunctionKeyword
            or SyntaxKind.EndEventKeyword
            or SyntaxKind.EndStateKeyword
            or SyntaxKind.EndPropertyKeyword;

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
                or SyntaxKind.EndWhileKeyword,
            StatementScope.While => kind is SyntaxKind.EndWhileKeyword
                or SyntaxKind.EndIfKeyword
                or SyntaxKind.ElseIfKeyword
                or SyntaxKind.ElseKeyword,
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

    private static bool IsBinaryOperator(SyntaxKind kind)
        => kind is SyntaxKind.BarBarToken
            or SyntaxKind.AmpersandAmpersandToken
            or SyntaxKind.EqualsEqualsToken
            or SyntaxKind.ExclamationEqualsToken
            or SyntaxKind.LessThanToken
            or SyntaxKind.GreaterThanToken
            or SyntaxKind.LessThanEqualsToken
            or SyntaxKind.GreaterThanEqualsToken
            or SyntaxKind.PlusToken
            or SyntaxKind.MinusToken
            or SyntaxKind.AsteriskToken
            or SyntaxKind.SlashToken
            or SyntaxKind.PercentToken;

    private static bool IsExpressionStart(SyntaxKind kind)
        => IsLiteral(kind)
            || IsNameToken(kind)
            || kind is SyntaxKind.MinusToken
                or SyntaxKind.ExclamationToken
                or SyntaxKind.OpenParenToken
                or SyntaxKind.NewKeyword;

    private static bool IsStatementKeyword(SyntaxKind kind)
        => kind is SyntaxKind.IfKeyword
            or SyntaxKind.WhileKeyword
            or SyntaxKind.ReturnKeyword
            or SyntaxKind.ElseKeyword
            or SyntaxKind.ElseIfKeyword;

    private static bool IsExpressionRemainderStop(SyntaxKind kind, int depth, bool stopAtAssignment)
    {
        if (kind is SyntaxKind.EndOfLineToken or SyntaxKind.EndOfFileToken)
        {
            return true;
        }

        if (depth != 0)
        {
            return false;
        }

        if (stopAtAssignment && IsAssignmentOperator(kind))
        {
            return true;
        }

        return IsStatementKeyword(kind)
            || IsEndKeyword(kind)
            || kind is SyntaxKind.FunctionKeyword
                or SyntaxKind.EventKeyword
                or SyntaxKind.StateKeyword
                or SyntaxKind.PropertyKeyword
                or SyntaxKind.ImportKeyword
                or SyntaxKind.AutoKeyword
                or SyntaxKind.DocCommentToken;
    }

    private static bool IsParameterListStop(SyntaxKind kind)
        => kind is SyntaxKind.CloseParenToken
            or SyntaxKind.EndOfLineToken
            or SyntaxKind.EndOfFileToken
            or SyntaxKind.FunctionKeyword
            or SyntaxKind.EventKeyword
            or SyntaxKind.StateKeyword
            or SyntaxKind.PropertyKeyword
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
            or SyntaxKind.VarKeyword;

    private enum StatementScope
    {
        Function,
        Event,
        If,
        While,
    }
}
