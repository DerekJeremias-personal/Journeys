using System.Collections.Generic;
using System.Text.Json;

namespace Journeys.CampaignAgent.Remediation;

/// <summary>
/// Compacts validate_campaign results: compact ack on success/warnings; compact error summary on hard failures.
/// </summary>
public static class CampaignValidationDigester
{
    private const int MaxTopErrors = 5;
    private const int MaxErrorMessageChars = 240;
    private const int MaxNextSteps = 3;

    public static MutationDigestResult Digest(string rawJson, string? payloadFingerprint = null)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return Passthrough(rawJson ?? string.Empty);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(rawJson); }
        catch (JsonException) { return Passthrough(rawJson); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Passthrough(rawJson);

            if (JsonPropertyReader.TryGetProperty(root, "errors", out _)
                && !JsonPropertyReader.TryGetProperty(root, "isValid", out _))
                return Passthrough(rawJson);

            if (!JsonPropertyReader.TryGetProperty(root, "isValid", out var isValidEl)
                || (isValidEl.ValueKind != JsonValueKind.True && isValidEl.ValueKind != JsonValueKind.False))
            {
                return Passthrough(rawJson);
            }

            var isValid = isValidEl.GetBoolean();
            var json = isValid
                ? WriteCompactAck(root, payloadFingerprint)
                : WriteCompactErrorAck(root, payloadFingerprint);
            return new MutationDigestResult(json, true, rawJson.Length, json.Length);
        }
    }

    private static MutationDigestResult Passthrough(string rawJson) =>
        new(rawJson, false, rawJson.Length, rawJson.Length);

    private static string WriteCompactAck(JsonElement root, string? payloadFingerprint)
    {
        var buffer = new System.IO.MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteBoolean("validateAck", true);
            w.WriteBoolean("isValid", true);

            if (!string.IsNullOrWhiteSpace(payloadFingerprint))
                w.WriteString("payloadFingerprint", payloadFingerprint);

            WriteSummary(w, root);
            WriteTopWarnings(w, root);
            WriteNextSteps(w, root);

            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string WriteCompactErrorAck(JsonElement root, string? payloadFingerprint)
    {
        var buffer = new System.IO.MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteBoolean("validateAck", true);
            w.WriteBoolean("isValid", false);
            w.WriteString("note",
                "Full validation error list suppressed for context efficiency; fix topErrors and re-validate.");

            if (!string.IsNullOrWhiteSpace(payloadFingerprint))
                w.WriteString("payloadFingerprint", payloadFingerprint);

            WriteSummary(w, root);
            WriteTopErrors(w, root);
            WriteNextSteps(w, root);

            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteSummary(Utf8JsonWriter w, JsonElement root)
    {
        if (JsonPropertyReader.TryGetProperty(root, "summary", out var summary)
            && summary.ValueKind == JsonValueKind.Object)
        {
            w.WritePropertyName("summary");
            summary.WriteTo(w);
        }
    }

    private static void WriteTopWarnings(Utf8JsonWriter w, JsonElement root)
    {
        w.WriteStartArray("topWarnings");
        if (JsonPropertyReader.TryGetProperty(root, "validation", out var validation)
            && JsonPropertyReader.TryGetProperty(validation, "warnings", out var warnings)
            && warnings.ValueKind == JsonValueKind.Array)
        {
            var count = 0;
            foreach (var warning in warnings.EnumerateArray())
            {
                if (count >= 3)
                    break;
                if (JsonPropertyReader.TryGetProperty(warning, "code", out var code)
                    && code.ValueKind == JsonValueKind.String)
                    w.WriteStringValue(code.GetString());
                count++;
            }
        }
        w.WriteEndArray();
    }

    private static void WriteTopErrors(Utf8JsonWriter w, JsonElement root)
    {
        w.WriteStartArray("topErrors");
        if (JsonPropertyReader.TryGetProperty(root, "validation", out var validation)
            && JsonPropertyReader.TryGetProperty(validation, "errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array)
        {
            var count = 0;
            foreach (var error in errors.EnumerateArray())
            {
                if (count >= MaxTopErrors)
                    break;

                w.WriteStartObject();
                WriteErrorString(w, error, "code");
                WriteErrorString(w, error, "field");
                WriteErrorMessage(w, error);
                w.WriteEndObject();
                count++;
            }
        }
        w.WriteEndArray();
    }

    private static void WriteErrorString(Utf8JsonWriter w, JsonElement error, string name)
    {
        if (JsonPropertyReader.TryGetProperty(error, name, out var prop) && prop.ValueKind == JsonValueKind.String)
            w.WriteString(name, prop.GetString());
    }

    private static void WriteErrorMessage(Utf8JsonWriter w, JsonElement error)
    {
        if (!JsonPropertyReader.TryGetProperty(error, "message", out var message)
            || message.ValueKind != JsonValueKind.String)
            return;

        var text = message.GetString() ?? string.Empty;
        if (text.Length > MaxErrorMessageChars)
            text = text[..MaxErrorMessageChars] + "…";
        w.WriteString("message", text);
    }

    private static void WriteNextSteps(Utf8JsonWriter w, JsonElement root)
    {
        w.WriteStartArray("nextSteps");
        if (JsonPropertyReader.TryGetProperty(root, "nextSteps", out var steps)
            && steps.ValueKind == JsonValueKind.Array)
        {
            var count = 0;
            foreach (var step in steps.EnumerateArray())
            {
                if (count >= MaxNextSteps)
                    break;
                if (step.ValueKind == JsonValueKind.String)
                    w.WriteStringValue(step.GetString());
                count++;
            }
        }
        w.WriteEndArray();
    }
}
