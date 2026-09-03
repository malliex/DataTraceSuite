using System.Collections.Generic;

namespace DTS.Extensions;

public static class DictionaryExtensions
{
    public static void InsertOrAppend(this Dictionary<string, string> dictionary, string key, string value)
    {
        dictionary[key] = dictionary.TryGetValue(key, out var existing)
            ? $"{existing.TrimEnd(',')},{value}"
            : value;
    }
}