using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Extensions
{
    public static class DictionaryExtensions
    {
        public static bool ContainsKeyIgnoreCase<T>(this Dictionary<string, T> dictionary, string key)
        {
            if (dictionary == null || key == null)
                return false;

            foreach (var k in dictionary.Keys)
            {
                if (string.Equals(k, key, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static bool TryGetValueIgnoreCase<T>(this Dictionary<string, T> dictionary, string key, out T? value)
        {
            value = default;
            if (dictionary == null || key == null)
                return false;

            foreach (var kvp in dictionary)
            {
                if (string.Equals(kvp.Key, key, System.StringComparison.OrdinalIgnoreCase))
                {
                    value = kvp.Value;
                    return true;
                }
            }
            return false;
        }
    }

}
