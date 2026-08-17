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

        var nativeKeyword = TryEat(SyntaxKind.NativeKeyword);
        var hiddenKeyword = TryEat(SyntaxKind.HiddenKeyword);
        var conditionalKeyword = TryEat(SyntaxKind.ConditionalKeyword);
        var endOfLineToken = EatEndOfLine();
        var imports = ParseImports();
        var endOfFileToken = EatRemainder();

        return SyntaxFactory.ScriptDeclaration(
            docComment,
            scriptNameKeyword,
            name,
            extendsClause,
            nativeKeyword,
            hiddenKeyword,
            conditionalKeyword,
            endOfLineToken,
            SyntaxFactory.List(imports),
            endOfFileToken);
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
            if (_current.Kind != SyntaxKind.ImportKeyword)
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
        if (_current.Kind == SyntaxKind.IdentifierToken)
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

    private SyntaxToken TryEat(SyntaxKind kind)
    {
        if (_current.Kind == kind)
        {
            return Take();
        }

        return default;
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
        while (_current.Kind != SyntaxKind.EndOfFileToken && !_cancellationToken.IsCancellationRequested)
        {
            Skip();
        }

        return Take();
    }

    private void SkipEndOfLines()
    {
        while (_current.Kind == SyntaxKind.EndOfLineToken && !_cancellationToken.IsCancellationRequested)
        {
            Skip();
        }
    }

    private SyntaxToken Take()
    {
        var token = AttachSkipped(_current);
        _current = _lexer.Lex();
        return token;
    }

    private void Skip()
    {
        if (_current.Kind == SyntaxKind.EndOfFileToken)
        {
            return;
        }

        _skipped.Add(_current);
        _current = _lexer.Lex();
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

    private static bool IsLineTerminator(SyntaxKind kind)
        => kind is SyntaxKind.EndOfLineToken or SyntaxKind.ImportKeyword or SyntaxKind.EndOfFileToken;
}
