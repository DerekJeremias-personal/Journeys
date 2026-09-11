using System.Linq;
using System.Text.Json;

namespace Journeys.CampaignAgent.Remediation;

/// <summary>
/// Heuristic: MCP tools returning JSON with top-level <c>errors</c> or <c>error: true</c> are failures.
/// </summary>
public static class ToolResultSuccessEvaluator
{
    public static bool LooksSuccessful(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return true;

            if (root.TryGetProperty("errors", out var errors)
                && errors.ValueKind != JsonValueKind.Null
                && (errors.ValueKind != JsonValueKind.Object || errors.EnumerateObject().Any()))
                return false;

            if (root.TryGetProperty("validationErrors", out var validationErrors)
                && validationErrors.ValueKind == JsonValueKind.Object
                && validationErrors.EnumerateObject().Any())
                return false;

            if (root.TryGetProperty("ValidationErrors", out var validationErrorsPascal)
                && validationErrorsPascal.ValueKind == JsonValueKind.Object
                && validationErrorsPascal.EnumerateObject().Any())
                return false;

            if (root.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == JsonValueKind.True)
                    return false;
                if (err.ValueKind == JsonValueKind.Object)
                    return false;
            }

            if (root.TryGetProperty("isError", out var isError) && isError.ValueKind == JsonValueKind.True)
                return false;

            if (root.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.Array
                && ContentIndicatesInvocationError(content))
                return false;

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ContentIndicatesInvocationError(JsonElement content)
    {
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            if (!item.TryGetProperty("text", out var textEl) || textEl.ValueKind != JsonValueKind.String)
                continue;
            var text = textEl.GetString() ?? "";
            if (text.Contains("An error occurred invoking", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
