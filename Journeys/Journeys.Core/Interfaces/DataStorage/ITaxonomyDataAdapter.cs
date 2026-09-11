using Backend.Dto.Requests;
using Backend.Dto.Structures.Taxonomy;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    /// <summary>
    /// Interface for taxonomy data adapter that provides HTTP client access to TaxonomyController endpoints.
    /// Provides methods for retrieving, creating, updating, and bulk operations on taxonomy hierarchies.
    /// </summary>
    public interface ITaxonomyDataAdapter
    {
        /// <summary>
        /// Retrieves a taxonomy node by its unique identifier, type, and category.
        /// </summary>
        /// <param name="tenantId">The tenant identifier for multi-tenant isolation.</param>
        /// <param name="taxonomyType">The type of taxonomy (e.g., "ProductCategory", "Location").</param>
        /// <param name="category">The category within the taxonomy type.</param>
        /// <param name="id">The unique identifier of the taxonomy node to retrieve.</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <param name="serializerOptions">Optional JSON serializer options.</param>
        /// <returns>The taxonomy node with its hierarchy information, or null if not found.</returns>
        Task<TaxonomyDto?> GetTaxonomyAsync(
            string tenantId,
            string taxonomyType,
            string category,
            string id,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>
        /// Retrieves a taxonomy node by its external identifier and type.
        /// </summary>
        /// <param name="tenantId">The tenant identifier for multi-tenant isolation.</param>
        /// <param name="taxonomyType">The type of taxonomy (e.g., "ProductCategory", "Location").</param>
        /// <param name="extId">The external identifier of the taxonomy node to retrieve.</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <param name="serializerOptions">Optional JSON serializer options.</param>
        /// <returns>The taxonomy node with its hierarchy information, or null if not found.</returns>
        Task<TaxonomyDto?> GetTaxonomyByExtIdAsync(
            string tenantId,
            string taxonomyType,
            string extId,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>
        /// Retrieves multiple taxonomy nodes by their unique identifiers in a single batch operation.
        /// </summary>
        /// <param name="tenantId">The tenant identifier for multi-tenant isolation.</param>
        /// <param name="taxonomyType">The type of taxonomy (e.g., "ProductCategory", "Location").</param>
        /// <param name="request">The batch request containing tenant ID, taxonomy type, and list of taxonomy IDs to retrieve.</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <param name="serializerOptions">Optional JSON serializer options.</param>
        /// <returns>A list of taxonomy nodes. Taxonomies not found are excluded from the results.</returns>
        Task<List<TaxonomyDto>> GetManyTaxonomiesAsync(
            string tenantId,
            string taxonomyType,
            GetManyTaxonomiesRequest request,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null);

        Task<List<TaxonomyDto>> GetManyTaxonomiesByXidAsync(
            string tenantId,
            List<string> lookupKeys,
            CancellationToken token = default);

        /// <summary>
        /// Creates or updates a root taxonomy node (top-level node with no parent).
        /// </summary>
        /// <param name="tenantId">The tenant identifier for multi-tenant isolation.</param>
        /// <param name="taxonomyDto">The root taxonomy node to create or update. Must not have a parent ID.</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <param name="serializerOptions">Optional JSON serializer options.</param>
        /// <returns>The saved root taxonomy node with any server-side modifications.</returns>
        Task<TaxonomyDto> SaveRootTaxonomyAsync(
            string tenantId,
            TaxonomyDto taxonomyDto,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>
        /// Creates or updates a taxonomy node within a specific taxonomy type.
        /// </summary>
        /// <param name="tenantId">The tenant identifier for multi-tenant isolation.</param>
        /// <param name="taxonomyType">The type of taxonomy (e.g., "ProductCategory", "Location").</param>
        /// <param name="taxonomyDto">The taxonomy node to create or update. May include parent ID for hierarchy relationships.</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <param name="serializerOptions">Optional JSON serializer options.</param>
        /// <returns>The saved taxonomy node with any server-side modifications and validation results.</returns>
        Task<TaxonomyDto> SaveTaxonomyAsync(
            string tenantId,
            string taxonomyType,
            TaxonomyDto taxonomyDto,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>
        /// Bulk upsert multiple taxonomies with comprehensive validation and error tracking.
        /// </summary>
        /// <param name="tenantId">The tenant identifier for multi-tenant isolation.</param>
        /// <param name="taxonomies">List of taxonomies to upsert.</param>
        /// <param name="validateAll">Whether to validate all items before processing (default: true).</param>
        /// <param name="continueOnValidationFailure">Whether to continue processing valid items when some fail validation (default: true).</param>
        /// <param name="includeDetailedResults">Whether to include detailed per-item results (default: true).</param>
        /// <param name="batchSize">Batch size for processing (default: 100).</param>
        /// <param name="maxDegreeOfParallelism">Maximum parallel batches (default: 4).</param>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <param name="serializerOptions">Optional JSON serializer options.</param>
        /// <returns>Comprehensive bulk upsert result with statistics and per-item details.</returns>
        Task<TaxonomyBulkUpsertResultDto> BulkUpsertTaxonomiesAsync(
            string tenantId,
            List<TaxonomyDto> taxonomies,
            bool validateAll = true,
            bool continueOnValidationFailure = true,
            bool includeDetailedResults = true,
            int batchSize = 100,
            int maxDegreeOfParallelism = 4,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>
        /// Gets the JSON serializer options used by this adapter.
        /// </summary>
        /// <returns>The JSON serializer options.</returns>
        JsonSerializerOptions GetJsonSerializerOptions();
    }
}

