using System.Text.Json;

namespace Journeys.CampaignAgent.Remediation;

public sealed record ListCampaignsDigestResult(string Json, bool Transformed, int OriginalChars, int DigestChars);

/// <summary>
/// Compacts ListCampaigns paged payloads to id/name/status (+ optional journey rule set count).
/// Errors and unrecognized shapes pass through unchanged.
/// </summary>
public static class ListCampaignsDigester
{
    public const string Note =
        "Full campaign documents suppressed; use get_campaign(campaignId) or THREAD CONTEXT for the active draft.";

    public const int MaxEntities = 30;

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static ListCampaignsDigestResult Digest(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return Passthrough(rawJson ?? string.Empty);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(rawJson); }
        catch (JsonException) { return Passthrough(rawJson); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("$type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "text"
                && root.TryGetProperty("text", out var inner) && inner.ValueKind == JsonValueKind.String)
            {
                var innerJson = inner.GetString() ?? string.Empty;
                var innerResult = Digest(innerJson);
                if (!innerResult.Transformed)
                    return Passthrough(rawJson);

                var rewrapped = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["$type"] = "text",
                    ["text"] = innerResult.Json
                });
                return new ListCampaignsDigestResult(rewrapped, true, rawJson.Length, rewrapped.Length);
            }

            return DigestCore(root, rawJson);
        }
    }

    private static ListCampaignsDigestResult DigestCore(JsonElement root, string rawJson)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return Passthrough(rawJson);

        if (HasStringError(root) || !ToolResultSuccessEvaluator.LooksSuccessful(rawJson))
            return Passthrough(rawJson);

        if (!TryGetEntitiesArray(root, out var entities))
            return Passthrough(rawJson);

        var summaries = new List<Dictionary<string, object?>>();
        var truncated = false;
        foreach (var entity in entities.EnumerateArray())
        {
            if (summaries.Count >= MaxEntities)
            {
                truncated = true;
                break;
            }

            if (entity.ValueKind != JsonValueKind.Object)
                continue;

            var summary = new Dictionary<string, object?>();
            if (TryGetString(entity, "id", out var id) || TryGetString(entity, "Id", out id))
                summary["campaignId"] = id;
            if (TryGetString(entity, "name", out var name) || TryGetString(entity, "Name", out name))
                summary["name"] = name;
            if (TryGetString(entity, "status", out var status) || TryGetString(entity, "Status", out status))
                summary["status"] = status;

            var ruleSetCount = TryReadJourneyRuleSetCount(entity);
            if (ruleSetCount.HasValue)
                summary["journeyRuleSetCount"] = ruleSetCount.Value;

            if (summary.Count > 0)
                summaries.Add(summary);
        }

        var payload = new Dictionary<string, object?>
        {
            ["_agentDigest"] = "list_campaigns",
            ["note"] = Note,
            ["count"] = summaries.Count,
            ["campaigns"] = summaries
        };

        if (truncated)
            payload["truncated"] = true;

        if (TryGetString(root, "continuationToken", out var token) || TryGetString(root, "ContinuationToken", out token))
            payload["continuationToken"] = token;

        var json = JsonSerializer.Serialize(payload, CompactOptions);
        return new ListCampaignsDigestResult(json, true, rawJson.Length, json.Length);
    }

    private static bool TryGetEntitiesArray(JsonElement root, out JsonElement entities)
    {
        if (TryGetProperty(root, "entities", out entities) || TryGetProperty(root, "Entities", out entities))
            return entities.ValueKind == JsonValueKind.Array;

        entities = default;
        return false;
    }

    private static int? TryReadJourneyRuleSetCount(JsonElement campaign)
    {
        if (!TryGetProperty(campaign, "journey", out var journey) && !TryGetProperty(campaign, "Journey", out journey))
            return null;
        if (journey.ValueKind != JsonValueKind.Object)
            return null;

        if (TryGetProperty(journey, "ruleSets", out var ruleSets) || TryGetProperty(journey, "RuleSets", out ruleSets))
        {
            if (ruleSets.ValueKind == JsonValueKind.Array)
                return ruleSets.GetArrayLength();
        }

        if (TryGetInt(journey, "ruleSetCount", out var count) || TryGetInt(journey, "RuleSetCount", out count))
            return count;

        return null;
    }

    private static bool HasStringError(JsonElement root) =>
        (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
        || (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object);

    private static bool TryGetProperty(JsonElement obj, string name, out JsonElement value) =>
        obj.TryGetProperty(name, out value);

    private static bool TryGetString(JsonElement obj, string name, out string? value)
    {
        value = null;
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return false;
        value = el.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetInt(JsonElement obj, string name, out int value)
    {
        value = 0;
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Number)
            return false;
        return el.TryGetInt32(out value);
    }

    private static ListCampaignsDigestResult Passthrough(string rawJson) =>
        new(rawJson, false, rawJson.Length, rawJson.Length);
}
