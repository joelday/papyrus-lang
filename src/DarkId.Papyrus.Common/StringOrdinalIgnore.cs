using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.Common
{
    /// <summary>
    /// Class to represent a string that is always compared with ordinals ignored.   Only necessary as DynamicData can't provide comparer functionality.
    /// https://github.com/reactiveui/DynamicData/pull/373
    /// </summary>
    public struct StringOrdinalIgnore : IComparable<StringOrdinalIgnore>, IEquatable<StringOrdinalIgnore>, IComparable<string>, IEquatable<string>
    {
        public readonly string String;

        public StringOrdinalIgnore(string str)
        {
            String = str;
        }

        public int CompareTo(StringOrdinalIgnore other)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(String, other.String);
        }

        public int CompareTo(string other)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(String, other);
        }

        public bool Equals(StringOrdinalIgnore other)
        {
            return string.Equals(String, other.String, StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(string other)
        {
            return string.Equals(String, other, StringComparison.OrdinalIgnoreCase);
        }

        public static implicit operator string(StringOrdinalIgnore s)
        {
            return s.String;
        }

        public static implicit operator StringOrdinalIgnore(string s)
        {
            return new StringOrdinalIgnore(s);
        }
    }
}
