using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    public struct TextPosition : IEquatable<TextPosition>
    {
        public long Line { get; }
        public long Character { get; }

        public TextPosition(long line, long character)
        {
            Line = line;
            Character = character;
        }

        public bool Equals(TextPosition other)
        {
            return Line == other.Line && Character == other.Character;
        }

        public override bool Equals(object obj)
        {
            return obj is TextPosition && (TextPosition)obj == this;
        }

        public override int GetHashCode()
        {
            var hashCode = 1927683087;
            hashCode = hashCode * -1521134295 + Line.GetHashCode();
            hashCode = hashCode * -1521134295 + Character.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(TextPosition left, TextPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TextPosition left, TextPosition right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(TextPosition left, TextPosition right)
        {
            return left.Line < right.Line || (left.Line == right.Line && left.Character < right.Character);
        }

        public static bool operator >(TextPosition left, TextPosition right)
        {
            return left.Line > right.Line || (left.Line == right.Line && left.Character > right.Character);
        }

        public static bool operator <=(TextPosition left, TextPosition right)
        {
            return left < right || left == right;
        }

        public static bool operator >=(TextPosition left, TextPosition right)
        {
            return left > right || left == right;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal string DebuggerDisplay => $"Line = {Line} Character = {Character}";
    }
}