using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    public struct TextRange : IEquatable<TextRange>
    {
        public static readonly TextRange Empty = default;

        public TextPosition Start { get; }
        public TextPosition End { get; }

        public TextRange(TextPosition start, TextPosition end)
        {
            Start = start;
            End = end;
        }

        public bool Equals(TextRange other)
        {
            return Start.Equals(other.Start) && End.Equals(other.End);
        }

        public static bool IsEmpty(TextRange range)
        {
            return range.Equals(Empty);
        }

        public override bool Equals(object obj)
        {
            return obj is TextRange textRange && textRange == this;
        }

        public override int GetHashCode()
        {
            var hashCode = -1676728671;
            hashCode = hashCode * -1521134295 + EqualityComparer<TextPosition>.Default.GetHashCode(Start);
            hashCode = hashCode * -1521134295 + EqualityComparer<TextPosition>.Default.GetHashCode(End);
            return hashCode;
        }

        public static bool operator ==(TextRange left, TextRange right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TextRange left, TextRange right)
        {
            return !left.Equals(right);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal string DebuggerDisplay => $"Start = {{ {Start.DebuggerDisplay} }} End = {{ {End.DebuggerDisplay} }}";
    }
}