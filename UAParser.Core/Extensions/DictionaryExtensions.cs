namespace UAParser.Extensions;

using System;
using System.Collections.Generic;

internal static class DictionaryExtensions
{
    public static TValue Find<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
    {
        if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
        return dictionary.TryGetValue(key, out var result) ? result : default;
    }
}
