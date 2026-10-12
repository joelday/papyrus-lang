using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Papyrus.CodeAnalysis;

/// <summary>
/// A Creation Kit user-flags file (<c>TESV_Papyrus_Flags.flg</c>,
/// <c>Institute_Papyrus_Flags.flg</c>, ...). The grammar follows the CK files and
/// Caprica's <c>CapricaUserFlagsParser</c>; whitespace and <c>//</c> / <c>/* */</c>
/// comments are ignored and keywords are case-insensitive:
/// <code>
/// Flag &lt;name&gt; &lt;index&gt;                          // allowed everywhere
/// Flag &lt;name&gt; &lt;index&gt; { Script Property ... }  // allowed only on the listed kinds
/// Flag &lt;name&gt; &lt;child&gt; (&amp; &lt;child&gt;)+           // composite of earlier flags
/// </code>
/// Location kinds are <c>Script</c>, <c>Property</c>, <c>Variable</c>, <c>Function</c>, and
/// (FO4+) <c>StructVar</c> and <c>Group</c>.
/// </summary>
public sealed class FlagsFile
{
    public const string BitIndexOutOfRangeDiagnosticId = DiagnosticCatalog.UserFlagBitIndexOutOfRangeId;
    public const string TooManyFlagsDiagnosticId = DiagnosticCatalog.TooManyNamedUserFlagsId;
    public const string InvalidSyntaxDiagnosticId = DiagnosticCatalog.InvalidFlagsFileId;

    // Bits 0-31 are the 32-bit mask; the 33rd flag with its own bit cannot fit.
    private const int MaxBitIndex = 31;
    private const int MaxNamedFlags = 32;

    private FlagsFile(ImmutableArray<UserFlag> flags, ImmutableArray<DiagnosticInfo> diagnostics)
    {
        Flags = flags;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<UserFlag> Flags { get; }

    public ImmutableArray<DiagnosticInfo> Diagnostics { get; }

    public static FlagsFile Parse(string? text)
    {
        return new Parser(text ?? string.Empty).Parse();
    }

    /// <summary>Case-insensitive lookup by flag name.</summary>
    public UserFlag? Find(string name)
    {
        foreach (var flag in Flags)
        {
            if (string.Equals(flag.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return flag;
            }
        }

        return null;
    }

    private enum TokenKind
    {
        End,
        Identifier,
        Integer,
        LeftBrace,
        RightBrace,
        Ampersand,
        Invalid,
    }

    private readonly record struct Token(TokenKind Kind, string Text, int Line);

    private sealed class Parser
    {
        private readonly string _text;
        private readonly List<UserFlag> _flags = new();
        private readonly List<DiagnosticInfo> _diagnostics = new();
        private int _position;
        private int _line = 1;
        private Token _current;

        public Parser(string text)
        {
            _text = text;
            _current = Lex();
        }

        public FlagsFile Parse()
        {
            while (_current.Kind != TokenKind.End)
            {
                if (!IsKeyword(_current, "Flag"))
                {
                    Error($"expected 'Flag' but found '{_current.Text}'");
                    SkipToNextFlag();
                    continue;
                }

                Advance();
                ParseFlag();
            }

            return new FlagsFile(_flags.ToImmutableArray(), _diagnostics.ToImmutableArray());
        }

        private void ParseFlag()
        {
            if (_current.Kind != TokenKind.Identifier)
            {
                Error($"expected a flag name but found '{_current.Text}'");
                SkipToNextFlag();
                return;
            }

            var name = _current.Text;
            var line = _current.Line;
            Advance();

            UserFlag? flag;
            if (_current.Kind == TokenKind.Integer)
            {
                flag = ParseBitFlag(name);
            }
            else if (_current.Kind == TokenKind.Identifier && !IsKeyword(_current, "Flag"))
            {
                flag = ParseCompositeFlag(name, line);
            }
            else
            {
                Error($"expected a bit index or child flag after '{name}' but found '{_current.Text}'");
                SkipToNextFlag();
                return;
            }

            if (flag is not null)
            {
                Register(flag, line);
            }
        }

        private UserFlag ParseBitFlag(string name)
        {
            var bitIndex = int.TryParse(_current.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : int.MaxValue;
            Advance();

            if (bitIndex > MaxBitIndex)
            {
                _diagnostics.Add(new DiagnosticInfo(DiagnosticCatalog.UserFlagBitIndexOutOfRange, name, bitIndex));
            }

            if (_current.Kind != TokenKind.LeftBrace)
            {
                return new UserFlag(name, bitIndex);
            }

            Advance();
            var locations = UserFlagLocations.None;
            while (_current.Kind is not (TokenKind.RightBrace or TokenKind.End))
            {
                var location = _current.Kind == TokenKind.Identifier ? ParseLocation(_current.Text) : null;
                if (location is null)
                {
                    Error($"unexpected '{_current.Text}' in the location list of '{name}'");
                }
                else
                {
                    locations |= location.Value;
                }

                Advance();
            }

            if (_current.Kind == TokenKind.RightBrace)
            {
                Advance();
            }
            else
            {
                Error($"missing '}}' after the location list of '{name}'");
            }

            return new UserFlag(name, bitIndex, locations);
        }

        private UserFlag? ParseCompositeFlag(string name, int line)
        {
            var mask = 0u;
            var locations = UserFlagLocations.All;
            var valid = true;
            while (true)
            {
                var child = FindFlag(_current.Text);
                if (child is null)
                {
                    Error($"unknown flag '{_current.Text}' in the definition of '{name}'");
                    valid = false;
                }
                else
                {
                    mask |= child.Mask;
                    locations &= child.Locations;
                }

                Advance();
                if (_current.Kind != TokenKind.Ampersand)
                {
                    break;
                }

                Advance();
                if (_current.Kind != TokenKind.Identifier)
                {
                    Error($"expected a child flag after '&' in the definition of '{name}'");
                    return null;
                }
            }

            if (!valid)
            {
                return null;
            }

            if (locations == UserFlagLocations.None)
            {
                Error($"the child flags of '{name}' have no valid locations in common", line);
            }

            return UserFlag.Composite(name, mask, locations);
        }

        private void Register(UserFlag flag, int line)
        {
            if (FindFlag(flag.Name) is not null)
            {
                Error($"a flag named '{flag.Name}' is already defined", line);
                return;
            }

            if (!flag.IsComposite)
            {
                foreach (var existing in _flags)
                {
                    if (!existing.IsComposite && existing.BitIndex == flag.BitIndex)
                    {
                        Error($"'{flag.Name}' reuses bit index {flag.BitIndex} of '{existing.Name}'", line);
                        return;
                    }
                }

                if (CountBitFlags() >= MaxNamedFlags)
                {
                    _diagnostics.Add(new DiagnosticInfo(DiagnosticCatalog.TooManyNamedUserFlags, flag.Name));
                }
            }

            _flags.Add(flag);
        }

        private int CountBitFlags()
        {
            var count = 0;
            foreach (var flag in _flags)
            {
                if (!flag.IsComposite)
                {
                    count++;
                }
            }

            return count;
        }

        private UserFlag? FindFlag(string name)
        {
            foreach (var flag in _flags)
            {
                if (string.Equals(flag.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return flag;
                }
            }

            return null;
        }

        private static UserFlagLocations? ParseLocation(string text)
        {
            return text.ToUpperInvariant() switch
            {
                "SCRIPT" => UserFlagLocations.Script,
                "PROPERTY" => UserFlagLocations.Property,
                "VARIABLE" => UserFlagLocations.Variable,
                "STRUCTVAR" => UserFlagLocations.StructVar,
                "FUNCTION" => UserFlagLocations.Function,
                "GROUP" => UserFlagLocations.Group,
                _ => null,
            };
        }

        private void SkipToNextFlag()
        {
            do
            {
                Advance();
            }
            while (_current.Kind != TokenKind.End && !IsKeyword(_current, "Flag"));
        }

        private void Error(string message, int? line = null)
        {
            _diagnostics.Add(new DiagnosticInfo(DiagnosticCatalog.InvalidFlagsFile, line ?? _current.Line, message));
        }

        private static bool IsKeyword(Token token, string keyword)
            => token.Kind == TokenKind.Identifier && string.Equals(token.Text, keyword, StringComparison.OrdinalIgnoreCase);

        private void Advance()
        {
            _current = Lex();
        }

        private Token Lex()
        {
            SkipTrivia();
            if (_position >= _text.Length)
            {
                return new Token(TokenKind.End, "end of file", _line);
            }

            var start = _position;
            var c = _text[_position];
            switch (c)
            {
                case '{':
                    _position++;
                    return new Token(TokenKind.LeftBrace, "{", _line);
                case '}':
                    _position++;
                    return new Token(TokenKind.RightBrace, "}", _line);
                case '&':
                    _position++;
                    return new Token(TokenKind.Ampersand, "&", _line);
            }

            if (char.IsAsciiDigit(c))
            {
                while (_position < _text.Length && char.IsAsciiDigit(_text[_position]))
                {
                    _position++;
                }

                return new Token(TokenKind.Integer, _text[start.._position], _line);
            }

            if (char.IsAsciiLetter(c) || c == '_')
            {
                while (_position < _text.Length && (char.IsAsciiLetterOrDigit(_text[_position]) || _text[_position] == '_'))
                {
                    _position++;
                }

                return new Token(TokenKind.Identifier, _text[start.._position], _line);
            }

            _position++;
            return new Token(TokenKind.Invalid, c.ToString(), _line);
        }

        private void SkipTrivia()
        {
            while (_position < _text.Length)
            {
                var c = _text[_position];
                if (c == '\n')
                {
                    _line++;
                    _position++;
                }
                else if (c is ' ' or '\t' or '\r' or '﻿')
                {
                    _position++;
                }
                else if (c == '/' && Peek() == '/')
                {
                    while (_position < _text.Length && _text[_position] != '\n')
                    {
                        _position++;
                    }
                }
                else if (c == '/' && Peek() == '*')
                {
                    _position += 2;
                    while (_position < _text.Length && !(_text[_position] == '*' && Peek() == '/'))
                    {
                        if (_text[_position] == '\n')
                        {
                            _line++;
                        }

                        _position++;
                    }

                    if (_position < _text.Length)
                    {
                        _position += 2;
                    }
                    else
                    {
                        _diagnostics.Add(new DiagnosticInfo(
                            DiagnosticCatalog.InvalidFlagsFile,
                            _line,
                            "unterminated /* comment"));
                    }
                }
                else
                {
                    return;
                }
            }
        }

        private char Peek() => _position + 1 < _text.Length ? _text[_position + 1] : '\0';
    }
}
