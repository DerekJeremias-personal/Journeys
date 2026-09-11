using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Azure;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public class DynamicExternalReferenceAdapter : DynamicBaseAdapter, IDynamicExternalReferenceAdapter
    {
        private readonly PathValueProvider IdProvider = new PathValueProvider("id");
        private const string EXT_REF_MODEL_ID = "9b1ac286-2647-4245-828c-4d5826711a27";
        private readonly IExternalReferenceAdapter _extReferenceAdapter;

        public DynamicExternalReferenceAdapter(IDynamicDataAdapter dynamicDataAdapter, IExternalReferenceAdapter extRefAdapter) : base(dynamicDataAdapter) 
        { 
            _extReferenceAdapter = extRefAdapter;
        }

        public async Task<ExternalReference?> FetchExtReferenceByKeyAsync(string tenantId, string type, string xid)
        {
            var extRef = await base.FetchEntityByKeyAsync(tenantId, type.ToLower(), EXT_REF_MODEL_ID, 100, xid.ToLower());
            var json = extRef?.Entities?.FirstOrDefault().ToString();
            if (!string.IsNullOrEmpty(json))
                return JsonSerializer.Deserialize<ExternalReference>(json, base._serializerOptions);
            else
                return null;
        }


        public async Task<ExternalReference?> FetchExtReferenceFromDto(string tenantId, string xId, ExternalReferenceDto dto)
        {
            var extRef = await _extReferenceAdapter.FetchExtReferenceFromIdAsync(tenantId, xId.ToLower(), dto.Type.ToLower(), 100);
            return extRef?.Entities?.FirstOrDefault();
        }

        public async Task<JsonElement?> GetEntityByExtId(string tenantId, string modelId, string extId, string extIdType, string pk = null, string pk2 = null)
        {
            if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(extId) && !string.IsNullOrEmpty(extIdType))
            {
                var extRef = await _extReferenceAdapter.FetchExtReferenceFromIdAsync(tenantId, extId.ToLower(), extIdType.ToLower(), 100);
                if (!string.IsNullOrEmpty(extRef?.ContinuationToken)) throw new Exception($"Maximum number of External References has been exceeded for loyalty account: {extId}");

                if (extRef != null && !string.IsNullOrEmpty(extRef.Entities?.FirstOrDefault()?.MapToId))
                {
                    return await base.FetchEntityAsync(tenantId, extRef.Entities.First().MapToId, modelId, pk, pk2);
                }
            }

            return null;
        }

        public async Task<JsonElement> UpsertEntityAsync(string tenantId, string modelId, WrappedEventPayload entity, string xId, string xIdType, string? status = null)
        {
            if (!string.IsNullOrEmpty(xId) && !string.IsNullOrEmpty(xIdType))
            {
                var normalizedXId = xId.ToLowerInvariant();
                var normalizedXIdType = xIdType.ToLowerInvariant();
                var entityId = entity.Id;

                // First, try to fetch by deterministic ID to get existing record with ETag
                var existingExtRef = await _extReferenceAdapter.FetchExtReferenceByDeterministicIdAsync(tenantId, normalizedXIdType, normalizedXId);
                if (existingExtRef != null)
                {
                    // External reference already exists - use it
                    var mappedEntityId = existingExtRef.MapToId;
                    if (string.IsNullOrEmpty(entityId))
                    {
                        entity.Id = entityId = mappedEntityId;
                    }
                    else if (entityId != mappedEntityId)
                    {
                        throw new ArgumentException($"Mismatch between passed entity and existing stored associated entity with the externalId, expected {mappedEntityId} but received {entityId} in the request");
                    }
                }
                else
                {
                    // External reference doesn't exist - create new one with deterministic ID
                    entityId = (!string.IsNullOrEmpty(entityId)) ? entityId : Guid.NewGuid().ToString();
                    entity.Id = entityId;
                    var refEntity = _extReferenceAdapter.CreateExternalReference(tenantId, normalizedXIdType, status, normalizedXId, entityId);
                    
                    try
                    {
                        // Attempt to upsert - will use ETag for optimistic concurrency
                        _ = await _extReferenceAdapter.UpsertExtReferenceAsync(tenantId, refEntity);
                    }
                    catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
                    {
                        // ETag conflict - another thread created it, re-fetch to get the existing record
                        var conflictExtRef = await _extReferenceAdapter.FetchExtReferenceByDeterministicIdAsync(tenantId, normalizedXIdType, normalizedXId);
                        if (conflictExtRef != null)
                        {
                            // Use the existing record that was created by the other thread
                            var mappedEntityId = conflictExtRef.MapToId;
                            if (string.IsNullOrEmpty(entityId) || entityId != mappedEntityId)
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


            //ToDo: *** Performance issue, we shouldn't need to serialize this here, just pass as the entity is
            entity.OmitEmptyCollections();
            var json = JsonSerializer.SerializeToElement(entity, JsonUtility.GetWrapperPersistOptions());
            return await base.UpsertEntityAsync(tenantId, modelId, json, typeof(JsonElement));
        }

        public async Task DeleteExternalReferenceAsync(string tenantId, string id, string type, string xid)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(xid))
                throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"DynamicExternalReferenceAdapter.DeleteExternalReferenceAsync: tenant, id, type and xid are all required." } });

            await base.DeleteEntityAsync(tenantId, EXT_REF_MODEL_ID, id, new Dictionary<string, string>
            {
                { "TenantId", tenantId },
                { "type", type.ToLower() },
                { "mapfromid", xid.ToLower() }
            });
        }

        public ExternalReference CreateExternalReference(string tenantId, string type, string status, string mapFromId, string mapToId)
        {
            // Use deterministic ID to prevent duplicates - delegate to ExternalReferenceAdapter
            return _extReferenceAdapter.CreateExternalReference(tenantId, type, status, mapFromId, mapToId);
        }
    }
}
