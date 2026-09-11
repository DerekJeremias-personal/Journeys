using System.Text.Json;
using CampaignContextAudit.Models;

namespace CampaignContextAudit.Analysis;

public static class ToolOutcomeClassifier
{
    private static readonly string[] ValidationCodePrefixes = { "JOURNEY_NAV_", "TIER_A_" };
    private const string JourneyValidationToken = "journey.validation";

    public static ToolOutcomeKind Classify(AgentMessageDoc? toolRow) =>
        Classify(toolRow?.ToolName, toolRow?.ToolResultJson);

    public static ToolOutcomeKind Classify(string? toolName, string? toolResultJson)
    {
        var raw = toolResultJson ?? "";
        var body = ExtractEffectiveBody(raw);

        if (IsMcpInvocationError(raw, body))
            return ToolOutcomeKind.McpInvocationError;

        if (HasValidationErrors(body))
            return ToolOutcomeKind.ValidationErrors;

        if (IsValidateAck(toolName, body))
            return ToolOutcomeKind.ValidateAck;

        if (IsMutationDigest(body))
            return ToolOutcomeKind.MutationDigest;

        return ToolOutcomeKind.Unknown;
    }

    private static bool IsMcpInvocationError(string raw, string body) =>
        raw.Contains("\"isError\":true", StringComparison.OrdinalIgnoreCase)
        || raw.Contains("\"isError\": true", StringComparison.OrdinalIgnoreCase)
        || body.Contains("An error occurred invoking", StringComparison.OrdinalIgnoreCase);

    private static bool HasValidationErrors(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return false;

        if (ValidationCodePrefixes.Any(p => body.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return true;
        if (body.Contains(JourneyValidationToken, StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.String)
                return false;
            if (root.ValueKind != JsonValueKind.Object)
                return ValidationCodePrefixes.Any(p => body.Contains(p, StringComparison.OrdinalIgnoreCase));

            if (TryGetNonEmptyErrorsArray(root, "errors")) return true;
            if (TryGetNonEmptyErrorsArray(root, "Errors")) return true;
            if (root.TryGetProperty("Validation", out var validation)
                && validation.ValueKind == JsonValueKind.Object
                && TryGetNonEmptyErrorsArray(validation, "Errors"))
                return true;
        }
        catch (JsonException)
        {
            if (body.Contains("\"errors\":[", StringComparison.OrdinalIgnoreCase)
                && !body.Contains("\"errors\":[]", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsValidateAck(string? toolName, string body)
    {
        if (!string.Equals(toolName, "validate_campaign", StringComparison.OrdinalIgnoreCase))
            return false;
        if (HasValidationErrors(body)) return false;
        if (body.Contains("An error occurred invoking", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!TryParseIsValidTrue(body))
            return false;
        return body.Length <= 4000;
    }

    private static bool TryParseIsValidTrue(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || !body.TrimStart().StartsWith('{'))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            return root.TryGetProperty("IsValid", out var isValid)
                   && isValid.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return body.Contains("\"IsValid\":true", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("\"IsValid\": true", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool IsMutationDigest(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return false;

        if (body.Contains("ruleSetCount", StringComparison.OrdinalIgnoreCase)
            || body.Contains("RuleSetCount", StringComparison.OrdinalIgnoreCase))
            return true;
        if (body.Contains("campaignId", StringComparison.OrdinalIgnoreCase)
            || body.Contains("CampaignId", StringComparison.OrdinalIgnoreCase))
            return true;

        return body.Contains("\"ack\"", StringComparison.OrdinalIgnoreCase)
            || body.Contains("\"mutation", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetNonEmptyErrorsArray(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return false;
        if (!root.TryGetProperty(propertyName, out var errors) || errors.ValueKind != JsonValueKind.Array)
            return false;
        return errors.GetArrayLength() > 0;
    }

    private static string ExtractEffectiveBody(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        if (!raw.TrimStart().StartsWith('{')) return raw;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                return text.GetString() ?? raw;

            if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in content.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var itemText) && itemText.ValueKind == JsonValueKind.String)
                        return itemText.GetString() ?? raw;
                }
            }

            return root.GetRawText();
        }
        catch (JsonException)
        {
            return raw;
        }
    }
}
