using Backend.Dto.Requests;
using Backend.Dto.Structures.Tenant;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.DTO.Responses;
using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Journeys.Infra.Backend
{
    /// <summary>
    /// Adapter for <c>TenantController</c> endpoints under <c>api/Tenant</c>.
    /// </summary>
    public class TenantAdapter : BaseAdapter, ITenantDataAdapter
    {
        private const string TenantRegistryContext = "tenant-registry";

        private const string GetTenantUrl = "/api/Tenant/get/{0}";
        private const string GetTenantByNameUrl = "/api/Tenant/getbyname/{0}";
        private const string GetManyTenantsUrl = "/api/Tenant/getmany";
        private const string GetAllTenantsUrl = "/api/Tenant/all";
        private const string SaveTenantUrl = "/api/Tenant/save";
        private const string SoftDeleteTenantUrl = "/api/Tenant/delete/{0}";

        private readonly ILogger<TenantAdapter> _logger;

        public TenantAdapter(
            IHttpClientFactory clientFactory,
            IOptions<KeyValueStorageConfig> keyValueStorageConfig,
            ILogger<TenantAdapter> logger)
            : base(clientFactory, keyValueStorageConfig, logger, "tenant")
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

        /// <inheritdoc />
        public async Task<TenantDto?> GetTenantAsync(
            string id,
            bool includeDeleted = false,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions serializerOptions = null)
        {
            using var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);
            var route = Path.Join(_keyValueStorageConfig.BaseUrl, string.Format(GetTenantUrl, Uri.EscapeDataString(id)));
            route = AppendIncludeDeleted(route, includeDeleted);
            var response = await client.GetAsync(route, cancellationToken);
            return await HandleGetResponse<TenantDto>(response, serializerOptions, TenantRegistryContext, id, "tenant");
        }

        /// <inheritdoc />
        public async Task<TenantDto?> GetTenantByNameAsync(
            string name,
            bool includeDeleted = false,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions serializerOptions = null)
        {
            using var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);
            var route = Path.Join(_keyValueStorageConfig.BaseUrl, string.Format(GetTenantByNameUrl, Uri.EscapeDataString(name)));
            route = AppendIncludeDeleted(route, includeDeleted);
            var response = await client.GetAsync(route, cancellationToken);
            return await HandleGetResponse<TenantDto>(response, serializerOptions, TenantRegistryContext, name, "tenant");
        }

        /// <inheritdoc />
        public async Task<ContinuableList<TenantDto>> GetManyTenantsAsync(
            GetManyTenantsRequest request,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions serializerOptions = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var url = Path.Join(_keyValueStorageConfig.BaseUrl, GetManyTenantsUrl);
            return await PostContinuableListAsync(url, request, cancellationToken, serializerOptions);
        }

        /// <inheritdoc />
        public async Task<ContinuableList<TenantDto>> GetAllTenantsAsync(
            GetAllTenantsRequest request,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions serializerOptions = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var url = Path.Join(_keyValueStorageConfig.BaseUrl, GetAllTenantsUrl);
            return await PostContinuableListAsync(url, request, cancellationToken, serializerOptions);
        }

        /// <inheritdoc />
        public async Task<TenantDto> SaveTenantAsync(
            TenantDto tenantDto,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions serializerOptions = null)
        {
            using var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);
            client.Timeout = TimeSpan.FromMinutes(10);

            try
            {
                var response = await client.PostAsJsonAsync(
                    Path.Join(_keyValueStorageConfig.BaseUrl, SaveTenantUrl),
                    tenantDto,
                    serializerOptions ?? _serializerOptions,
                    cancellationToken);

                return await HandleSetEntityResponse<TenantDto>(
                    response,
                    typeof(TenantDto),
                    serializerOptions,
                    TenantRegistryContext,
                    tenantDto?.Id ?? tenantDto?.Name ?? "tenant");
            }
            catch (BackendEntityConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error in SaveTenantAsync for tenant id {TenantId}", tenantDto?.Id);
                throw;
            }
            catch (Exception ex) when (ex is not BackendValidationException && ex is not BackendSystemException && ex is not BackendEntityNotFoundException)
            {
                _logger.LogError(ex, "Unexpected error in SaveTenantAsync for tenant id {TenantId}", tenantDto?.Id);
                throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
            }
        }

        /// <inheritdoc />
        public async Task<TenantDto?> SoftDeleteTenantAsync(
            string id,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions serializerOptions = null)
        {
            using var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

            var url = Path.Join(_keyValueStorageConfig.BaseUrl, string.Format(SoftDeleteTenantUrl, Uri.EscapeDataString(id)));
            using var response = await client.PostAsync(url, null, cancellationToken);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(json))
                    return null;
                return JsonSerializer.Deserialize<TenantDto>(json, serializerOptions ?? _serializerOptions);
            }

            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
            {
                _logger.LogDebug("Soft delete: tenant {TenantId} not found (tenant registry)", id);
                return null;
            }

            await HandleErrorResponse(response, TenantRegistryContext, "tenant");
            return null;
        }

        private async Task<ContinuableList<TenantDto>> PostContinuableListAsync(
            string url,
            object body,
            CancellationToken cancellationToken,
            JsonSerializerOptions serializerOptions)
        {
            using var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

            try
            {
                var response = await client.PostAsJsonAsync(url, body, serializerOptions ?? _serializerOptions, cancellationToken);

                if (response.StatusCode != HttpStatusCode.OK)
                    await HandleErrorResponse(response, TenantRegistryContext, "tenant");

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(json))
                    return new ContinuableList<TenantDto>();

                var list = JsonSerializer.Deserialize<ContinuableList<TenantDto>>(json, serializerOptions ?? _serializerOptions);
                return list ?? new ContinuableList<TenantDto>();
            }
            catch (Exception ex) when (ex is not BackendValidationException && ex is not BackendSystemException && ex is not BackendEntityNotFoundException)
            {
                _logger.LogError(ex, "Unexpected error posting to tenant registry: {Url}", url);
                throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
            }
        }

        private static string AppendIncludeDeleted(string route, bool includeDeleted)
        {
            var sep = route.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{route}{sep}includeDeleted={(includeDeleted ? "true" : "false")}";
        }
    }
}
