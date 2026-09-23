using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using Papyrus.CodeAnalysis.Syntax.InternalSyntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Syntax;

internal sealed class Lexer
{
    private static readonly SearchValues<char> WhitespaceChars = SearchValues.Create(" \t\uFEFF");
    private static readonly SearchValues<char> IdentifierContinueChars =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_");
    private static readonly SearchValues<char> DigitChars = SearchValues.Create("0123456789");
    private static readonly SearchValues<char> HexDigitChars =
        SearchValues.Create("0123456789abcdefABCDEF");

    private readonly string _source;
    private readonly FrozenDictionary<string, SyntaxKind>.AlternateLookup<ReadOnlySpan<char>> _keywords;
    private readonly bool _champollionCompat;
    private readonly List<GreenNode> _trivia = new();
    private int _offset;
    private int _position;
    private bool _eofReturned;

    public Lexer(SourceText text, PapyrusDialect dialect, PapyrusParseOptions options)
    {
        text ??= SourceText.From(string.Empty);
        dialect ??= PapyrusDialect.FromOptions(options ?? PapyrusParseOptions.Default);
        options ??= PapyrusParseOptions.Default;

        _source = text.ToString();
        _keywords = dialect.Keywords.GetAlternateLookup<ReadOnlySpan<char>>();
        _champollionCompat = options.ChampollionCompat;
    }

    public SyntaxToken Lex()
    {
        if (_eofReturned)
        {
            return CreateToken(SyntaxKind.EndOfFileToken, string.Empty, leading: null);
        }

        var leading = ScanTrivia();
        if (_offset >= _source.Length)
        {
            _eofReturned = true;
            return CreateToken(SyntaxKind.EndOfFileToken, string.Empty, leading);
        }

        var start = _offset;
        var kind = ScanToken();
        var text = _source.Substring(start, _offset - start);
        return CreateToken(kind, text, leading);
    }

    private SyntaxToken CreateToken(SyntaxKind kind, string text, GreenNode? leading)
    {
        var green = SyntaxTokenCache.Get(kind, text, leading, trailing: null, isMissing: false);
        var token = new SyntaxToken(green, parent: null, _position);
        _position += green.FullWidth;
        return token;
    }

    private GreenNode? ScanTrivia()
    {
        _trivia.Clear();
        while (TryScanOneTrivia())
        {
        }

        return GreenList.From(_trivia);
    }

    private bool TryScanOneTrivia()
    {
        if (_offset >= _source.Length)
        {
            return false;
        }

        var c = _source[_offset];
        if (c == ' ' || c == '\t' || c == '\uFEFF')
        {
            ScanWhitespace();
            return true;
        }

        if (c == ';')
        {
            ScanComment();
            return true;
        }

        if (c == '\\' && IsLineContinuation())
        {
            ScanLineContinuation();
            return true;
        }

        return false;
    }

    private void ScanWhitespace()
    {
        var start = _offset;
        var remaining = Remaining();
        var count = remaining.IndexOfAnyExcept(WhitespaceChars);
        _offset += count < 0 ? remaining.Length : count;
        AddTrivia(SyntaxKind.WhitespaceTrivia, start);
    }

    private void ScanComment()
    {
        var start = _offset;
        _offset++;
        if (_offset < _source.Length && _source[_offset] == '/')
        {
            _offset++;
            while (_offset < _source.Length)
            {
                if (_source[_offset] == '/' && _offset + 1 < _source.Length && _source[_offset + 1] == ';')
                {
                    _offset += 2;
                    break;
                }

                _offset++;
            }

            AddTrivia(SyntaxKind.BlockCommentTrivia, start);
            return;
        }

        while (_offset < _source.Length && !IsNewLineChar(_source[_offset]))
        {
            _offset++;
        }

        AddTrivia(SyntaxKind.LineCommentTrivia, start);
    }

    private bool IsLineContinuation()
    {
        var i = _offset + 1;
        while (i < _source.Length && (_source[i] == ' ' || _source[i] == '\t'))
        {
            i++;
        }

        return i < _source.Length && IsNewLineChar(_source[i]);
    }

    private void ScanLineContinuation()
    {
        var start = _offset;
        _offset++;
        while (_offset < _source.Length && (_source[_offset] == ' ' || _source[_offset] == '\t'))
        {
            _offset++;
        }

        ConsumeNewLine();
        AddTrivia(SyntaxKind.LineContinuationTrivia, start);
    }

    private void AddTrivia(SyntaxKind kind, int start)
    {
        _trivia.Add(new GreenTrivia(kind, _source.Substring(start, _offset - start)));
    }

    private SyntaxKind ScanToken()
    {
        var c = _source[_offset];
        if (IsNewLineChar(c))
        {
            ConsumeNewLine();
            return SyntaxKind.EndOfLineToken;
        }

        if (c == '{')
        {
            return ScanDocComment();
        }

        if (c == '"')
        {
            return ScanString();
        }

        if (_champollionCompat && c == ':' && Peek(1) == ':' && IsIdentifierStart(Peek(2)))
        {
            return ScanChampollionIdentifier();
        }

        if (IsIdentifierStart(c))
        {
            return ScanIdentifierOrKeyword();
        }

        if (IsDigit(c))
        {
            return ScanNumber();
        }

        _offset++;
        switch (c)
        {
            case '(':
                return SyntaxKind.OpenParenToken;
            case ')':
                return SyntaxKind.CloseParenToken;
            case '[':
                return SyntaxKind.OpenBracketToken;
            case ']':
                return SyntaxKind.CloseBracketToken;
            case '.':
                return SyntaxKind.DotToken;
            case ',':
                return SyntaxKind.CommaToken;
            case ':':
                return SyntaxKind.ColonToken;
            case '#':
                return SyntaxKind.HashToken;
            case '=':
                return AdvanceIf('=') ? SyntaxKind.EqualsEqualsToken : SyntaxKind.EqualsToken;
            case '!':
                return AdvanceIf('=') ? SyntaxKind.ExclamationEqualsToken : SyntaxKind.ExclamationToken;
            case '+':
                return AdvanceIf('=') ? SyntaxKind.PlusEqualsToken : SyntaxKind.PlusToken;
            case '-':
                return AdvanceIf('=') ? SyntaxKind.MinusEqualsToken : SyntaxKind.MinusToken;
            case '*':
                return AdvanceIf('=') ? SyntaxKind.AsteriskEqualsToken : SyntaxKind.AsteriskToken;
            case '/':
                return AdvanceIf('=') ? SyntaxKind.SlashEqualsToken : SyntaxKind.SlashToken;
            case '%':
                return AdvanceIf('=') ? SyntaxKind.PercentEqualsToken : SyntaxKind.PercentToken;
            case '<':
                return AdvanceIf('=') ? SyntaxKind.LessThanEqualsToken : SyntaxKind.LessThanToken;
            case '>':
                return AdvanceIf('=') ? SyntaxKind.GreaterThanEqualsToken : SyntaxKind.GreaterThanToken;
            case '&':
                return AdvanceIf('&') ? SyntaxKind.AmpersandAmpersandToken : SyntaxKind.BadToken;
            case '|':
                return AdvanceIf('|') ? SyntaxKind.BarBarToken : SyntaxKind.BadToken;
            default:
                return SyntaxKind.BadToken;
        }
    }

    private SyntaxKind ScanDocComment()
    {
        _offset++;
        while (_offset < _source.Length && _source[_offset] != '}')
        {
            _offset++;
        }

        if (_offset < _source.Length)
        {
            _offset++;
        }

        return SyntaxKind.DocCommentToken;
    }

    private SyntaxKind ScanString()
    {
        _offset++;
        while (_offset < _source.Length)
        {
            var c = _source[_offset];
            if (c == '"')
            {
                _offset++;
                break;
            }

            if (IsNewLineChar(c))
            {
                break;
            }

            if (c == '\\' && _offset + 1 < _source.Length && !IsNewLineChar(_source[_offset + 1]))
            {
                _offset += 2;
                continue;
            }

            _offset++;
        }

        return SyntaxKind.StringLiteralToken;
    }

    private SyntaxKind ScanChampollionIdentifier()
    {
        _offset += 2;
        ScanIdentifierTail();
        return SyntaxKind.IdentifierToken;
    }

    private SyntaxKind ScanIdentifierOrKeyword()
    {
        var start = _offset;
        ScanIdentifierTail();
        var span = _source.AsSpan(start, _offset - start);
        return _keywords.TryGetValue(span, out var keyword) ? keyword : SyntaxKind.IdentifierToken;
    }

    private void ScanIdentifierTail()
    {
        var remaining = Remaining();
        var count = remaining.IndexOfAnyExcept(IdentifierContinueChars);
        _offset += count < 0 ? remaining.Length : count;
    }

    private SyntaxKind ScanNumber()
    {
        var remaining = Remaining();
        if (remaining.Length >= 2 && remaining[0] == '0' && (remaining[1] == 'x' || remaining[1] == 'X'))
        {
            _offset += 2;
            AdvanceWhile(HexDigitChars);
            return SyntaxKind.HexLiteralToken;
        }

        AdvanceWhile(DigitChars);
        remaining = Remaining();
        if (remaining.Length > 0 && remaining[0] == '.')
        {
            _offset++;
            AdvanceWhile(DigitChars);
            return SyntaxKind.FloatLiteralToken;
        }

        return SyntaxKind.IntegerLiteralToken;
    }

    private void AdvanceWhile(SearchValues<char> chars)
    {
        var remaining = Remaining();
        var count = remaining.IndexOfAnyExcept(chars);
        _offset += count < 0 ? remaining.Length : count;
    }

    private ReadOnlySpan<char> Remaining() => _source.AsSpan(_offset);

    private bool AdvanceIf(char expected)
    {
        if (_offset < _source.Length && _source[_offset] == expected)
        {
            _offset++;
            return true;
        }

        return false;
    }

    private char Peek(int ahead)
    {
        var index = _offset + ahead;
        return (uint)index < (uint)_source.Length ? _source[index] : '\0';
    }

    private void ConsumeNewLine()
    {
        if (_offset >= _source.Length)
        {
            return;
        }

        if (_source[_offset] == '\r')
        {
            _offset++;
            if (_offset < _source.Length && _source[_offset] == '\n')
            {
                _offset++;
            }

            return;
        }

        if (_source[_offset] == '\n')
        {
            _offset++;
        }
    }

    private static bool IsNewLineChar(char c) => c == '\r' || c == '\n';

    private static bool IsDigit(char c) => (uint)(c - '0') <= 9;

    private static bool IsIdentifierStart(char c)
        => (uint)(c - 'A') <= 'Z' - 'A' || (uint)(c - 'a') <= 'z' - 'a' || c == '_';
}
