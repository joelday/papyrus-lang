using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.Common
{
    public static class DictionaryExtensions
    {
        public static Dictionary<K, V> ToDictionary<K, V>(this IEnumerable<KeyValuePair<K, V>> pairs)
        {
            return pairs.ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        public static Dictionary<K, U> MapValues<K, V, U>(this Dictionary<K, V> dict, Func<K, V, U> mapFunc)
        {
            return dict.AsEnumerable().Select(pair => new KeyValuePair<K, U>(pair.Key, mapFunc(pair.Key, pair.Value))).ToDictionary();
        }
    }
}