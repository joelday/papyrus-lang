using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Common
{
    public static class CoreExtensions
    {
        public static bool CaseInsensitiveEquals(this string str, string other)
        {
            return str.Equals(other, StringComparison.OrdinalIgnoreCase);
        }

        public static bool HashCodeEquals<T>(this T a, T b)
        {
            if (a == null && b != null || a != null && b == null)
            {
                return false;
            }

            return a.GetHashCode() == b.GetHashCode();
        }

        public static string NullIfWhitespace(this string str)
        {
            return string.IsNullOrWhiteSpace(str) ? null : str;
        }
    }
}
