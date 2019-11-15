using System;
using DarkId.Papyrus.Common;

namespace DarkId.Papyrus.LanguageService.Syntax.Lexer
{
    public readonly struct ScriptToken : IEquatable<ScriptToken>
    {
        public SyntaxKind Kind { get; }
        public string Text { get; }

        public ScriptToken(SyntaxKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public bool Equals(ScriptToken other)
        {
            return Kind == other.Kind && Text == other.Text;
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
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"{nameof(Kind)}: {Kind}, {nameof(Text)}: \"{Text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")}\"";
        }
    }
}
