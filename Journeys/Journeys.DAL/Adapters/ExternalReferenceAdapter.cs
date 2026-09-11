using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public class ExternalReferenceAdapter : BaseAdapter<ExternalReference>, IExternalReferenceAdapter
    {
        private const string EXT_REF_MODEL_ID = "9b1ac286-2647-4245-828c-4d5826711a27";

        public ExternalReferenceAdapter(IDynamicDataAdapter dynAdapter) : base(dynAdapter) { }

        public async Task<PagedResultSet<ExternalReference>> FetchExtReferenceFromIdAsync(string tenantId, string extRefId, string type, int pageSize, string? continuationToken = null)
        {
            return await base.FetchEntityByKeyAsync(tenantId, type.ToLower(), EXT_REF_MODEL_ID, pageSize, extRefId.ToLower(), continuationToken);
        }

        /// <summary>
        /// Fetches an ExternalReference by its deterministic ID (based on partition key values).
        /// </summary>
        public async Task<ExternalReference?> FetchExtReferenceByDeterministicIdAsync(string tenantId, string type, string mapFromId)
        {
            var deterministicId = GenerateDeterministicId(tenantId, type, mapFromId);
            // Use type as partition key (pk) and mapFromId as pk2
            return await base.FetchEntityAsync(tenantId, deterministicId, EXT_REF_MODEL_ID, type.ToLower(), mapFromId.ToLower());
        }

        public async Task<ExternalReference> UpsertExtReferenceAsync(string tenantId, ExternalReference extRef)
        {
            if (string.IsNullOrEmpty(extRef.ModelId)) extRef.ModelId = EXT_REF_MODEL_ID;
            
            extRef.Type = extRef.Type.ToLower();
            extRef.MapFromId = extRef.MapFromId.ToLower();
            
            return await base.UpsertEntityAsync(tenantId, EXT_REF_MODEL_ID, extRef);
        }

        public async Task DeleteExtReferenceAsync(string tenantId, string extRefId, string type)
        {
            await base.DeleteEntityAsync(tenantId, EXT_REF_MODEL_ID, extRefId.ToLower(), new Dictionary<string, string> { { "type", type.ToLower() } });
        }

        public async Task DeleteExtReferenceAsync(string tenantId, ExternalReference extRef)
        {
            await base.DeleteEntityAsync(tenantId, EXT_REF_MODEL_ID, extRef);
        }

        /// <summary>
        /// Generates a deterministic ID for ExternalReference based on partition key values.
        /// This ensures only one ExternalReference can exist per (tenantId, type, mapFromId) combination.
        /// </summary>
        public static string GenerateDeterministicId(string tenantId, string type, string mapFromId)
        {
            // Normalize inputs (lowercase, trimmed) to ensure consistency
            var normalizedTenantId = tenantId?.ToLowerInvariant().Trim() ?? string.Empty;
            var normalizedType = type?.ToLowerInvariant().Trim() ?? string.Empty;
            var normalizedMapFromId = mapFromId?.ToLowerInvariant().Trim() ?? string.Empty;

            // Create a composite key from partition key values
            var compositeKey = $"{normalizedTenantId}|{normalizedType}|{normalizedMapFromId}";

            // Generate SHA256 hash and convert to GUID-like string format
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(compositeKey));
                // Convert to GUID format (32 hex characters with hyphens)
                var guidString = new Guid(hashBytes.Take(16).ToArray()).ToString();
                return guidString;
            }
        }

        public ExternalReference CreateExternalReference(string tenantId, string type, string status, string mapFromId, string mapToId)
        {
            // Use deterministic ID based on partition key to prevent duplicates
            var deterministicId = GenerateDeterministicId(tenantId, type, mapFromId);
            
            var extReference = new ExternalReference(
                            type,
                            status,
                            mapFromId,
                            mapToId,
                            tenantId,
                            deterministicId
                        );
            extReference.CreateDate = DateTime.UtcNow;
            extReference.LastUpdated = DateTime.UtcNow;
            extReference.ModelId = EXT_REF_MODEL_ID;
            return extReference;
        }


    }
}
