using System.Collections.Generic;
using System.Text.Json;

namespace Journeys.CampaignAgent.Remediation;

/// <summary>Outcome of a pattern-recipes digest, with sizes for logging.</summary>
public sealed record RulePatternRecipesDigestResult(string Json, bool Transformed, int OriginalChars, int DigestChars);

/// <summary>
/// Compacts GetRulePatternRecipes JSON for campaign-agent context: retains pattern ids, titles,
/// and rule-kind semantics summaries while stripping full minimalSkeleton JSON.
/// </summary>
public static class RulePatternRecipesDigester
{
    public const string Note =
        "Full pattern skeletons suppressed for context efficiency; " +
        "fetch MCP resource journeys://rules-engine/pattern-recipes/v1 or call get_rule_pattern_recipes for complete JSON.";

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static RulePatternRecipesDigestResult Digest(string rawJson) =>
        Digest(rawJson, fullSkeletonForPatternId: null);

    public static RulePatternRecipesDigestResult Digest(string rawJson, string? fullSkeletonForPatternId)
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
                var innerResult = Digest(innerJson, fullSkeletonForPatternId);
                if (!innerResult.Transformed)
                    return Passthrough(rawJson);

                var rewrapped = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["$type"] = "text",
                    ["text"] = innerResult.Json
                });
                return new RulePatternRecipesDigestResult(rewrapped, true, rawJson.Length, rewrapped.Length);
            }

            if (!string.IsNullOrWhiteSpace(fullSkeletonForPatternId))
            {
                var carved = TryBuildSinglePatternFull(root, rawJson, fullSkeletonForPatternId);
                if (carved != null)
                    return carved;
            }

            return DigestCore(root, rawJson);
        }
    }

    private static RulePatternRecipesDigestResult? TryBuildSinglePatternFull(
        JsonElement root,
        string rawJson,
        string patternId)
    {
        if (!ToolResultSuccessEvaluator.LooksSuccessful(rawJson) || root.ValueKind != JsonValueKind.Object)
            return null;
        if (!TryGetProperty(root, "patterns", out var patterns) || patterns.ValueKind != JsonValueKind.Array)
            return null;

        JsonElement? match = null;
        foreach (var pattern in patterns.EnumerateArray())
        {
            if (TryGetString(pattern, "id", out var id)
                && string.Equals(id, patternId, StringComparison.OrdinalIgnoreCase))
            {
                match = pattern;
                break;
            }
        }

        if (match == null)
            return null;

        var payload = new Dictionary<string, object?>
        {
            ["note"] = "Full minimalSkeleton retained for pinned journey pattern.",
            ["schemaVersion"] = TryGetString(root, "schemaVersion", out var sv) ? sv : null,
            ["patterns"] = new[] { JsonSerializer.Deserialize<object>(match.Value.GetRawText()) }
        };

        var json = JsonSerializer.Serialize(payload, CompactOptions);
        return new RulePatternRecipesDigestResult(json, true, rawJson.Length, json.Length);
    }

    private static RulePatternRecipesDigestResult DigestCore(JsonElement root, string rawJson)
    {
        if (!ToolResultSuccessEvaluator.LooksSuccessful(rawJson) || root.ValueKind != JsonValueKind.Object)
            return Passthrough(rawJson);

        if (!TryGetProperty(root, "schemaVersion", out _))
            return Passthrough(rawJson);

        var payload = BuildCompact(root);
        var json = JsonSerializer.Serialize(payload, CompactOptions);
        return new RulePatternRecipesDigestResult(json, true, rawJson.Length, json.Length);
    }

    private static Dictionary<string, object?> BuildCompact(JsonElement root)
    {
        var result = new Dictionary<string, object?>
        {
            ["note"] = Note
        };

        if (TryGetString(root, "schemaVersion", out var schemaVersion))
            result["schemaVersion"] = schemaVersion;

        if (TryGetProperty(root, "patterns", out var patterns) && patterns.ValueKind == JsonValueKind.Array)
            result["patterns"] = ReadPatterns(patterns);

        if (TryGetProperty(root, "ruleSemantics", out var semantics) && semantics.ValueKind == JsonValueKind.Array)
            result["ruleSemantics"] = ReadRuleSemantics(semantics);

        return result;
    }

    private static List<Dictionary<string, object?>> ReadPatterns(JsonElement patterns)
    {
        var list = new List<Dictionary<string, object?>>();
        foreach (var pattern in patterns.EnumerateArray())
        {
            if (pattern.ValueKind != JsonValueKind.Object)
                continue;

            var compact = new Dictionary<string, object?>();
            if (TryGetString(pattern, "id", out var id))
                compact["id"] = id;
            if (TryGetString(pattern, "title", out var title))
                compact["title"] = title;
            if (TryGetString(pattern, "whenToUse", out var whenToUse))
                compact["whenToUse"] = whenToUse;
            if (TryGetStringArray(pattern, "antiPatterns", out var antiPatterns))
                compact["antiPatterns"] = antiPatterns;
            if (TryGetString(pattern, "relatedExampleCampaignId", out var exampleId))
                compact["relatedExampleCampaignId"] = exampleId;
            list.Add(compact);
        }

        return list;
    }

    private static List<Dictionary<string, object?>> ReadRuleSemantics(JsonElement semantics)
    {
        var list = new List<Dictionary<string, object?>>();
        foreach (var entry in semantics.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var compact = new Dictionary<string, object?>();
            if (TryGetString(entry, "id", out var id))
                compact["id"] = id;
            if (TryGetString(entry, "evaluates", out var evaluates))
                compact["evaluates"] = evaluates;
            if (TryGetStringArray(entry, "relatedPatterns", out var related))
                compact["relatedPatterns"] = related;
            list.Add(compact);
        }

        return list;
    }

    private static bool TryGetProperty(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value))
            return true;

        foreach (var prop in el.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetString(JsonElement el, string name, out string? value)
    {
        value = null;
        if (!TryGetProperty(el, name, out var prop) || prop.ValueKind != JsonValueKind.String)
            return false;
        value = prop.GetString();
        return true;
    }

    private static bool TryGetStringArray(JsonElement el, string name, out List<string> values)
    {
        values = new List<string>();
        if (!TryGetProperty(el, name, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrEmpty(s))
                    values.Add(s);
            }
        }

        return values.Count > 0;
    }

    private static RulePatternRecipesDigestResult Passthrough(string rawJson) =>
        new(rawJson, false, rawJson.Length, rawJson.Length);
}
