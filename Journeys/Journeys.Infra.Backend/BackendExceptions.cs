using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Infra.Backend
{
    /// <summary>
    /// Exception thrown when the backend returns validation errors (HTTP 400)
    /// </summary>
    public class BackendValidationException : Exception
    {
        public Dictionary<string, string> ValidationErrors { get; }
        public string ErrorCode { get; }

        public BackendValidationException(Dictionary<string, string> validationErrors, string message = null, string errorCode = null)
            : base(message ?? "Validation errors occurred")
        {
            ValidationErrors = validationErrors ?? new Dictionary<string, string>();
            ErrorCode = errorCode ?? "validation_error";
        }
    }

    /// <summary>
    /// Exception thrown when the backend returns system errors (HTTP 500, etc.)
    /// </summary>
    public class BackendSystemException : Exception
    {
        public string ErrorCode { get; }
        public string RawResponse { get; }

        public BackendSystemException(string errorCode, string message, string rawResponse = null)
            : base(message)
        {
            ErrorCode = errorCode;
            RawResponse = rawResponse;
        }
    }

    /// <summary>
    /// Exception thrown when an entity is not found (HTTP 404)
    /// </summary>
    public class BackendEntityNotFoundException : Exception
    {
        public string EntityId { get; }
        public string ModelId { get; }
        public string TenantId { get; }

        public BackendEntityNotFoundException(string entityId, string modelId, string tenantId)
            : base($"Entity '{entityId}' not found for model '{modelId}' in tenant '{tenantId}'")
        {
            EntityId = entityId;
            ModelId = modelId;
            TenantId = tenantId;
        }
    }

    /// <summary>
    /// Exception thrown when an entity is not found (HTTP 404)
    /// </summary>
    public class BackendEntityConcurrencyException : Exception
    {
        public string EntityId { get; }
        public string ModelId { get; }
        public string TenantId { get; }

        public BackendEntityConcurrencyException(string entityId, string modelId, string tenantId)
            : base($"Concurrency error on Entity '{entityId}', for model '{modelId}' in tenant '{tenantId}'")
        {
            EntityId = entityId;
            ModelId = modelId;
            TenantId = tenantId;
        }
    }
}
