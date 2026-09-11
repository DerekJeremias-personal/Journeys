using Backend.Dto.Requests;
using Backend.Dto.Structures.Lookup;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Requests;
using Journeys.Infra.Backend.Models;
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
    /// Adapter for accessing LookupController endpoints.
    /// Provides methods for retrieving, creating, and deleting lookup records for alternative access patterns.
    /// </summary>
    public class BackendLookupAdapter : BaseAdapter, ILookupDataAdapter
    {
        private readonly ILogger<BackendLookupAdapter> _logger;

        // URL constants for Lookup endpoints
        private const string GET_LOOKUP_URL = "/api/v1/tenants/{0}/lookups/{1}/{2}";
        private const string GET_MANY_LOOKUPS_URL = "/api/v1/tenants/{0}/lookups/getMany";
        private const string CREATE_LOOKUP_URL = "/api/v1/tenants/{0}/lookups/{1}";
        private const string CREATE_TAXONOMY_LOOKUP_URL = "/api/v1/tenants/{0}/lookups/{1}/taxonomy";
        private const string CREATE_MODEL_LOOKUP_URL = "/api/v1/tenants/{0}/lookups/{1}/model";
        private const string DELETE_LOOKUP_URL = "/api/v1/tenants/{0}/lookups/{1}/{2}";
        private const string GET_LOOKUPS_BY_TARGET_URL = "/api/v1/tenants/{0}/lookups/byTarget/{1}/{2}";
        private const string DELETE_LOOKUPS_BY_TARGET_URL = "/api/v1/tenants/{0}/lookups/byTarget/{1}/{2}";

        public BackendLookupAdapter(IHttpClientFactory clientFactory, IOptions<KeyValueStorageConfig> keyValueStorageConfig, ILogger<BackendLookupAdapter> logger)
            : base(clientFactory, keyValueStorageConfig, logger, "lookup")
        {
            _logger = logger;
        }

        /// <summary>
        /// Finds a lookup record by its key and type using efficient point read.
        /// </summary>
        public async Task<LookupDto?> GetLookupAsync(
            string tenantId,
            string lookupType,
            string lookupKey,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);
                var route = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_LOOKUP_URL), tenantId, lookupType, lookupKey);
                var response = await client.GetAsync(route, token);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Lookup {LookupKey} of type {LookupType} not found for tenant {TenantId}", lookupKey, lookupType, tenantId);
                    return null;
                }

                return await HandleGetResponse<LookupDto>(response, serializerOptions, tenantId, lookupKey, lookupType);
            }
        }

        /// <summary>
        /// Retrieves multiple lookup records in a single batch operation.
        /// </summary>
        public async Task<List<LookupResult>> GetManyLookupsAsync(
            string tenantId,
            GetManyLookupsRequest request,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            request.TenantId = tenantId;

            string url = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_MANY_LOOKUPS_URL), tenantId);

            try
            {
                return await PostRequest<LookupResult>(tenantId, request.LookupType, url, request, typeof(LookupResult), serializerOptions ?? _serializerOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting many lookups for tenant {TenantId}, lookupType {LookupType}", tenantId, request.LookupType);
                throw;
            }
        }

        /// <summary>
        /// Creates a lookup record (create-only, no updates allowed).
        /// </summary>
        public async Task<LookupDto> CreateLookupAsync(
            string tenantId,
            string lookupType,
            CreateLookupRequest request,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                try
                {
                    var response = await client.PostAsJsonAsync(
                        string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, CREATE_LOOKUP_URL), tenantId, lookupType),
                        request,
                        serializerOptions ?? _serializerOptions);

                    if (response.StatusCode == HttpStatusCode.Created)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            return JsonSerializer.Deserialize<LookupDto>(json, serializerOptions ?? _serializerOptions);
                        }
                    }

                    await HandleErrorResponse(response, tenantId, lookupType);
                    return null; // This line should never be reached due to exception throwing above
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in CreateLookupAsync for tenant {TenantId}, lookupType {LookupType}", tenantId, lookupType);
                    throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
                }
            }
        }

        /// <summary>
        /// Creates a lookup record for a Taxonomy entity (convenience endpoint).
        /// </summary>
        public async Task<LookupDto> CreateTaxonomyLookupAsync(
            string tenantId,
            string lookupType,
            CreateTaxonomyLookupRequestDto request,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                try
                {
                    var response = await client.PostAsJsonAsync(
                        string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, CREATE_TAXONOMY_LOOKUP_URL), tenantId, lookupType),
                        request,
                        serializerOptions ?? _serializerOptions);

                    if (response.StatusCode == HttpStatusCode.Created)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            return JsonSerializer.Deserialize<LookupDto>(json, serializerOptions ?? _serializerOptions);
                        }
                    }

                    await HandleErrorResponse(response, tenantId, lookupType);
                    return null; // This line should never be reached due to exception throwing above
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in CreateTaxonomyLookupAsync for tenant {TenantId}, lookupType {LookupType}", tenantId, lookupType);
                    throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
                }
            }
        }

        /// <summary>
        /// Creates a lookup record for a Model entity (convenience endpoint).
        /// </summary>
        public async Task<LookupDto> CreateModelLookupAsync(
            string tenantId,
            string lookupType,
            CreateModelLookupRequestDto request,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                try
                {
                    var response = await client.PostAsJsonAsync(
                        string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, CREATE_MODEL_LOOKUP_URL), tenantId, lookupType),
                        request,
                        serializerOptions ?? _serializerOptions);

                    if (response.StatusCode == HttpStatusCode.Created)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            return JsonSerializer.Deserialize<LookupDto>(json, serializerOptions ?? _serializerOptions);
                        }
                    }

                    await HandleErrorResponse(response, tenantId, lookupType);
                    return null; // This line should never be reached due to exception throwing above
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in CreateModelLookupAsync for tenant {TenantId}, lookupType {LookupType}", tenantId, lookupType);
                    throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
                }
            }
        }

        /// <summary>
        /// Deletes a lookup record by its key and type.
        /// </summary>
        public async Task<bool> DeleteLookupAsync(
            string tenantId,
            string lookupType,
            string lookupKey,
            CancellationToken token = default)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                try
                {
                    var path = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, DELETE_LOOKUP_URL), tenantId, lookupType, lookupKey);
                    var response = await client.DeleteAsync(path, token);

                    if (response.StatusCode == HttpStatusCode.NoContent)
                    {
                        return true;
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Lookup {LookupKey} of type {LookupType} not found for deletion in tenant {TenantId}", lookupKey, lookupType, tenantId);
                        return false;
                    }
                    else
                    {
                        await HandleErrorResponse(response, tenantId, lookupType);
                        return false; // This line should never be reached due to exception throwing above
                    }
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in DeleteLookupAsync for tenant {TenantId}, lookupType {LookupType}, lookupKey {LookupKey}", tenantId, lookupType, lookupKey);
                    throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
                }
            }
        }

        /// <summary>
        /// Gets all lookup records for a specific target entity (for cleanup operations).
        /// </summary>
        public async Task<List<LookupDto>> GetLookupsByTargetAsync(
            string tenantId,
            string targetEntityType,
            string targetId,
            CancellationToken token = default,
            JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);
                var route = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_LOOKUPS_BY_TARGET_URL), tenantId, targetEntityType, targetId);
                var response = await client.GetAsync(route, token);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        return JsonSerializer.Deserialize<List<LookupDto>>(json, serializerOptions ?? _serializerOptions);
                    }
                }

                await HandleErrorResponse(response, tenantId, targetEntityType);
                return new List<LookupDto>(); // This line should never be reached due to exception throwing above
            }
        }

        /// <summary>
        /// Deletes all lookup records for a specific target entity (for cleanup operations).
        /// </summary>
        public async Task<int> DeleteLookupsByTargetAsync(
            string tenantId,
            string targetEntityType,
            string targetId,
            CancellationToken token = default)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                try
                {
                    var path = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, DELETE_LOOKUPS_BY_TARGET_URL), tenantId, targetEntityType, targetId);
                    var response = await client.DeleteAsync(path, token);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _serializerOptions);
                            if (result != null && result.ContainsKey("deletedCount"))
                            {
                                return Convert.ToInt32(result["deletedCount"]);
                            }
                        }
                        return 0;
                    }
                    else
                    {
                        await HandleErrorResponse(response, tenantId, targetEntityType);
                        return 0; // This line should never be reached due to exception throwing above
                    }
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in DeleteLookupsByTargetAsync for tenant {TenantId}, targetEntityType {TargetEntityType}, targetId {TargetId}", tenantId, targetEntityType, targetId);
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

