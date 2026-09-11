using Backend.Dto.Requests;
using Backend.Dto.Responses;
using Backend.Dto.Structures.Taxonomy;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using Journeys.DTO.Exceptions;
using Journeys.Infra.Backend.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ErrorResponse = Journeys.Infra.Backend.Models.ErrorResponse;
using FieldValidationErrorResponse = Journeys.Infra.Backend.Models.FieldValidationErrorResponse;

namespace Journeys.Infra.Backend
{
    /// <summary>
    /// Adapter for accessing TaxonomyController endpoints.
    /// Provides methods for retrieving, creating, updating, and bulk operations on taxonomy hierarchies.
    /// </summary>
    public class BackendTaxonomyAdapter : BaseAdapter, ITaxonomyDataAdapter
    {
        private readonly ILogger<BackendTaxonomyAdapter> _logger;

        // URL constants for Taxonomy endpoints
        private const string GET_TAXONOMY_URL = "/api/{0}/taxonomy/{1}/{2}/{3}";
        private const string GET_TAXONOMY_BY_EXT_ID_URL = "/api/{0}/taxonomy/{1}/{2}";
        private const string GET_MANY_TAXONOMIES_URL = "/api/{0}/taxonomy/getmany";
        private const string GET_MANY_TAXONOMIES_XID_URL = "/api/{0}/taxonomy/getmanyxids";
        private const string SAVE_ROOT_TAXONOMY_URL = "/api/{0}/taxonomy/root/save";
        private const string SAVE_TAXONOMY_URL = "/api/{0}/taxonomy/{1}/save";
        private const string BULK_UPSERT_TAXONOMIES_URL = "/api/{0}/taxonomy/bulk";

        public BackendTaxonomyAdapter(IHttpClientFactory clientFactory, IOptions<KeyValueStorageConfig> keyValueStorageConfig, ILogger<BackendTaxonomyAdapter> logger)
            : base(clientFactory, keyValueStorageConfig, logger, "taxonomy")
        {
            _logger = logger;

            _serializerOptions = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                Converters =
                {
                    new CustomDateTimeConverter(),
                    new CustomDateOnlyConverter(),
                    new CustomTimeSpanConverter(),
                    new JsonStringEnumConverter()
                }
            };
        }

        /// <summary>
        /// Retrieves a taxonomy node by its unique identifier, type, and category.
        /// </summary>
        public async Task<TaxonomyDto?> GetTaxonomyAsync(string tenantId, string taxonomyType, string category, string id, CancellationToken token = default, JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);
                var route = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_TAXONOMY_URL), tenantId, taxonomyType, category, id);
                var response = await client.GetAsync(route, token);

                return await HandleGetResponse<TaxonomyDto>(response, serializerOptions, tenantId, id, taxonomyType);
            }
        }

        /// <summary>
        /// Retrieves a taxonomy node by its external identifier and type.
        /// </summary>
        public async Task<TaxonomyDto?> GetTaxonomyByExtIdAsync(string tenantId, string taxonomyType, string extId, CancellationToken token = default, JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);
                var route = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_TAXONOMY_BY_EXT_ID_URL), tenantId, taxonomyType, extId);
                var response = await client.GetAsync(route, token);

                return await HandleGetResponse<TaxonomyDto>(response, serializerOptions, tenantId, extId, taxonomyType);
            }
        }

        /// <summary>
        /// Retrieves multiple taxonomy nodes by their unique identifiers in a single batch operation.
        /// </summary>
        public async Task<List<TaxonomyDto>> GetManyTaxonomiesAsync(string tenantId, string taxonomyType, GetManyTaxonomiesRequest request, CancellationToken token = default, JsonSerializerOptions serializerOptions = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            request.TenantId = tenantId;

            string url = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_MANY_TAXONOMIES_URL), tenantId, taxonomyType);
            
            try
            {
                return await PostRequest<TaxonomyDto>(tenantId, taxonomyType, url, request, typeof(TaxonomyDto), serializerOptions ?? _serializerOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting many taxonomies for tenant {TenantId}, taxonomyType {TaxonomyType}", tenantId, taxonomyType);
                throw;
            }
        }

        public async Task<List<TaxonomyDto>> GetManyTaxonomiesByXidAsync(string tenantId, List<string> lookupKeys, CancellationToken token = default)
        {
            if (lookupKeys?.Count == 0)
                throw new ArgumentNullException(nameof(lookupKeys));

            string url = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_MANY_TAXONOMIES_XID_URL), tenantId, lookupKeys);

            try
            {
                using (var client = _clientFactory.CreateClient())
                {
                    client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                    var request = new GetTaxonomiesByLookupsRequest
                    {
                        TenantId = tenantId,
                        LookupKeys = lookupKeys
                    };
                    var jsonstr = JsonSerializer.Serialize(request);
                    var response = await client.PostAsJsonAsync(url, request, _serializerOptions);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(json))
                        {
                            return JsonSerializer.Deserialize<List<TaxonomyDto>>(json, _serializerOptions);
                        }
                        return default(List<TaxonomyDto>);
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
                    {
                        _logger.LogDebug($"Taxonomies: {string.Join(", ", lookupKeys)} not found in tenant {tenantId}");
                        return default(List<TaxonomyDto>);
                    }
                    else
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        throw new BackendSystemException("unexpected_http_status",
                        $"BackendTaxonomyAdapter::GetManyTaxonomiesByXidAsync: Unexpected HTTP status code: {response.StatusCode}", responseContent);

                        return default(List<TaxonomyDto>); // This line should never be reached due to exception throwing above
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BackendTaxonomyAdapter::GetManyTaxonomiesByXidAsync:Error getting many taxonomies for tenant {TenantId}", tenantId);
                throw;
            }
        }

        /// <summary>
        /// Creates or updates a root taxonomy node (top-level node with no parent).
        /// </summary>
        public async Task<TaxonomyDto> SaveRootTaxonomyAsync(string tenantId, TaxonomyDto taxonomyDto, CancellationToken token = default, JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);
                client.Timeout = new TimeSpan(0, 10, 0);

                try
                {
                    var response = await client.PostAsJsonAsync(
                        string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, SAVE_ROOT_TAXONOMY_URL), tenantId),
                        taxonomyDto,
                        serializerOptions ?? _serializerOptions);

                    return await HandleSetEntityResponse<TaxonomyDto>(response, typeof(TaxonomyDto), serializerOptions, tenantId, taxonomyDto?.TaxonomyType);
                }
                catch (BackendEntityConcurrencyException ex)
                {
                    _logger.LogError(ex, "Concurrency error in SaveRootTaxonomyAsync for tenant {TenantId}, taxonomyType {TaxonomyType}", tenantId, taxonomyDto?.TaxonomyType);
                    throw;
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in SaveRootTaxonomyAsync for tenant {TenantId}, taxonomyType {TaxonomyType}", tenantId, taxonomyDto?.TaxonomyType);
                    throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
                }
            }
        }

        /// <summary>
        /// Creates or updates a taxonomy node within a specific taxonomy type.
        /// </summary>
        public async Task<TaxonomyDto> SaveTaxonomyAsync(string tenantId, string taxonomyType, TaxonomyDto taxonomyDto, CancellationToken token = default, JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);
                client.Timeout = new TimeSpan(0, 10, 0);

                try
                {
                    var response = await client.PostAsJsonAsync(
                        string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, SAVE_TAXONOMY_URL), tenantId, taxonomyType),
                        taxonomyDto,
                        serializerOptions ?? _serializerOptions);

                    return await HandleSetEntityResponse<TaxonomyDto>(response, typeof(TaxonomyDto), serializerOptions, tenantId, taxonomyType);
                }
                catch (BackendEntityConcurrencyException ex)
                {
                    _logger.LogError(ex, "Concurrency error in SaveTaxonomyAsync for tenant {TenantId}, taxonomyType {TaxonomyType}", tenantId, taxonomyType);
                    throw;
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in SaveTaxonomyAsync for tenant {TenantId}, taxonomyType {TaxonomyType}", tenantId, taxonomyType);
                    throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
                }
            }
        }

        /// <summary>
        /// Bulk upsert multiple taxonomies with comprehensive validation and error tracking.
        /// </summary>
        public async Task<TaxonomyBulkUpsertResultDto> BulkUpsertTaxonomiesAsync(
            string tenantId,
            List<TaxonomyDto> taxonomies,
            bool validateAll = true,
            bool continueOnValidationFailure = true,
            bool includeDetailedResults = true,
            int batchSize = 100,
            int maxDegreeOfParallelism = 4,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);
                client.Timeout = new TimeSpan(0, 10, 0);

                try
                {
                    var url = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, BULK_UPSERT_TAXONOMIES_URL), tenantId);
                    url += $"?validateAll={validateAll}&continueOnValidationFailure={continueOnValidationFailure}&includeDetailedResults={includeDetailedResults}&batchSize={batchSize}&maxDegreeOfParallelism={maxDegreeOfParallelism}";

                    var response = await client.PostAsJsonAsync(url, taxonomies, serializerOptions ?? _serializerOptions);

                    if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == (HttpStatusCode)207)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            return JsonSerializer.Deserialize<TaxonomyBulkUpsertResultDto>(json, serializerOptions ?? _serializerOptions);
                        }
                    }

                    await HandleErrorResponse(response, tenantId, null);
                    return null; // This line should never be reached due to exception throwing above
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in BulkUpsertTaxonomiesAsync for tenant {TenantId}", tenantId);
                    throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
                }
            }
        }

        public JsonSerializerOptions GetJsonSerializerOptions()
        {
            return this._serializerOptions;
        }
    }
}

