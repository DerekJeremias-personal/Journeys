using Backend.Dto.Structures.Model;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Caching
{
    public class ModelCache
    {
        public const string MODEL_TYPE = "loyalty";
        public const int PAGE_SIZE = 500;

        private readonly IModelAdapter _modelAdapter;
        private readonly TimeSpan _cacheDuration;
        private readonly IMemoryCache _memoryCache;
        private readonly IPointAccountTypeAdapter _adapter;
        private readonly ILogger<ModelCache> _logger;
        private readonly ConcurrentDictionary<string, string> _cacheKeys;
        private readonly ConcurrentDictionary<string, List<string>> _tenantKeys;

        private static readonly object _lock = new object();
        
        public ModelCache(
            IMemoryCache memoryCache,
            IModelAdapter modelAdapter,
            ILogger<ModelCache> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _modelAdapter = modelAdapter;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheDuration = TimeSpan.FromMinutes(10);
            _cacheKeys = new ConcurrentDictionary<string, string>();
            _tenantKeys = new ConcurrentDictionary<string, List<string>>();
        }

        private async Task HydrateCache(string tenantId)
        {
            PagedResultSet<ModelDto> models = null;

            //TODO: Likely need to page through if we hit the 500
            //For now this will be sufficient, but needs adjusted to go beyond 500 objects.
            models = await _modelAdapter.GetModels(tenantId, MODEL_TYPE, pageSize: 500);
            _tenantKeys.AddOrUpdate(tenantId, new List<string>(), (key, oldValue) => oldValue);
            if (models?.Entities != null)
            {
                foreach (var model in models.Entities)
                {
                    var cacheKey = $"{tenantId}_{model.Name}".ToLower();
                    _memoryCache.Set(
                        cacheKey,
                        model,
                        new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = _cacheDuration
                        });

                    _cacheKeys[model.ID] = model.Name.ToLower(); // Track the key and Id Mapping
                    _tenantKeys[tenantId].Add(model.Name.ToLower());
                    _logger.LogInformation("Cached model with key {CacheKey} for {CacheDuration}.", cacheKey, _cacheDuration);
                }
            }
            else
            {
                throw new NullReferenceException($"No Models could be found for {tenantId}.");
            }
        }

        public async Task<ModelDto> GetOrLoadById(string tenantId, string modelId)
        {
            if (_cacheKeys.ContainsKey(modelId))
            {
                return await GetOrLoadByName(tenantId, _cacheKeys[modelId]);
            }
            else
            {
                await HydrateCache(tenantId);
                if (!_cacheKeys.ContainsKey(modelId))
                {
                    throw new KeyNotFoundException($"Model {modelId} for tenant {tenantId} does not exist and could not be loaded.");
                }
                else
                {
                    return await GetOrLoadByName(tenantId, _cacheKeys[modelId]);
                }
            }
        }

        public async Task<List<ModelDto>> GetOrLoadAll(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            if(!_tenantKeys.ContainsKey(tenantId) || (_tenantKeys.ContainsKey(tenantId) && _tenantKeys[tenantId].Count == 0))
                await HydrateCache(tenantId);

            var response = new List<ModelDto>();
            var loaded = _tenantKeys[tenantId];
            foreach (var name in loaded)
            {
                var modelDto = await GetOrLoadByName(tenantId, name);
                if(modelDto != null)
                {
                    response.Add(modelDto);
                }
            }
            return response;
        }

        public async Task<ModelDto> GetOrLoadByName(string tenantId, string modelName) 
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(modelName)) throw new ArgumentNullException(nameof(modelName));

            var seekCacheKey = $"{tenantId}_{modelName}".ToLower();
            if (!_memoryCache.TryGetValue(seekCacheKey, out ModelDto? cachedValue))
            {
                _logger.LogInformation($"Cache miss for key {seekCacheKey}. Fetching from adapter.");
                await HydrateCache(tenantId);
                if (!_memoryCache.TryGetValue(seekCacheKey, out ModelDto? secondAttemptCachedValue) || secondAttemptCachedValue == null)
                {
                    throw new KeyNotFoundException($"Model {modelName} for tenant {tenantId} does not exist and could not be loaded.");
                }
                else
                {
                    cachedValue = secondAttemptCachedValue;
                }

            }

            if (cachedValue != null)
            {
                return cachedValue;
            }
            else
            {
                throw new KeyNotFoundException($"Model {modelName} for tenant {tenantId} does not exist and could not be loaded.");
            }
        }

        public async Task<List<ModelDto>> GetRelatedLoyaltyAccountModelsAsync(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            List<ModelDto> returnLst = new List<ModelDto>();
            Dictionary<string, ModelDto> returnLstDic = new Dictionary<string, ModelDto>();

            var loaded = await GetOrLoadAll(tenantId) ?? new List<ModelDto>();
            var select = loaded.Where(x => x.ModelMetaData?.Count > 0).ToList() ?? new List<ModelDto>();
            foreach (var model in select)
            {
                if (model.ModelMetaData != null && model.ModelMetaData.ContainsKey("Wrapper") && !string.IsNullOrEmpty(model.ModelMetaData["Wrapper"]))
                {
                    var sid = model.ModelMetaData["Wrapper"];
                    var wrapper = loaded.FirstOrDefault(x => x.ID == sid);
                    if (wrapper != null && !returnLstDic.ContainsKey(wrapper.ID))
                    {
                        returnLst.Add(wrapper);
                        returnLstDic.Add(wrapper.ID, wrapper);
                    }
                }
            }
            return returnLst;
        }

        public async Task<List<ModelDto>> GetRelatedLoyaltyAccountDetailsModelsAsync(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            var models = await GetRelatedLoyaltyAccountModelsAsync(tenantId) ?? new List<ModelDto>();
            List<ModelDto> returnSet = new List<ModelDto>();
            foreach(var model in models)
            {
                if (model.ModelMetaData != null && model.ModelMetaData.ContainsKey("IsLoyaltyAccount") && bool.TryParse(model.ModelMetaData["IsLoyaltyAccount"], out bool isDetails) && isDetails)
                {
                    returnSet.Add(model);
                }
            }
            return returnSet;
        }
    }
}
