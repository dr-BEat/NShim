using System;
using System.Collections.Generic;
using System.Text;

namespace NShim.Helpers
{
    internal static class DictionaryExtensions
    {
        public static bool TryAdd<T, U>(this Dictionary<T, U> dictionary, T key, U value)
        {
            if (dictionary.ContainsKey(key))
                return false;
            dictionary.Add(key, value);
            return true;
        }

        public static bool TryAdd<T, U>(this Dictionary<T, U> dictionary, T key, Func<U> valueFunc)
        {
            if (dictionary.ContainsKey(key))
                return false;
            dictionary.Add(key, valueFunc());
            return true;
        }
    }
}
