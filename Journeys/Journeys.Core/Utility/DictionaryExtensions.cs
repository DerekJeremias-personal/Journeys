namespace Journeys.Core.Utility;

public static class DictionaryExtensions
{
    public static void Update<TKey, TValue>(this IDictionary<TKey, TValue> target, IDictionary<TKey, TValue> source)
    {
        foreach (var (key, value) in source)
            target[key] = value;
    }
}
