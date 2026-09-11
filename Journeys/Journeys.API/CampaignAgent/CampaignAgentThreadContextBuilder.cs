using System.Text.Json;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.DTO.Models;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Builds <see cref="CampaignAgentThreadContext"/> from persisted conversation messages
/// (successful UpsertCampaign / UpsertPointAccountType tool calls only).
/// </summary>
public static class CampaignAgentThreadContextBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static CampaignAgentThreadContext Build(
        IReadOnlyList<AgentMessage> messages,
        int maxCampaigns = CampaignAgentThreadContext.DefaultMaxCampaigns,
        int maxPointAccountTypes = CampaignAgentThreadContext.DefaultMaxPointAccountTypes)
    {
        if (messages.Count == 0)
            return new CampaignAgentThreadContext();

        var ordered = messages.OrderBy(m => m.Sequence).ToList();
        var callsInOrder = CollectCallsInSequenceOrder(ordered);
        var resultByCallId = CollectToolResultsByCallId(ordered);

        var campaignsById = new Dictionary<string, (CampaignAgentThreadCampaignRef Ref, long Sequence)>(StringComparer.OrdinalIgnoreCase);
        var patsById = new Dictionary<string, (CampaignAgentThreadPatRef Ref, long Sequence)>(StringComparer.OrdinalIgnoreCase);

        foreach (var call in callsInOrder)
        {
            if (!resultByCallId.TryGetValue(call.CallId, out var toolResultJson))
                continue;
            if (!CampaignWorkflowToolSuccess.LooksSuccessful(toolResultJson))
                continue;

            if (IsUpsertCampaign(call.ToolName))
            {
                var campaign = TryParseCampaign(call.Args, toolResultJson);
                if (campaign is null)
                    continue;
                campaignsById[campaign.CampaignId] = (campaign, call.Sequence);
                continue;
            }

            if (IsUpsertPointAccountType(call.ToolName))
            {
                var pat = TryParsePointAccountType(call.Args, toolResultJson);
                if (pat is null)
                    continue;
                patsById[pat.Id] = (pat, call.Sequence);
            }
        }

        var allCampaigns = campaignsById.Values.OrderBy(v => v.Sequence).Select(v => v.Ref).ToList();
        var allPats = patsById.Values.OrderBy(v => v.Sequence).Select(v => v.Ref).ToList();

        var truncatedCampaigns = Math.Max(0, allCampaigns.Count - maxCampaigns);
        var truncatedPats = Math.Max(0, allPats.Count - maxPointAccountTypes);
        var campaigns = allCampaigns.TakeLast(maxCampaigns).ToList();
        var pats = allPats.TakeLast(maxPointAccountTypes).ToList();

        CampaignAgentThreadCampaignRef? primary = allCampaigns.Count > 0
            ? campaignsById.Values.OrderByDescending(v => v.Sequence).First().Ref
            : null;

        return new CampaignAgentThreadContext
        {
            PrimaryCampaign = primary,
            Campaigns = campaigns,
            PointAccountTypes = pats,
            TruncatedCampaignCount = truncatedCampaigns,
            TruncatedPointAccountTypeCount = truncatedPats
        };
    }

    private static List<CallRef> CollectCallsInSequenceOrder(IReadOnlyList<AgentMessage> ordered)
    {
        var list = new List<CallRef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var m in ordered)
        {
            if (!string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!CampaignAgentMeaiTranscriptCodec.IsMeaiTranscriptEnvelope(m.Content))
                continue;

            ChatMessage cm;
            try
            {
                cm = CampaignAgentMeaiTranscriptCodec.DeserializeTranscriptMessage(m.Content!);
            }
            catch
            {
                continue;
            }

            foreach (var content in cm.Contents)
            {
                if (content is not FunctionCallContent fc || string.IsNullOrWhiteSpace(fc.CallId))
                    continue;
                if (!seen.Add(fc.CallId))
                    continue;
                list.Add(new CallRef(m.Sequence, fc.CallId, fc.Name ?? string.Empty, fc.Arguments));
            }
        }

        return list;
    }

    private static Dictionary<string, string?> CollectToolResultsByCallId(IReadOnlyList<AgentMessage> ordered)
    {
        var resultByCallId = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var m in ordered)
        {
            if (string.Equals(m.Role, "tool", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(m.ToolCallId))
                    continue;
                resultByCallId[m.ToolCallId] = m.ToolResultJson;
                continue;
            }

            if (!string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!CampaignAgentMeaiTranscriptCodec.IsMeaiTranscriptEnvelope(m.Content))
                continue;

            ChatMessage cm;
            try
            {
                cm = CampaignAgentMeaiTranscriptCodec.DeserializeTranscriptMessage(m.Content!);
            }
            catch
            {
                continue;
            }

            foreach (var content in cm.Contents)
            {
                if (content is not FunctionResultContent fr || string.IsNullOrWhiteSpace(fr.CallId))
                    continue;
                resultByCallId[fr.CallId] = SerializeFunctionResult(fr.Result);
            }
        }

        return resultByCallId;
    }

    private static string? SerializeFunctionResult(object? result)
    {
        if (result is null)
            return null;
        return result switch
        {
            string s => s,
            JsonElement je => je.GetRawText(),
            _ => JsonSerializer.Serialize(result, JsonOpts)
        };
    }

    private static CampaignAgentThreadCampaignRef? TryParseCampaign(
        IDictionary<string, object?>? args,
        string? toolResultJson)
    {
        CampaignDto? dto = null;
        var argsJson = GetArgRawJson(args, "campaignJson");
        if (!string.IsNullOrWhiteSpace(argsJson))
        {
            try
            {
                dto = JsonSerializer.Deserialize<CampaignDto>(argsJson, JsonOpts);
            }
            catch
            {
                /* ignore */
            }
        }

        if ((dto is null || string.IsNullOrWhiteSpace(dto.Id)) && !string.IsNullOrWhiteSpace(toolResultJson))
        {
            try
            {
                dto = JsonSerializer.Deserialize<CampaignDto>(toolResultJson, JsonOpts);
            }
            catch
            {
                /* ignore */
            }
        }

        if (string.IsNullOrWhiteSpace(dto?.Id))
            return null;

        var status = dto.Status;
        if (string.IsNullOrWhiteSpace(status) && !string.IsNullOrWhiteSpace(toolResultJson))
            status = TryReadJsonStringProperty(toolResultJson, "status");
        if (string.IsNullOrWhiteSpace(status))
            status = CampaignStatusStrings.Draft;

        var events = dto.Events?
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .ToList() ?? new List<string>();

        var name = string.IsNullOrWhiteSpace(dto.Name) ? "Campaign" : dto.Name.Trim();
        return new CampaignAgentThreadCampaignRef(dto.Id!, status, name, events);
    }

    private static CampaignAgentThreadPatRef? TryParsePointAccountType(
        IDictionary<string, object?>? args,
        string? toolResultJson)
    {
        PointAccountTypeDto? dto = null;
        var argsJson = GetArgRawJson(args, "pointAccountTypeJson");
        if (!string.IsNullOrWhiteSpace(argsJson))
        {
            try
            {
                dto = JsonSerializer.Deserialize<PointAccountTypeDto>(argsJson, JsonOpts);
            }
            catch
            {
                /* ignore */
            }
        }

        if ((dto is null || string.IsNullOrWhiteSpace(dto.Id)) && !string.IsNullOrWhiteSpace(toolResultJson))
        {
            try
            {
                dto = JsonSerializer.Deserialize<PointAccountTypeDto>(toolResultJson, JsonOpts);
            }
            catch
            {
                /* ignore */
            }
        }

        if (string.IsNullOrWhiteSpace(dto?.Id))
            return null;

        var label = string.IsNullOrWhiteSpace(dto.Name) ? dto.Id! : dto.Name.Trim();
        return new CampaignAgentThreadPatRef(dto.Id!, label);
    }

    private static string? TryReadJsonStringProperty(string json, string propertyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(propertyName, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        }
        catch
        {
            /* ignore */
        }

        return null;
    }

    private static bool IsUpsertCampaign(string? name) =>
        name != null
        && (string.Equals(name, "UpsertCampaign", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "upsert_campaign", StringComparison.OrdinalIgnoreCase));

    private static bool IsUpsertPointAccountType(string? name) =>
        name != null
        && (string.Equals(name, "UpsertPointAccountType", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "upsert_point_account_type", StringComparison.OrdinalIgnoreCase));

    private static string? GetArgRawJson(IDictionary<string, object?>? args, string key)
    {
        if (args == null || !TryGetArgIgnoreCase(args, key, out var v) || v == null)
            return null;
        return v switch
        {
            string s => s,
            JsonElement je => je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText(),
            _ => JsonSerializer.Serialize(v, JsonOpts)
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

    private sealed record CallRef(long Sequence, string CallId, string ToolName, IDictionary<string, object?>? Args);
}
