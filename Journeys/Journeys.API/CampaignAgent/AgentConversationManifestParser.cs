using System.Text.Json;
using Journeys.Core.Models;
using Journeys.DTO.Models;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Derives mutable entity identifiers from persisted <see cref="AgentMessage"/> rows by reading
/// MEAI assistant transcripts (function calls) and correlating tool-result rows by <c>callId</c>.
/// </summary>
public static class AgentConversationManifestParser
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Builds a manifest from conversation messages ordered by <see cref="AgentMessage.Sequence"/>.
    /// </summary>
    public static AgentConversationClearManifest BuildManifest(IReadOnlyList<AgentMessage> messages)
    {
        var manifest = new AgentConversationClearManifest();
        if (messages.Count == 0)
            return manifest;

        var ordered = messages.OrderBy(m => m.Sequence).ToList();

        var campaignsSeen = new HashSet<CampaignDeleteRef>();
        var patSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var saveSeen = new HashSet<SaveModelEntityRef>();

        var callsById = new Dictionary<string, (string Name, IDictionary<string, object?>? Args)>(StringComparer.Ordinal);

        foreach (var m in ordered)
        {
            if (!string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!CampaignAgentMeaiTranscriptCodec.IsMeaiTranscriptEnvelope(m.Content))
                continue;

            ChatMessage cm;
            try
            {
                cm = CampaignAgentMeaiTranscriptCodec.DeserializeTranscriptMessage(m.Content);
            }
            catch
            {
                continue;
            }

            foreach (var content in cm.Contents)
            {
                if (content is not FunctionCallContent fc || string.IsNullOrWhiteSpace(fc.CallId))
                    continue;
                callsById[fc.CallId] = (fc.Name ?? "", fc.Arguments);
            }
        }

        var resultByCallId = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var m in ordered)
        {
            if (!string.Equals(m.Role, "tool", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.IsNullOrWhiteSpace(m.ToolCallId))
                continue;
            resultByCallId[m.ToolCallId] = m.ToolResultJson;
        }

        foreach (var kv in callsById)
        {
            resultByCallId.TryGetValue(kv.Key, out var toolResultJson);
            ApplyCall(kv.Value.Name, kv.Value.Args, toolResultJson, campaignsSeen, patSeen, saveSeen, manifest);
        }

        return manifest;
    }

    private static void ApplyCall(
        string toolName,
        IDictionary<string, object?>? args,
        string? toolResultJson,
        HashSet<CampaignDeleteRef> campaignsSeen,
        HashSet<string> patSeen,
        HashSet<SaveModelEntityRef> saveSeen,
        AgentConversationClearManifest manifest)
    {
        if (IsUpsertCampaign(toolName))
        {
            var r = TryCampaignFromArgs(args);
            if (r != null && campaignsSeen.Add(r))
                manifest.Campaigns.Add(r);
            return;
        }

        if (IsDeleteCampaign(toolName))
        {
            var r = TryCampaignDeleteRefFromDeleteToolArgs(args);
            if (r != null && campaignsSeen.Add(r))
                manifest.Campaigns.Add(r);
            return;
        }

        if (IsUpsertPointAccountType(toolName))
        {
            var id = TryPatIdFromArgs(args);
            if (!string.IsNullOrWhiteSpace(id) && patSeen.Add(id))
                manifest.PointAccountTypeIds.Add(id);
            return;
        }

        if (CampaignAgentBackendMcp.IsSaveModelToolName(toolName))
        {
            foreach (var r in TrySaveModelRefs(args, toolResultJson))
            {
                if (saveSeen.Add(r))
                    manifest.SaveModelEntities.Add(r);
            }
        }
    }

    private static bool IsUpsertCampaign(string? name) =>
        name != null
        && (string.Equals(name, "UpsertCampaign", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "upsert_campaign", StringComparison.OrdinalIgnoreCase));

    private static bool IsUpsertPointAccountType(string? name) =>
        name != null
        && (string.Equals(name, "UpsertPointAccountType", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "upsert_point_account_type", StringComparison.OrdinalIgnoreCase));

    private static bool IsDeleteCampaign(string? name) =>
        name != null
        && (string.Equals(name, "DeleteCampaign", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "delete_campaign", StringComparison.OrdinalIgnoreCase));

    private static CampaignDeleteRef? TryCampaignDeleteRefFromDeleteToolArgs(IDictionary<string, object?>? args)
    {
        var id = GetArgString(args, "campaignId");
        if (string.IsNullOrWhiteSpace(id))
            return null;
        var status = GetArgString(args, "status");
        if (string.IsNullOrWhiteSpace(status))
            status = CampaignStatusStrings.Live;
        return new CampaignDeleteRef(id, status);
    }

    private static CampaignDeleteRef? TryCampaignFromArgs(IDictionary<string, object?>? args)
    {
        var json = GetArgRawJson(args, "campaignJson");
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var dto = JsonSerializer.Deserialize<CampaignDto>(json, JsonOpts);
            if (string.IsNullOrWhiteSpace(dto?.Id) || string.IsNullOrWhiteSpace(dto.Status))
                return null;
            return new CampaignDeleteRef(dto.Id!, dto.Status);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryPatIdFromArgs(IDictionary<string, object?>? args)
    {
        var json = GetArgRawJson(args, "pointAccountTypeJson");
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var dto = JsonSerializer.Deserialize<PointAccountTypeDto>(json, JsonOpts);
            return string.IsNullOrWhiteSpace(dto?.Id) ? null : dto.Id;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<SaveModelEntityRef> TrySaveModelRefs(IDictionary<string, object?>? args, string? toolResultJson)
    {
        var modelName = GetArgString(args, "modelName", "model");
        var entityRaw =
            GetArgRawJson(args, "entityJson")
            ?? GetArgRawJson(args, "entity")
            ?? GetArgRawJson(args, "modelJson")
            ?? GetArgRawJson(args, "data");

        string? entityId = TryReadIdFromJson(entityRaw);
        entityId ??= TryReadIdFromJson(toolResultJson);

        if (!string.IsNullOrWhiteSpace(modelName) || !string.IsNullOrWhiteSpace(entityId))
            yield return new SaveModelEntityRef(modelName, entityId);
    }

    private static string? TryReadIdFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            foreach (var prop in new[] { "id", "Id", "entityId", "naturalKey" })
            {
                if (root.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String)
                {
                    var s = p.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }
        }
        catch
        {
            /* ignore */
        }

        return null;
    }

    private static string? GetArgString(IDictionary<string, object?>? args, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (args == null || !TryGetArgIgnoreCase(args, key, out var v) || v == null)
                continue;
            switch (v)
            {
                case string s:
                    return s;
                case JsonElement je when je.ValueKind == JsonValueKind.String:
                    return je.GetString();
                default:
                    return v.ToString();
            }
        }

        return null;
    }

    private static string? GetArgRawJson(IDictionary<string, object?>? args, string key)
    {
        if (args == null || !TryGetArgIgnoreCase(args, key, out var v) || v == null)
            return null;
        return v switch
        {
            string s => s,
            // Persisted MEAI args deserialize as JsonElements; string-valued JSON blobs must use GetString(),
            // not GetRawText(), which includes JSON quotes and breaks nested DTO deserialization.
            JsonElement je => je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText(),
            _ => JsonSerializer.Serialize(v, JsonOpts),
        };
    }

    private static bool TryGetArgIgnoreCase(IDictionary<string, object?> args, string key, out object? value)
    {
        foreach (var kv in args)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}
