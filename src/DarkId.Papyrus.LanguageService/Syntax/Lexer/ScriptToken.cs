using System;
using DarkId.Papyrus.Common;

namespace DarkId.Papyrus.LanguageService.Syntax.Lexer
{
    public struct ScriptToken : IEquatable<ScriptToken>
    {
        public SyntaxKind Kind { get; }
        public string Text { get; }
        public TextRange Range { get; }
        public ScriptLexerState LexerState { get; }

        public ScriptToken(SyntaxKind kind, string text, TextRange range, ScriptLexerState lexerState)
        {
            Kind = kind;
            Text = text;
            Range = range;
            LexerState = lexerState;
        }

        public bool Equals(ScriptToken other)
        {
            return Kind == other.Kind && Text == other.Text && Range.Equals(other.Range) && LexerState.Equals(other.LexerState);
        }

        public override bool Equals(object obj)
        {
            return obj is ScriptToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) Kind;
                hashCode = (hashCode * 397) ^ (Text != null ? Text.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Range.GetHashCode();
                hashCode = (hashCode * 397) ^ LexerState.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"{nameof(Kind)}: {Kind}, {nameof(Text)}: \"{Text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")}\", {nameof(Range)}: {{ {Range} }}";
        }
    }
}
