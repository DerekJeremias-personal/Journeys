using Backend.Dto.Structures.Taxonomy;
using Journeys.Core.Interfaces.DataStorage;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Journeys.Tests.Stubs
{
    /// <summary>
    /// Stub taxonomy adapter for unit tests. Register SKUs (or other xids) to be treated as included.
    /// For GetManyTaxonomiesByXidAsync, returns one TaxonomyDto per registered lookup key with the given node Id/Category.
    /// </summary>
    public class StubTaxonomyDataAdapter : ITaxonomyDataAdapter
    {
        /// <summary>Node Id to return for matching lookup keys (use in TaxonomicRule.IncludedIds).</summary>
        public string TaxonomyNodeId { get; set; } = "test-sku-node";

        /// <summary>Category string for returned DTOs (use in IncludedTreeNodes if needed).</summary>
        public string Category { get; set; } = "TestCategory";

        /// <summary>Lookup keys (e.g. SKUs) that should resolve to a taxonomy node.</summary>
        public HashSet<string> RegisteredKeys { get; set; } = new HashSet<string>();

        public void RegisterSku(string sku)
        {
            RegisteredKeys.Add(sku);
        }

        public Task<List<TaxonomyDto>> GetManyTaxonomiesByXidAsync(string tenantId, List<string> lookupKeys, CancellationToken token = default)
        {
            var result = new List<TaxonomyDto>();
            if (lookupKeys == null) return Task.FromResult(result);
            foreach (var key in lookupKeys)
            {
                if (string.IsNullOrEmpty(key)) continue;
                if (!RegisteredKeys.Contains(key)) continue;
                result.Add(new TaxonomyDto
                {
                    Id = TaxonomyNodeId,
                    Category = Category,
                    DataExternalIds = new List<string> { key }
                });
            }
            return Task.FromResult(result);
        }

        public Task<TaxonomyDto?> GetTaxonomyAsync(string tenantId, string taxonomyType, string category, string id, CancellationToken token = default, JsonSerializerOptions serializerOptions = null)
            => Task.FromResult<TaxonomyDto?>(null);

        public Task<TaxonomyDto?> GetTaxonomyByExtIdAsync(string tenantId, string taxonomyType, string extId, CancellationToken token = default, JsonSerializerOptions serializerOptions = null)
            => Task.FromResult<TaxonomyDto?>(null);

        public Task<List<TaxonomyDto>> GetManyTaxonomiesAsync(string tenantId, string taxonomyType, Backend.Dto.Requests.GetManyTaxonomiesRequest request, CancellationToken token = default, JsonSerializerOptions serializerOptions = null)
            => Task.FromResult(new List<TaxonomyDto>());

        public Task<TaxonomyDto> SaveRootTaxonomyAsync(string tenantId, TaxonomyDto taxonomyDto, CancellationToken token = default, JsonSerializerOptions serializerOptions = null)
            => throw new System.NotImplementedException();

        public Task<TaxonomyDto> SaveTaxonomyAsync(string tenantId, string taxonomyType, TaxonomyDto taxonomyDto, CancellationToken token = default, JsonSerializerOptions serializerOptions = null)
            => throw new System.NotImplementedException();

        public Task<TaxonomyBulkUpsertResultDto> BulkUpsertTaxonomiesAsync(string tenantId, List<TaxonomyDto> taxonomies, bool validateAll = true, bool continueOnValidationFailure = true, bool includeDetailedResults = true, int batchSize = 100, int maxDegreeOfParallelism = 4, CancellationToken token = default, JsonSerializerOptions serializerOptions = null)
            => throw new System.NotImplementedException();

        public JsonSerializerOptions GetJsonSerializerOptions()
            => new JsonSerializerOptions();
    }
}
