using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Journeys.CampaignAgent.Remediation;

/// <summary>Outcome of a mutation-result digest, with sizes for logging.</summary>
public sealed record MutationDigestResult(string Json, bool Transformed, int OriginalChars, int DigestChars);

/// <summary>
/// Turns a SUCCESSFUL upsert_campaign result (a verbatim echo of the submitted campaign) into a compact
/// acknowledgment: server-authoritative fields kept verbatim, the agent-authored journey summarized to
/// names + per-rule-set outcome counts. Failures and unrecognized shapes pass through unchanged.
/// </summary>
public static class CampaignMutationDigester
{
    public const string Note =
        "Full campaign definition suppressed for context efficiency; " +
        "call get_campaign_assistant_context for the persisted detail.";

    private static readonly string[] IdFields = { "Id", "CampaignId", "ExtCampaignId" };
    private static readonly string[] ScalarFields = { "Status", "Name", "StartDate", "EndDate" };

    public static MutationDigestResult Digest(string rawJson)
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
                return new MutationDigestResult(rewrapped, true, rawJson.Length, rewrapped.Length);
            }

            return DigestCore(root, rawJson);
        }
    }

    private static MutationDigestResult DigestCore(JsonElement root, string rawJson)
    {
        if (!ToolResultSuccessEvaluator.LooksSuccessful(rawJson) || root.ValueKind != JsonValueKind.Object)
            return Passthrough(rawJson);

        if (TryGetJourney(root, out var journey) && journey.ValueKind == JsonValueKind.Object)
        {
            var json = WriteAck(root, journey);
            return new MutationDigestResult(json, true, rawJson.Length, json.Length);
        }

        if (!HasAnyId(root))
            return Passthrough(rawJson);

        var ack = WriteAck(root, default);
        return new MutationDigestResult(ack, true, rawJson.Length, ack.Length);
    }

    private static bool TryGetJourney(JsonElement root, out JsonElement journey)
    {
        if (JsonPropertyReader.TryGetProperty(root, "journey", out journey)
            && journey.ValueKind == JsonValueKind.Object)
            return true;

        if (JsonPropertyReader.TryGetProperty(root, "campaign", out var campaign)
            && JsonPropertyReader.TryGetProperty(campaign, "journey", out journey)
            && journey.ValueKind == JsonValueKind.Object)
            return true;

        journey = default;
        return false;
    }

    private static MutationDigestResult Passthrough(string rawJson) =>
        new(rawJson, false, rawJson.Length, rawJson.Length);

    private static bool HasAnyId(JsonElement root)
    {
        foreach (var f in IdFields)
        {
            if (JsonPropertyReader.TryGetProperty(root, f, out var v)
                && v.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(v.GetString()))
                return true;
        }

        if (JsonPropertyReader.TryGetProperty(root, "campaign", out var campaign))
        {
            foreach (var f in IdFields)
            {
                if (JsonPropertyReader.TryGetProperty(campaign, f, out var v)
                    && v.ValueKind == JsonValueKind.String
                    && !string.IsNullOrEmpty(v.GetString()))
                    return true;
            }
        }

        return false;
    }

    private static string WriteAck(JsonElement root, JsonElement journey)
    {
        var campaignRoot = JsonPropertyReader.TryGetProperty(root, "campaign", out var nested)
                           && nested.ValueKind == JsonValueKind.Object
            ? nested
            : root;

        var buffer = new System.IO.MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("note", Note);

            w.WriteStartObject("campaign");
            foreach (var f in IdFields) CopyVerbatim(w, campaignRoot, f);
            foreach (var f in ScalarFields) CopyVerbatim(w, campaignRoot, f);
            if (JsonPropertyReader.TryGetProperty(campaignRoot, "events", out var events)
                && events.ValueKind == JsonValueKind.Array)
            {
                w.WritePropertyName("Events");
                events.WriteTo(w);
            }
            else if (JsonPropertyReader.TryGetProperty(campaignRoot, "Events", out events)
                     && events.ValueKind == JsonValueKind.Array)
            {
                w.WritePropertyName("Events");
                events.WriteTo(w);
            }

            if (JsonPropertyReader.TryGetProperty(campaignRoot, "segments", out var segments)
                && segments.ValueKind == JsonValueKind.Array)
                w.WriteNumber("segmentCount", segments.GetArrayLength());
            else if (JsonPropertyReader.TryGetProperty(campaignRoot, "Segments", out segments)
                     && segments.ValueKind == JsonValueKind.Array)
                w.WriteNumber("segmentCount", segments.GetArrayLength());
            w.WriteEndObject();

            if (journey.ValueKind == JsonValueKind.Object)
            {
                w.WritePropertyName("journey");
                WriteJourney(w, journey);
            }

            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteJourney(Utf8JsonWriter w, JsonElement journey)
    {
        w.WriteStartObject();
        WriteNameAs(w, journey, "name");
        w.WriteStartArray("nodes");

        var hasChildren = TryGetArray(journey, "Children", out var children) || TryGetArray(journey, "children", out children);
        var hasRootRules = TryGetArray(journey, "Rules", out var rootRules) || TryGetArray(journey, "rules", out rootRules);

        if (hasRootRules && (!hasChildren || children.GetArrayLength() == 0))
        {
            w.WriteStartObject();
            WriteNameAs(w, journey, "name");
            WriteRuleSets(w, rootRules);
            w.WriteEndObject();
        }
        else if (hasChildren)
        {
            foreach (var node in children.EnumerateArray())
                WriteNode(w, node);
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static bool TryGetArray(JsonElement el, string name, out JsonElement array)
    {
        if (JsonPropertyReader.TryGetProperty(el, name, out array) && array.ValueKind == JsonValueKind.Array)
            return true;
        array = default;
        return false;
    }

    private static void WriteNode(Utf8JsonWriter w, JsonElement node)
    {
        w.WriteStartObject();
        WriteNameAs(w, node, "name");

        if (TryGetArray(node, "Rules", out var rules) || TryGetArray(node, "rules", out rules))
            WriteRuleSets(w, rules);
        else
        {
            w.WriteStartArray("ruleSets");
            w.WriteEndArray();
        }

        if (JsonPropertyReader.TryGetProperty(node, "children", out var nested)
            && nested.ValueKind == JsonValueKind.Array && nested.GetArrayLength() > 0)
        {
            w.WriteStartArray("nodes");
            foreach (var child in nested.EnumerateArray())
                WriteNode(w, child);
            w.WriteEndArray();
        }

        w.WriteEndObject();
    }

    private static void WriteRuleSets(Utf8JsonWriter w, JsonElement rules)
    {
        w.WriteStartArray("ruleSets");
        foreach (var rs in rules.EnumerateArray())
        {
            w.WriteStartObject();
            WriteNameAs(w, rs, "name");
            var outcomes = 0;
            if (JsonPropertyReader.TryGetProperty(rs, "outcomesJsonElement", out var oc)
                && oc.ValueKind == JsonValueKind.Array)
                outcomes = oc.GetArrayLength();
            else if (JsonPropertyReader.TryGetProperty(rs, "OutcomesJsonElement", out oc)
                     && oc.ValueKind == JsonValueKind.Array)
                outcomes = oc.GetArrayLength();
            w.WriteNumber("outcomes", outcomes);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    private static void WriteNameAs(Utf8JsonWriter w, JsonElement el, string destName)
    {
        if (JsonPropertyReader.TryGetProperty(el, "name", out var v) && v.ValueKind == JsonValueKind.String)
            w.WriteString(destName, v.GetString());
    }

    private static void CopyVerbatim(Utf8JsonWriter w, JsonElement root, string name)
    {
        if (JsonPropertyReader.TryGetProperty(root, name, out var v) && v.ValueKind != JsonValueKind.Undefined)
        {
            w.WritePropertyName(name);
            v.WriteTo(w);
        }
    }
}
