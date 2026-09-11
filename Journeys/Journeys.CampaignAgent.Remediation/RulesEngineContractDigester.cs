using System.Collections.Generic;
using System.Text.Json;

namespace Journeys.CampaignAgent.Remediation;

/// <summary>Outcome of a rules-engine contract digest, with sizes for logging.</summary>
public sealed record RulesContractDigestResult(string Json, bool Transformed, int OriginalChars, int DigestChars);

/// <summary>
/// Compacts GetRulesEngineContractSummary JSON for campaign-agent context: retains journey-fix fields
/// (critical rows, enum catalog, kinds) while stripping redundant prose and pretty-printing.
/// </summary>
public static class RulesEngineContractDigester
{
    public const string Note =
        "Full Tier A contract suppressed for context efficiency; " +
        "fetch MCP resource journeys://rules-engine/campaign-contract/v1 for the complete matrix.";

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static RulesContractDigestResult Digest(string rawJson)
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
                return new RulesContractDigestResult(rewrapped, true, rawJson.Length, rewrapped.Length);
            }

            return DigestCore(root, rawJson);
        }
    }

    private static RulesContractDigestResult DigestCore(JsonElement root, string rawJson)
    {
        if (!ToolResultSuccessEvaluator.LooksSuccessful(rawJson) || root.ValueKind != JsonValueKind.Object)
            return Passthrough(rawJson);

        if (!TryGetProperty(root, "matrixVersion", out _))
            return Passthrough(rawJson);

        var payload = BuildCompact(root);
        var json = JsonSerializer.Serialize(payload, CompactOptions);
        return new RulesContractDigestResult(json, true, rawJson.Length, json.Length);
    }

    private static Dictionary<string, object?> BuildCompact(JsonElement root)
    {
        var result = new Dictionary<string, object?>
        {
            ["note"] = Note
        };

        if (TryGetString(root, "matrixVersion", out var matrixVersion))
            result["matrixVersion"] = matrixVersion;
        if (TryGetString(root, "workflowHint", out var workflowHint))
            result["workflowHint"] = workflowHint;

        if (TryGetProperty(root, "tierAErrorShape", out var tierA))
            result["tierAErrorShape"] = ReadTierAErrorShape(tierA);

        if (TryGetStringArray(root, "ruleKinds", out var ruleKinds))
            result["ruleKinds"] = ruleKinds;
        if (TryGetStringArray(root, "outcomeKinds", out var outcomeKinds))
            result["outcomeKinds"] = outcomeKinds;
        if (TryGetStringArray(root, "providerKinds", out var providerKinds))
            result["providerKinds"] = providerKinds;
        if (TryGetStringArray(root, "valueProviderKinds", out var valueProviderKinds))
            result["valueProviderKinds"] = valueProviderKinds;
        if (TryGetStringArray(root, "historicalProviderKinds", out var historicalProviderKinds))
            result["historicalProviderKinds"] = historicalProviderKinds;
        if (TryGetStringArray(root, "navigationCriteriaTypes", out var navigationCriteriaTypes))
            result["navigationCriteriaTypes"] = navigationCriteriaTypes;
        if (TryGetStringArray(root, "evaluatorTypes", out var evaluatorTypes))
            result["evaluatorTypes"] = evaluatorTypes;

        if (TryGetProperty(root, "typeDiscriminatorCatalog", out var typeDiscriminatorCatalog)
            && typeDiscriminatorCatalog.ValueKind == JsonValueKind.Array)
            result["typeDiscriminatorCatalog"] = ReadTypeDiscriminatorCatalog(typeDiscriminatorCatalog);

        if (TryGetProperty(root, "criticalRows", out var criticalRows) && criticalRows.ValueKind == JsonValueKind.Array)
            result["criticalRows"] = ReadCriticalRows(criticalRows);

        if (TryGetProperty(root, "enumCatalog", out var enumCatalog) && enumCatalog.ValueKind == JsonValueKind.Array)
            result["enumCatalog"] = ReadEnumCatalog(enumCatalog);

        return result;
    }

    private static Dictionary<string, object?> ReadTierAErrorShape(JsonElement tierA)
    {
        var shape = new Dictionary<string, object?>();
        if (TryGetString(tierA, "journeyValidationKeyPrefix", out var prefix))
            shape["journeyValidationKeyPrefix"] = prefix;
        if (TryGetString(tierA, "violationTokenFormat", out var format))
            shape["violationTokenFormat"] = format;
        if (TryGetStringArray(tierA, "commonMessageFields", out var fields))
            shape["commonMessageFields"] = fields;
        return shape;
    }

    private static List<Dictionary<string, object?>> ReadCriticalRows(JsonElement rows)
    {
        var list = new List<Dictionary<string, object?>>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
                continue;

            var compact = new Dictionary<string, object?>();
            if (TryGetString(row, "id", out var id))
                compact["id"] = id;
            if (TryGetStringArray(row, "kindMatchers", out var kinds))
                compact["kindMatchers"] = kinds;
            if (TryGetStringArray(row, "requiredJsonProperties", out var required))
                compact["requiredJsonProperties"] = required;
            if (TryGetStringArray(row, "tierAViolationCodes", out var codes))
                compact["tierAViolationCodes"] = codes;
            list.Add(compact);
        }

        return list;
    }

    private static List<Dictionary<string, object?>> ReadTypeDiscriminatorCatalog(JsonElement catalog)
    {
        var list = new List<Dictionary<string, object?>>();
        foreach (var entry in catalog.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var compact = new Dictionary<string, object?>();
            if (TryGetString(entry, "id", out var id))
                compact["id"] = id;
            if (TryGetStringArray(entry, "allowedTypes", out var allowedTypes))
                compact["allowedTypes"] = allowedTypes;
            if (TryGetStringArray(entry, "jsonContexts", out var jsonContexts))
                compact["jsonContexts"] = jsonContexts;
            list.Add(compact);
        }

        return list;
    }

    private static List<Dictionary<string, object?>> ReadEnumCatalog(JsonElement catalog)
    {
        var list = new List<Dictionary<string, object?>>();
        foreach (var entry in catalog.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var compact = new Dictionary<string, object?>();
            if (TryGetString(entry, "id", out var enumName))
                compact["enumName"] = enumName;
            else if (TryGetString(entry, "enumName", out var enumNameAlt))
                compact["enumName"] = enumNameAlt;

            if (TryGetStringArray(entry, "values", out var values))
                compact["validStrings"] = values;
            else if (TryGetStringArray(entry, "validStrings", out var validStrings))
                compact["validStrings"] = validStrings;

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

    private static RulesContractDigestResult Passthrough(string rawJson) =>
        new(rawJson, false, rawJson.Length, rawJson.Length);
}
