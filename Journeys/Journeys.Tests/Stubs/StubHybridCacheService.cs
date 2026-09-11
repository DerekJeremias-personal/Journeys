using Journeys.Core.Caching;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Journeys.Tests.Stubs
{
    public class StubHybridCacheService : IHybridCacheService
    {
        private readonly ConcurrentDictionary<string, object> _store = new ConcurrentDictionary<string, object>();

        public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? memoryExpiration = null, TimeSpan? distributedExpiration = null, bool bypassCache = false)
        {
            if (!bypassCache && _store.TryGetValue(key, out var cached) && cached is T typed)
                return typed;
            var value = await factory();
            if (value != null)
                _store[key] = value;
            return value;
        }

        public Task<T?> GetAsync<T>(string key, bool bypassCache = false)
            => Task.FromResult(_store.TryGetValue(key, out var v) && v is T t ? t : default(T?));

        public Task SetAsync<T>(string key, T value, TimeSpan? memoryExpiration = null, TimeSpan? distributedExpiration = null)
        {
            if (value != null)
                _store[key] = value;
            return Task.CompletedTask;
        }

        public Task InvalidateAsync(string key)
            => Task.CompletedTask;

        public Task InvalidatePatternAsync(string pattern)
            => Task.CompletedTask;

        public void InvalidateMemoryCache(string key) { }
    }
}
