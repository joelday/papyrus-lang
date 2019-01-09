using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public struct Range : IEquatable<Range>
    {
        public static readonly Range Empty = new Range();

        public Position Start { get; set; }
        public Position End { get; set; }

        public bool Equals(Range other)
        {
            return Start.Equals(other.Start) && End.Equals(other.End);
        }

        public static bool IsEmpty(Range range)
        {
            return range.Equals(Empty);
        }

        public override bool Equals(object obj)
        {
            return obj is Range && (Range)obj == this;
        }

        public override int GetHashCode()
        {
            var hashCode = -1676728671;
            hashCode = hashCode * -1521134295 + EqualityComparer<Position>.Default.GetHashCode(Start);
            hashCode = hashCode * -1521134295 + EqualityComparer<Position>.Default.GetHashCode(End);
            return hashCode;
        }

        public static bool operator ==(Range left, Range right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Range left, Range right)
        {
            return !left.Equals(right);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal string DebuggerDisplay => $"Start = {{ {Start.DebuggerDisplay} }} End = {{ {End.DebuggerDisplay} }}";
    }
}