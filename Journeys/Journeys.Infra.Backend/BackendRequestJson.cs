using System.Text.Json;
using System.Text.Json.Serialization;

namespace Journeys.Infra.Backend;

public static class BackendRequestJson
{
    public static object CreateCatalogListBody(string tenantId, int pageSize, string? continuationToken, string? modelType = null)
    {
        var resolvedType = string.IsNullOrWhiteSpace(modelType)
            || modelType.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            ? BackendModelId.LoyaltyModelType
            : modelType.Trim();
        return new
        {
            TenantId = tenantId,
            ModelType = resolvedType,
            PageSize = pageSize,
            ContinuationToken = continuationToken
        };
    }

    public static string Serialize(object req, JsonSerializerOptions? options = null)
    {
        RejectUnknownModelId(req);
        var opts = options == null
            ? new JsonSerializerOptions()
            : new JsonSerializerOptions(options);
        opts.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        var json = JsonSerializer.Serialize(req, opts);
        if (JsonContainsUnknownModelId(json))
        {
            throw new ArgumentException("Backend request must not include modelId 'unknown'.");
        }

        return json;
    }

    private static void RejectUnknownModelId(object req)
    {
        var modelId = req.GetType().GetProperty("ModelId")?.GetValue(req) as string;
        if (modelId != null && !BackendModelId.IsUsable(modelId))
        {
            throw new ArgumentException("Backend request must not include modelId 'unknown'.", nameof(req));
        }
    }

    private static bool JsonContainsUnknownModelId(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in new[] { "ModelId", "modelId" })
        {
            if (doc.RootElement.TryGetProperty(name, out var el)
                && el.ValueKind == JsonValueKind.String
                && el.GetString() is string value
                && value.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
