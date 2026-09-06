using System;
using System.Collections.Generic;

namespace RimTalk.Memory.Utils;

public static class DictionaryExtensions
{
    public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key) where TValue : new()
    {
        if (!dictionary.TryGetValue(key, out TValue value))
            dictionary[key] = value = new TValue();

        return value;
    }
    public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TValue> valueFactory)
    {
        if (!dictionary.TryGetValue(key, out TValue value))
            dictionary[key] = value = valueFactory();

        return value;
    }
    public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> valueFactory)
    {
        if (!dictionary.TryGetValue(key, out TValue value))
            dictionary[key] = value = valueFactory(key);

        return value;
    }
}
