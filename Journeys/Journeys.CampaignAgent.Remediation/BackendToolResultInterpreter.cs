using System.Text.Json;

namespace Journeys.CampaignAgent.Remediation;

public static class BackendToolResultInterpreter
{
    public static bool TryParse(string toolName, string? resultJson, out BackendToolFailure? failure)
    {
        failure = new BackendToolFailure
        {
            ToolName = toolName,
            RawJson = resultJson,
            ParseStatus = BackendToolFailureParseStatus.NotAttempted
        };

        if (string.IsNullOrWhiteSpace(resultJson))
        {
            failure = Copy(failure, BackendToolFailureParseStatus.NotJson);
            return false;
        }

        if (ToolResultSuccessEvaluator.LooksSuccessful(resultJson))
        {
            if (TryParseValidateCampaignFailure(toolName, resultJson, out var validateFailure))
            {
                failure = validateFailure;
                return true;
            }

            failure = Copy(failure, BackendToolFailureParseStatus.NotFailure);
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                failure = Copy(failure, BackendToolFailureParseStatus.NotJson);
                return false;
            }

            var errors = ExtractValidationMap(doc.RootElement);
            var message = TryGetString(doc.RootElement, "message");
            var code = TryGetString(doc.RootElement, "code");

            failure = new BackendToolFailure
            {
                ToolName = toolName,
                RawJson = resultJson,
                ParseStatus = BackendToolFailureParseStatus.Success,
                ValidationErrors = errors,
                Message = message,
                Code = code
            };

            return errors.Count > 0 || !string.IsNullOrEmpty(message);
        }
        catch (JsonException)
        {
            failure = Copy(failure, BackendToolFailureParseStatus.NotJson);
            return false;
        }
    }

    private static BackendToolFailure Copy(BackendToolFailure source, BackendToolFailureParseStatus status) =>
        new()
        {
            ToolName = source.ToolName,
            RawJson = source.RawJson,
            ParseStatus = status,
            ValidationErrors = source.ValidationErrors,
            Message = source.Message,
            Code = source.Code
        };

    private static Dictionary<string, string> ExtractValidationMap(JsonElement root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        TryGetStringMap(root, "errors", map);
        TryGetStringMap(root, "validationErrors", map);
        TryGetStringMap(root, "ValidationErrors", map);
        return map;
    }

    private static void TryGetStringMap(JsonElement root, string prop, Dictionary<string, string> into)
    {
        if (!root.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.Object)
            return;

        foreach (var p in el.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.String)
                into[p.Name] = p.Value.GetString() ?? "";
        }
    }

    private static string? TryGetString(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static bool TryParseValidateCampaignFailure(
        string toolName,
        string resultJson,
        out BackendToolFailure? failure)
    {
        failure = null;
        if (!IsValidateCampaignTool(toolName))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (!root.TryGetProperty("isValid", out var isValidEl) || isValidEl.ValueKind != JsonValueKind.False)
                return false;

            var errors = ExtractValidateErrors(root);
            if (errors.Count == 0)
                return false;

            failure = new BackendToolFailure
            {
                ToolName = toolName,
                RawJson = resultJson,
                ParseStatus = BackendToolFailureParseStatus.Success,
                ValidationErrors = errors
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Dictionary<string, string> ExtractValidateErrors(JsonElement root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("validation", out var validation)
            || validation.ValueKind != JsonValueKind.Object
            || !validation.TryGetProperty("errors", out var errors)
            || errors.ValueKind != JsonValueKind.Array)
        {
            return map;
        }

        var index = 0;
        foreach (var error in errors.EnumerateArray())
        {
            var field = error.TryGetProperty("field", out var f) && f.ValueKind == JsonValueKind.String
                ? f.GetString() ?? $"validation.errors[{index}]"
                : $"validation.errors[{index}]";
            var message = error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString() ?? ""
                : error.GetRawText();
            map[field] = message;
            index++;
        }

        return map;
    }

    private static bool IsValidateCampaignTool(string toolName) =>
        string.Equals(toolName, "ValidateCampaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "validate_campaign", StringComparison.OrdinalIgnoreCase);
}
