using DynamicData;
using DynamicData.Kernel;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace DarkId.Papyrus.Common
{
    public static class CacheExtensions
    {
        public enum SetToEnum
        {
            /// <summary>
            /// Clear existing collection, and set it to the new set of values
            /// </summary>
            Whitewash,

            /// <summary>
            /// Adds only new values that don't already exist to the collection
            /// </summary>
            SkipExisting,

            /// <summary>
            /// Sets all new values into the collection, replacing existing collisions
            /// </summary>
            SetExisting,
        }


        public static void SetTo<V, K>(this ISourceCache<V, K> cache, IEnumerable<V> items)
        {
            cache.Clear();
            cache.AddOrUpdate(items);
        }

        public static void SetTo<V, K>(this ISourceCache<V, K> cache, Func<V, K> keySelector, IEnumerable<V> items, SetToEnum setTo = SetToEnum.Whitewash)
        {
            if (setTo == SetToEnum.Whitewash)
            {
                SetTo(cache, items);
                return;
            }
            var toRemove = new HashSet<K>(cache.Keys);
            var keyPairs = items.Select(i => new KeyValuePair<K, V>(keySelector(i), i)).ToArray();
            toRemove.Remove(keyPairs.Select(kv => kv.Key));
            cache.Remove(toRemove);
            switch (setTo)
            {
                case SetToEnum.SkipExisting:
                    foreach (var item in keyPairs)
                    {
                        var lookup = cache.Lookup(item.Key);
                        if (!lookup.HasValue)
                        {
                            cache.AddOrUpdate(item.Value);
                        }
                    }
                    break;
                case SetToEnum.SetExisting:
                    cache.AddOrUpdate(items);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public static bool TryGetValue<TObject, TKey>(this IObservableCache<TObject, TKey> cache, TKey key, [MaybeNullWhen(false)] out TObject value)
        {
            var lookup = cache.Lookup(key);
            if (lookup.HasValue)
            {
                value = lookup.Value;
                return true;
            }
            value = default;
            return false;
        }

        public static IEnumerable<T> WhereLookup<T>(this IEnumerable<Optional<T>> lookups)
        {
            return lookups
                .Where(l => l.HasValue)
                .Select(l => l.Value);
        }

        public static TObject GetValueOrDefault<TObject, TKey>(this IObservableCache<TObject, TKey> cache, TKey key)
        {
            if (cache.TryGetValue(key, out var val))
            {
                return val;
            }
            return default;
        }
    }
}
