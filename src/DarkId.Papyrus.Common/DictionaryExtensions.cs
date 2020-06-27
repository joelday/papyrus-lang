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

        public static void SynchronizeWithFactory<K, V>(
            this Dictionary<K, V> dict,
            HashSet<K> newKeys, Func<K, V> createValueForKey, 
            bool disposeRemovedDisposables = true, 
            Func<K, V, V> updateOrReplaceExisting = null,
            Action<K, V> preDisposalHandler = null)
            where V : class
        {
            var currentKeys = new HashSet<K>(dict.Keys);

            var existingKeys = currentKeys.Intersect(newKeys);
            var keysToRemove = currentKeys.Except(newKeys);
            var keysToAdd = newKeys.Except(currentKeys);

            foreach (var keyToAdd in keysToAdd)
            {
                var value = createValueForKey(keyToAdd);
                if (value != null)
                {
                    dict.Add(keyToAdd, value);
                }
            }

            foreach (var keyToRemove in keysToRemove)
            {
                if (disposeRemovedDisposables)
                {
                    if (dict.TryGetValue(keyToRemove, out var val))
                    {
                        if (val is IDisposable asDisposable)
                        {
                            updateOrReplaceExisting?.Invoke(keyToRemove, val);
                            preDisposalHandler?.Invoke(keyToRemove, val);
                            asDisposable.Dispose();
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"Key to be removed could not be located: {keyToRemove}");
                    }
                }

                dict.Remove(keyToRemove);
            }

            if (updateOrReplaceExisting != null)
            {
                foreach (var existingKey in existingKeys)
                {
                    var existingValue = dict[existingKey];
                    var newValue = updateOrReplaceExisting(existingKey, existingValue);
                    if (newValue != existingValue)
                    {
                        dict.Remove(existingKey);
                        dict.Add(existingKey, newValue);
                    }
                }
            }
        }
    }
}