using Backend.Dto.Requests;
using Backend.Dto.Structures.Lookup;
using Journeys.DTO.Requests;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    /// <summary>
    /// Interface for lookup data adapter that provides HTTP client access to LookupController endpoints.
    /// Provides methods for retrieving, creating, and deleting lookup records for alternative access patterns.
    /// </summary>
    public interface ILookupDataAdapter
    {
        /// <summary>
        /// Finds a lookup record by its key and type using efficient point read.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="lookupType">Type of lookup (e.g., "ExternalId", "AlternateKey").</param>
        /// <param name="lookupKey">The lookup key to search for.</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <param name="serializerOptions">Optional JSON serializer options.</param>
        /// <returns>The lookup record if found, or null if not found.</returns>
        Task<LookupDto?> GetLookupAsync(
            string tenantId,
            string lookupType,
            string lookupKey,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>
        /// Retrieves multiple lookup records in a single batch operation.
        /// All lookups must be of the same type for partitioning efficiency.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="request">Batch lookup request.</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <param name="serializerOptions">Optional JSON serializer options.</param>
        /// <returns>List of lookup results.</returns>
        Task<List<LookupResult>> GetManyLookupsAsync(
            string tenantId,
            GetManyLookupsRequest request,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>
        /// Creates a lookup record (create-only, no updates allowed).
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="lookupType">Type of lookup.</param>
        /// <param name="request">Create lookup request.</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <param name="serializerOptions">Optional JSON serializer options.</param>
        /// <returns>The created lookup record.</returns>
        Task<LookupDto> CreateLookupAsync(
            string tenantId,
            string lookupType,
            CreateLookupRequest request,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>
        /// Creates a lookup record for a Taxonomy entity (convenience endpoint).
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="lookupType">Type of lookup.</param>
        /// <param name="request">Taxonomy lookup request.</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <param name="serializerOptions">Optional JSON serializer options.</param>
        /// <returns>The created lookup record.</returns>
        Task<LookupDto> CreateTaxonomyLookupAsync(
            string tenantId,
            string lookupType,
            CreateTaxonomyLookupRequestDto request,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>
        /// Creates a lookup record for a Model entity (convenience endpoint).
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="lookupType">Type of lookup.</param>
        /// <param name="request">Model lookup request.</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <param name="serializerOptions">Optional JSON serializer options.</param>
        /// <returns>The created lookup record.</returns>
        Task<LookupDto> CreateModelLookupAsync(
            string tenantId,
            string lookupType,
            CreateModelLookupRequestDto request,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>
        /// Deletes a lookup record by its key and type.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="lookupType">Type of lookup.</param>
        /// <param name="lookupKey">The lookup key.</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <returns>True if the lookup was successfully deleted, false if not found.</returns>
        Task<bool> DeleteLookupAsync(
            string tenantId,
            string lookupType,
            string lookupKey,
            CancellationToken token = default);

        /// <summary>
        /// Gets all lookup records for a specific target entity (for cleanup operations).
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="targetEntityType">Type of target entity.</param>
        /// <param name="targetId">ID of target entity.</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <param name="serializerOptions">Optional JSON serializer options.</param>
        /// <returns>List of lookup records for the target entity.</returns>
        Task<List<LookupDto>> GetLookupsByTargetAsync(
            string tenantId,
            string targetEntityType,
            string targetId,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>
        /// Deletes all lookup records for a specific target entity (for cleanup operations).
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="targetEntityType">Type of target entity.</param>
        /// <param name="targetId">ID of target entity.</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <returns>Number of deleted records.</returns>
        Task<int> DeleteLookupsByTargetAsync(
            string tenantId,
            string targetEntityType,
            string targetId,
            CancellationToken token = default);

        /// <summary>
        /// Gets the JSON serializer options used by this adapter.
        /// </summary>
        /// <returns>The JSON serializer options.</returns>
        JsonSerializerOptions GetJsonSerializerOptions();
    }

}

