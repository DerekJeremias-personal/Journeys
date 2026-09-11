using System.Collections.Generic;
using System.Text.Json;

namespace Journeys.CampaignAgent.Remediation;

/// <summary>Outcome of an example-campaign digest, with sizes for logging.</summary>
public sealed record ExampleCampaignDigestResult(string Json, bool Transformed, int OriginalChars, int DigestChars);

/// <summary>
/// Turns a successful GetExampleCampaign payload into a compact skeleton (counts + identifiers only).
/// Error responses and unrecognized shapes pass through unchanged.
/// </summary>
public static class ExampleCampaignDigester
{
    public const string Note =
        "Example campaign JSON suppressed; mericantires packs are structural references only.";

    public const string ExampleTenant = "mericantires";

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static ExampleCampaignDigestResult Digest(string rawJson) =>
        Digest(rawJson, useStructuralExcerpt: false);

    public static ExampleCampaignDigestResult Digest(string rawJson, bool useStructuralExcerpt)
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
                var innerResult = Digest(innerJson, useStructuralExcerpt);
                if (!innerResult.Transformed)
                    return Passthrough(rawJson);

                var rewrapped = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["$type"] = "text",
                    ["text"] = innerResult.Json
                });
                return new ExampleCampaignDigestResult(rewrapped, true, rawJson.Length, rewrapped.Length);
            }

            if (useStructuralExcerpt)
            {
                var excerpt = ExampleCampaignStructuralExcerpt.TryBuild(rawJson);
                if (!string.IsNullOrWhiteSpace(excerpt))
                    return new ExampleCampaignDigestResult(excerpt, true, rawJson.Length, excerpt.Length);
            }

            return DigestCore(root, rawJson);
        }
    }

    private static ExampleCampaignDigestResult DigestCore(JsonElement root, string rawJson)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return Passthrough(rawJson);

        if (HasStringError(root))
            return Passthrough(rawJson);

        if (!ToolResultSuccessEvaluator.LooksSuccessful(rawJson))
            return Passthrough(rawJson);

        if (!TryGetProperty(root, "journey", out var journey) && !TryGetProperty(root, "Journey", out journey))
            return Passthrough(rawJson);

        if (journey.ValueKind != JsonValueKind.Object)
            return Passthrough(rawJson);

        var (nodeCount, ruleSetCount, outcomeCount) = CountJourney(journey);
        var ruleKindHistogram = BuildRuleKindHistogram(journey);

        var campaign = new Dictionary<string, object?>();
        if (TryGetString(root, "id", out var id) || TryGetString(root, "Id", out id))
            campaign["id"] = id;
        if (TryGetString(root, "status", out var status) || TryGetString(root, "Status", out status))
            campaign["status"] = status;
        if (TryGetString(root, "name", out var name) || TryGetString(root, "Name", out name))
            campaign["name"] = name;
        if (TryGetStringArray(root, "events", out var events) || TryGetStringArray(root, "Events", out events))
            campaign["eventModelIds"] = events;

        var exampleId = ResolveExampleId(root);
        var patternId = ResolvePatternId(root, exampleId);
        var title = name;
        string? summary = null;
        if (TryGetString(root, "summary", out var summaryProp))
            summary = summaryProp;

        var payload = new Dictionary<string, object?>
        {
            ["note"] = Note,
            ["exampleId"] = exampleId,
            ["patternId"] = patternId,
            ["title"] = title,
            ["summary"] = summary,
            ["tenant"] = ExampleTenant,
            ["ruleKindHistogram"] = ruleKindHistogram,
            ["campaign"] = campaign,
            ["journey"] = new Dictionary<string, object?>
            {
                ["nodeCount"] = nodeCount,
                ["ruleSetCount"] = ruleSetCount,
                ["outcomeCount"] = outcomeCount
            }
        };

        var json = JsonSerializer.Serialize(payload, CompactOptions);
        return new ExampleCampaignDigestResult(json, true, rawJson.Length, json.Length);
    }

    private static bool HasStringError(JsonElement root)
    {
        if (!TryGetProperty(root, "error", out var err))
            return false;
        return err.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(err.GetString());
    }

    private static string? ResolvePatternId(JsonElement root, string? exampleId)
    {
        if (TryGetString(root, "patternId", out var patternId))
            return patternId;

        if (!string.IsNullOrEmpty(exampleId)
            && ExamplePatternIds.TryGetValue(exampleId, out var mapped))
            return mapped;

        return null;
    }

    private static readonly Dictionary<string, string> ExamplePatternIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["TierSystem"] = "tier-navigation-point-balance",
            ["tier-system-campaign"] = "tier-navigation-point-balance",
            ["HistoricalByCategory"] = "historical-taxonomy-filtered",
            ["historical-by-category-campaign"] = "historical-taxonomy-filtered",
            ["HistoricalSpendThreshold30d"] = "historical-spend-threshold",
            ["historical-spend-threshold-30d"] = "historical-spend-threshold",
            ["HistoricalCount90d"] = "historical-event-count",
            ["historical-count-90d"] = "historical-event-count",
            ["bogo-ecom-campaign"] = "historical-taxonomy-filtered",
            ["hayward-private-tiers-campaign"] = "tier-navigation-point-balance",
        };

    private static Dictionary<string, int> BuildRuleKindHistogram(JsonElement journey)
    {
        var histogram = new Dictionary<string, int>(StringComparer.Ordinal);
        WalkRuleKinds(journey, histogram);
        return histogram;
    }

    private static void WalkRuleKinds(JsonElement node, Dictionary<string, int> histogram)
    {
        if (TryGetProperty(node, "rules", out var rules) || TryGetProperty(node, "Rules", out rules))
        {
            if (rules.ValueKind == JsonValueKind.Array)
            {
                foreach (var rs in rules.EnumerateArray())
                {
                    if (TryGetProperty(rs, "ruleJsonElement", out var ruleTree)
                        || TryGetProperty(rs, "RuleJsonElement", out ruleTree))
                    {
                        CountRuleKind(ruleTree, histogram);
                    }
                }
            }
        }

        if (TryGetProperty(node, "navigation", out var navigation) || TryGetProperty(node, "Navigation", out navigation))
        {
            if (navigation.ValueKind == JsonValueKind.Object)
            {
                foreach (var nav in navigation.EnumerateObject())
                {
                    if (TryGetProperty(nav.Value, "navConstraint", out var navConstraint)
                        || TryGetProperty(nav.Value, "NavConstraint", out navConstraint))
                    {
                        CountRuleKind(navConstraint, histogram);
                    }
                }
            }
        }

        if (TryGetProperty(node, "children", out var children) || TryGetProperty(node, "Children", out children))
        {
            if (children.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in children.EnumerateArray())
                    WalkRuleKinds(child, histogram);
            }
        }
    }

    private static void CountRuleKind(JsonElement rule, Dictionary<string, int> histogram)
    {
        if (rule.ValueKind != JsonValueKind.Object)
            return;

        if (TryGetString(rule, "Kind", out var kind) || TryGetString(rule, "kind", out kind))
        {
            histogram.TryGetValue(kind!, out var count);
            histogram[kind!] = count + 1;
        }

        if (TryGetProperty(rule, "Children", out var children) || TryGetProperty(rule, "children", out children))
        {
            if (children.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in children.EnumerateArray())
                    CountRuleKind(child, histogram);
            }
        }

        foreach (var prop in rule.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object)
                continue;
            if (prop.Name.Equals("Children", StringComparison.OrdinalIgnoreCase)
                || prop.Name.Equals("children", StringComparison.OrdinalIgnoreCase))
                continue;
            if (TryGetString(prop.Value, "Kind", out _) || TryGetString(prop.Value, "kind", out _))
                CountRuleKind(prop.Value, histogram);
        }
    }

    private static string? ResolveExampleId(JsonElement root)
    {
        if (TryGetString(root, "exampleId", out var exampleId))
            return exampleId;
        if (TryGetString(root, "extCampaignId", out var extId) || TryGetString(root, "ExtCampaignId", out extId))
            return extId;
        if (TryGetString(root, "name", out var name) || TryGetString(root, "Name", out name))
            return name;
        return null;
    }

    private static (int nodeCount, int ruleSetCount, int outcomeCount) CountJourney(JsonElement journey)
    {
        var nodeCount = 0;
        var ruleSetCount = 0;
        var outcomeCount = 0;
        WalkNodes(journey, ref nodeCount, ref ruleSetCount, ref outcomeCount);
        return (nodeCount, ruleSetCount, outcomeCount);
    }

    private static void WalkNodes(JsonElement node, ref int nodeCount, ref int ruleSetCount, ref int outcomeCount)
    {
        nodeCount++;

        if (TryGetProperty(node, "rules", out var rules) || TryGetProperty(node, "Rules", out rules))
        {
            if (rules.ValueKind == JsonValueKind.Array)
            {
                foreach (var rs in rules.EnumerateArray())
                {
                    ruleSetCount++;
                    if (TryGetProperty(rs, "outcomesJsonElement", out var outcomes)
                        || TryGetProperty(rs, "OutcomesJsonElement", out outcomes)
                        || TryGetProperty(rs, "outcomes", out outcomes)
                        || TryGetProperty(rs, "Outcomes", out outcomes))
                    {
                        if (outcomes.ValueKind == JsonValueKind.Array)
                            outcomeCount += outcomes.GetArrayLength();
                    }
                }
            }
        }

        if (TryGetProperty(node, "children", out var children) || TryGetProperty(node, "Children", out children))
        {
            if (children.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in children.EnumerateArray())
                    WalkNodes(child, ref nodeCount, ref ruleSetCount, ref outcomeCount);
            }
        }
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
        return !string.IsNullOrEmpty(value);
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

    private static ExampleCampaignDigestResult Passthrough(string rawJson) =>
        new(rawJson, false, rawJson.Length, rawJson.Length);
}
