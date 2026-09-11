using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Entities;
using Journeys.Core.Models;
using Azure;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public abstract class ReferenceableBaseAdapter<T> : BaseAdapter<T> where T : TenantedModelBase
    {
        private readonly ILogger<ReferenceableBaseAdapter<T>> _logger;
        private readonly IExternalReferenceAdapter _extReferenceAdapter;

        public ReferenceableBaseAdapter(IDynamicDataAdapter dynamicDataAdapter, IExternalReferenceAdapter extReferenceAdapter, ILogger<ReferenceableBaseAdapter<T>> logger) : base(dynamicDataAdapter) 
        {
            _extReferenceAdapter = extReferenceAdapter;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger cannot be null. Please provide a valid logger instance.");
        }

        public async Task<string> GetExtIdAsync(T entity)
        {
            if (entity is IReferenceable referenceable)
            {
                // Call the ExternalId prpoerty directly on the entity via IReferenceable
                return referenceable.ExternalId;
            }

            throw new InvalidOperationException("The provided entity does not implement IReferenceable<T>.");
        }
        public async Task<string> GetExtIdTypeAsync(T entity)
        {
            if (entity is IReferenceable referenceable)
            {
                // Call the ExternalIdType prpoerty directly on the entity via IReferenceable
                return referenceable.ExternalIdType;
            }

            throw new InvalidOperationException("The provided entity does not implement IReferenceable<T>.");
        }

        public virtual async Task<ExternalReference> GetExternalReference(string tenantId, string modelId, string extId, string type)
        {
            try
            {
                if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(extId) && !string.IsNullOrEmpty(type))
                {
                    var extRef = await _extReferenceAdapter.FetchExtReferenceFromIdAsync(tenantId, extId.Trim(), type.Trim(), 100);
                    if (!string.IsNullOrEmpty(extRef?.ContinuationToken)) throw new Exception($"Maximum number of External References has been exceeded for loyalty account: {extId}");

                    return extRef?.Entities?.FirstOrDefault();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching external reference for tenantId: {tenantId}, modelId: {modelId}, extId: {extId}, type: {type}, message: {ex.Message}");
                throw;
            }
        }

        public virtual async Task<T> GetEntityByExtId(string tenantId, string modelId, string extId, string type, string pk1 = null, string pk2 = null)
        {
            var extRef = await GetExternalReference(tenantId, modelId, extId, type);

            return (T)await GetEntityByExtId(tenantId, modelId, extRef, pk1, pk2);
        }

        public virtual async Task<T> GetEntityByExtId(string tenantId, string modelId, ExternalReference extRef, string pk1 = null, string pk2 = null)
        {
            if (extRef != null && !string.IsNullOrEmpty(extRef.MapToId))
            {
                return (T)await base.FetchEntityAsync(tenantId, extRef.MapToId, modelId, pk1, pk2);
            }
            return default(T);
        }

        public override async Task<T> UpsertEntityAsync(string tenantId, string modelId, T entity, Type? entityType = default(Type))
        {
            //We may not get an id, even if the entity record exists
            // We allow consumers to send in Id (string?)
            // but we don't expect them to know or care about our Ids

            //First look for any external reference
            var extId = GetExtIdAsync(entity).Result;
            var extIdType = GetExtIdTypeAsync(entity).Result;
            if (!string.IsNullOrEmpty(extId) && !string.IsNullOrEmpty(extIdType))
            {
                var normalizedExtId = extId.ToLowerInvariant();
                var normalizedExtIdType = extIdType.ToLowerInvariant();

                // First, try to fetch by deterministic ID to get existing record with ETag
                var existingExtRef = await _extReferenceAdapter.FetchExtReferenceByDeterministicIdAsync(tenantId, normalizedExtIdType, normalizedExtId);
                if (existingExtRef != null)
                {
                    // External reference already exists - use it
                    //This is a known entity
                    if (string.IsNullOrEmpty(entity.Id))
                    {
                        entity.Id = existingExtRef.MapToId; //Set Id to retrieved External Reference record
                    }
                    else
                    {
                        var mapTo = existingExtRef.MapToId;
                        if (!mapTo.Equals(entity.Id, StringComparison.InvariantCultureIgnoreCase))
                        {
                            //This seems bad - mismatch between entity Id and external reference MapToId
                            _logger.LogWarning($"Mismatch between entity Id ({entity.Id}) and ExternalReference MapToId ({mapTo}) for tenant {tenantId}, extId {extId}, type {extIdType}");
                        }
                    }
                }
                else
                {
                    // External reference doesn't exist - create new one with deterministic ID
                    entity.Id = (!string.IsNullOrEmpty(entity.Id)) ? entity.Id : Guid.NewGuid().ToString();
                    var refStatus = "active"; //TODO: should be an enum
                    var refEntity = _extReferenceAdapter.CreateExternalReference(tenantId, normalizedExtIdType, refStatus, normalizedExtId, entity.Id);
                    
                    try
                    {
                        // Attempt to upsert - will use ETag for optimistic concurrency
                        _ = await _extReferenceAdapter.UpsertExtReferenceAsync(tenantId, refEntity);
                    }
                    catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
                    {
                        // ETag conflict - another thread created it, re-fetch to get the existing record
                        var conflictExtRef = await _extReferenceAdapter.FetchExtReferenceByDeterministicIdAsync(tenantId, normalizedExtIdType, normalizedExtId);
                        
                        if (conflictExtRef != null)
                        {
                            // Use the existing record that was created by the other thread
                            var mappedEntityId = conflictExtRef.MapToId;
                            if (string.IsNullOrEmpty(entity.Id) || entity.Id != mappedEntityId)
                            {
                                entity.Id = mappedEntityId;
                            }
                        }
                        else
                        {
                            // Unexpected: conflict but record not found - rethrow
                            throw;
                        }
                    }
                }
            }

            return await base.UpsertEntityAsync(tenantId, modelId, entity, entityType);
        }

        public virtual async Task<ExternalReference> AliasEntityAsync(string tenantId, ExternalReference extRef)
        {
            if (!string.IsNullOrEmpty(extRef.MapFromId) && !string.IsNullOrEmpty(extRef.MapToId))
            {
                var normalizedExtId = extRef.MapFromId.ToLowerInvariant();
                var normalizedExtIdType = extRef.Type.ToLowerInvariant();

                var storedXRef = _extReferenceAdapter.CreateExternalReference(tenantId, normalizedExtIdType, extRef.Status ?? "active", normalizedExtId, extRef.MapToId);
                
                try
                {
                    // Attempt to upsert - will use ETag for optimistic concurrency
                    return await _extReferenceAdapter.UpsertExtReferenceAsync(tenantId, storedXRef);
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
                {
                    // ETag conflict - another thread created it, re-fetch to get the existing record
                    var conflictExtRef = await _extReferenceAdapter.FetchExtReferenceByDeterministicIdAsync(tenantId, normalizedExtIdType, normalizedExtId);
                    if (conflictExtRef != null)
                    {
                        return conflictExtRef;
                    }
                    else
                    {
                        // Unexpected: conflict but record not found - rethrow
                        throw;
                    }
                }
            }

            return null;
        }
    }
}
