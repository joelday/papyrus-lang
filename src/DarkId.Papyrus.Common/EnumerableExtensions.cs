using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<TSource> WhereNotNull<TSource>(this IEnumerable<TSource> source)
            where TSource : class
        {
            return source.Where(e => e != null);
        }

        public static int IndexOf<TSource>(this IEnumerable<TSource> source, TSource element)
            where TSource : class
        {
            var index = 0;

            foreach (var sourceElement in source)
            {
                if (sourceElement == element)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        public static IEnumerable<TSource> Flatten<TSource>(this IEnumerable<IEnumerable<TSource>> source)
        {
            foreach (var subEnumerable in source)
            {
                foreach (var subElement in subEnumerable)
                {
                    yield return subElement;
                }
            }
        }

        public static string Join<TSource>(this IEnumerable<TSource> source, string separator)
        {
            return string.Join(separator, source);
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> keyComparer = null)
        {
            var keys = new HashSet<TKey>(keyComparer);

            foreach (var element in source)
            {
                if (keys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }
    }
}
