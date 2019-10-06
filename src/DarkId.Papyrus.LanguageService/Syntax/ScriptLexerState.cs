using System;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    public struct ScriptLexerState : IEquatable<ScriptLexerState>
    {
        public ScriptLexerStateFlags Flags { get; set; }
        public string PreviousText { get; set; }
        public int Position { get; set; }
        public int StringLiteralStartPosition { get; set; }
        public SyntaxKind PreviousTokenKind { get; set; }

        public bool Equals(ScriptLexerState other)
        {
            return Flags == other.Flags &&
                   PreviousText == other.PreviousText &&
                   Position == other.Position &&
                   StringLiteralStartPosition == other.StringLiteralStartPosition &&
                   PreviousTokenKind == other.PreviousTokenKind;
        }

        public override bool Equals(object obj)
        {
            return obj is ScriptLexerState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // This is not a reference type.
                // ReSharper disable NonReadonlyMemberInGetHashCode

                var hashCode = (int) Flags;
                hashCode = (hashCode * 397) ^ (PreviousText != null ? PreviousText.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Position;
                hashCode = (hashCode * 397) ^ StringLiteralStartPosition;
                hashCode = (hashCode * 397) ^ (int) PreviousTokenKind;

                // ReSharper restore NonReadonlyMemberInGetHashCode

                return hashCode;
            }
        }
    }
}