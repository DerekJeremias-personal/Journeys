using Backend.Dto.Requests;
using Backend.Dto.Structures.Model;
using Backend.Dto.Utilities;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Journeys.Infra.Backend
{
    public class ModelAdapter : BaseAdapter, IModelAdapter
    {
        private readonly ILogger<ModelAdapter> _logger;

        private const string MODEL_TYPE = "loyalty";

        public ModelAdapter(IHttpClientFactory clientFactory, IOptions<KeyValueStorageConfig> keyValueStorageConfig, ILogger<ModelAdapter> logger)
            : base(clientFactory, keyValueStorageConfig, logger, MODEL_TYPE)
        {
            _logger = logger;

            _serializerOptions = JsonUtility.GetDefaultOptions();
        }

        public async Task<PagedResultSet<ModelDto>> GetModels(string tenantId, string groupingType, int pageSize = 10, string? continuationToken = null, CancellationToken? token = null)
        {
            try
            {
                string url = Path.Join(_keyValueStorageConfig.BaseUrl, $"/api/{tenantId}/model/all");

                var request = BackendRequestJson.CreateCatalogListBody(tenantId, pageSize, continuationToken, groupingType);

                return await PostPagedRequest<ModelDto>(tenantId, null, url, request, null, _serializerOptions);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON deserialization error in GetModels for tenant {TenantId}. This may indicate a mismatch between the backend response format and the expected ModelDto structure.", tenantId);
                throw new BackendSystemException("model_deserialization_error", 
                    "Failed to deserialize model data from backend. The response format may have changed or contains invalid data.", 
                    jsonEx.Message);
            }
            catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
            {
                _logger.LogError(ex, "Unexpected error in GetModels for tenant {TenantId}", tenantId);
                throw new BackendSystemException("unexpected_error", "An unexpected error occurred while retrieving models", ex.Message);
            }
        }

        public async Task RemoveModelAsync(string tenantId, string modelId, string modelType, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId));
            var resolvedModelId = BackendModelId.Require(modelId);
            if (string.IsNullOrWhiteSpace(modelType))
                throw new ArgumentNullException(nameof(modelType));

            string url = Path.Join(_keyValueStorageConfig.BaseUrl, $"/api/{tenantId}/model/{modelType}/{resolvedModelId}");

            using var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

            try
            {
                using var response = await client.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.OK)
                    return;

                await HandleErrorResponse(response, tenantId, resolvedModelId);
            }
            catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException || ex is OperationCanceledException))
            {
                _logger.LogError(ex, "Unexpected error in RemoveModelAsync for tenant {TenantId}, model {ModelId}", tenantId, modelId);
                throw new BackendSystemException("unexpected_error", "An unexpected error occurred while deleting the model", ex.Message);
            }
        }

        public async Task<ModelDto?> GetModelAsync(string tenantId, string modelId, string modelType, bool includeChildModels = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId));
            var resolvedModelId = BackendModelId.Require(modelId);
            if (string.IsNullOrWhiteSpace(modelType))
                throw new ArgumentNullException(nameof(modelType));

            var qs = $"?modelType={Uri.EscapeDataString(modelType)}&includeChildModels={(includeChildModels ? "true" : "false")}";
            string url = Path.Join(_keyValueStorageConfig.BaseUrl, $"/api/{tenantId}/Model/get/{resolvedModelId}{qs}");

            using var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

            try
            {
                using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
                return await HandleGetResponse<ModelDto>(response, _serializerOptions, tenantId, resolvedModelId, resolvedModelId).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException || ex is OperationCanceledException))
            {
                _logger.LogError(ex, "Unexpected error in GetModelAsync for tenant {TenantId}, model {ModelId}", tenantId, modelId);
                throw new BackendSystemException("unexpected_error", "An unexpected error occurred while retrieving the model", ex.Message);
            }
        }

    }

}
