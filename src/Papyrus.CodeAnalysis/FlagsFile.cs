using System.Collections.Generic;
using System.Collections.Immutable;

namespace Papyrus.CodeAnalysis;

public sealed class FlagsFile
{
    public const string BitIndexOutOfRangeDiagnosticId = DiagnosticCatalog.UserFlagBitIndexOutOfRangeId;
    public const string TooManyFlagsDiagnosticId = DiagnosticCatalog.TooManyNamedUserFlagsId;

    // Bits 0-31 are the 32-bit mask; the 33rd named flag cannot fit.
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

    private sealed class Parser
    {
        private readonly string _text;
        private readonly List<UserFlag> _flags = new();
        private readonly List<DiagnosticInfo> _diagnostics = new();
        private int _position;
        private int _nextImplicitBit;

        public Parser(string text)
        {
            _text = text;
        }

        public FlagsFile Parse()
        {
            SkipTrivia();
            while (!IsAtEnd)
            {
                if (!TryParseFlag())
                {
                    _position++;
                }

                SkipTrivia();
            }

            return new FlagsFile(_flags.ToImmutableArray(), _diagnostics.ToImmutableArray());
        }

        private bool TryParseFlag()
        {
            if (!IsIdentifierStart(Current))
            {
                return false;
            }

            var name = ReadIdentifier();
            SkipTrivia();

            int bitIndex;
            if (Current == '{')
            {
                if (!TryParseBitIndex(out bitIndex))
                {
                    bitIndex = _nextImplicitBit++;
                }
            }
            else
            {
                bitIndex = _nextImplicitBit++;
            }

            if (bitIndex > MaxBitIndex)
            {
                _diagnostics.Add(new DiagnosticInfo(
                    DiagnosticCatalog.UserFlagBitIndexOutOfRange,
                    name,
                    bitIndex));
            }

            if (_flags.Count >= MaxNamedFlags)
            {
                _diagnostics.Add(new DiagnosticInfo(
                    DiagnosticCatalog.TooManyNamedUserFlags,
                    name));
            }

            _flags.Add(new UserFlag(name, bitIndex));
            return true;
        }

        private bool TryParseBitIndex(out int bitIndex)
        {
            _position++;
            SkipTrivia();

            var hasIndex = TryReadInteger(out bitIndex);
            SkipTrivia();
            if (Current == '}')
            {
                _position++;
            }
            else
            {
                SkipUntil('}');
            }

            return hasIndex;
        }

        private bool TryReadInteger(out int value)
        {
            if (!IsDigit(Current))
            {
                value = 0;
                return false;
            }

            var acc = 0L;
            while (IsDigit(Current))
            {
                acc = (acc * 10) + (Current - '0');
                _position++;
                if (acc > int.MaxValue)
                {
                    acc = int.MaxValue;
                    while (IsDigit(Current))
                    {
                        _position++;
                    }

                    break;
                }
            }

            value = (int)acc;
            return true;
        }

        private string ReadIdentifier()
        {
            var start = _position;
            _position++;
            while (IsIdentifierPart(Current))
            {
                _position++;
            }

            return _text.Substring(start, _position - start);
        }

        private void SkipTrivia()
        {
            while (!IsAtEnd)
            {
                var c = Current;
                if (c == ' ' || c == '\t' || c == '\uFEFF' || c == '\r' || c == '\n')
                {
                    _position++;
                    continue;
                }

                if (c == ';')
                {
                    SkipComment();
                    continue;
                }

                return;
            }
        }

        private void SkipComment()
        {
            _position++;
            if (Current == '/')
            {
                _position++;
                while (!IsAtEnd)
                {
                    if (Current == '/' && Peek() == ';')
                    {
                        _position += 2;
                        return;
                    }

                    _position++;
                }

                return;
            }

            while (!IsAtEnd && Current != '\r' && Current != '\n')
            {
                _position++;
            }
        }

        private void SkipUntil(char terminator)
        {
            while (!IsAtEnd && Current != terminator)
            {
                if (Current == ';')
                {
                    SkipComment();
                    continue;
                }

                _position++;
            }

            if (Current == terminator)
            {
                _position++;
            }
        }

        private bool IsAtEnd => _position >= _text.Length;

        private char Current => _position < _text.Length ? _text[_position] : '\0';

        private char Peek() => _position + 1 < _text.Length ? _text[_position + 1] : '\0';

        private static bool IsIdentifierStart(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';

        private static bool IsIdentifierPart(char c)
            => IsIdentifierStart(c) || IsDigit(c);

        private static bool IsDigit(char c) => c >= '0' && c <= '9';
    }
}
