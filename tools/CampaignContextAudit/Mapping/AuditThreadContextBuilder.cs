using System.Text.Json;
using CampaignContextAudit.Transcript;
using Journeys.API.CampaignAgent;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace CampaignContextAudit.Mapping;

/// <summary>
/// Production-faithful thread context for SESSION sizing using audit MEAI parsing (no MEAI package dependency).
/// </summary>
public static class AuditThreadContextBuilder
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
                var campaign = TryParseCampaign(call.ArgsJson, toolResultJson);
                if (campaign is null) continue;
                campaignsById[campaign.CampaignId] = (campaign, call.Sequence);
                continue;
            }

            if (IsUpsertPointAccountType(call.ToolName))
            {
                var pat = TryParsePointAccountType(call.ArgsJson, toolResultJson);
                if (pat is null) continue;
                patsById[pat.Id] = (pat, call.Sequence);
            }
        }

        var allCampaigns = campaignsById.Values.OrderBy(v => v.Sequence).Select(v => v.Ref).ToList();
        var allPats = patsById.Values.OrderBy(v => v.Sequence).Select(v => v.Ref).ToList();
        var campaigns = allCampaigns.TakeLast(maxCampaigns).ToList();
        var pats = allPats.TakeLast(maxPointAccountTypes).ToList();
        var primary = allCampaigns.Count > 0
            ? campaignsById.Values.OrderByDescending(v => v.Sequence).First().Ref
            : null;

        return new CampaignAgentThreadContext
        {
            PrimaryCampaign = primary,
            Campaigns = campaigns,
            PointAccountTypes = pats,
            TruncatedCampaignCount = Math.Max(0, allCampaigns.Count - maxCampaigns),
            TruncatedPointAccountTypeCount = Math.Max(0, allPats.Count - maxPointAccountTypes)
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
            foreach (var call in MeaiEnvelope.ExtractFunctionCalls(m.Content))
            {
                if (string.IsNullOrWhiteSpace(call.CallId) || !seen.Add(call.CallId))
                    continue;
                list.Add(new CallRef(m.Sequence, call.CallId, call.Name, call.ArgumentsRaw));
            }
        }

        return list;
    }

    private static Dictionary<string, string?> CollectToolResultsByCallId(IReadOnlyList<AgentMessage> ordered)
    {
        var resultByCallId = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var m in ordered)
        {
            if (string.Equals(m.Role, "tool", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(m.ToolCallId))
            {
                resultByCallId[m.ToolCallId] = m.ToolResultJson;
                continue;
            }

            if (!string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var result in MeaiEnvelope.ExtractFunctionResults(m.Content))
            {
                if (!string.IsNullOrWhiteSpace(result.CallId))
                    resultByCallId[result.CallId] = result.ResultRaw;
            }
        }

        return resultByCallId;
    }

    private static CampaignAgentThreadCampaignRef? TryParseCampaign(string? argsJson, string? toolResultJson)
    {
        CampaignDto? dto = null;
        if (!string.IsNullOrWhiteSpace(argsJson))
        {
            try { dto = JsonSerializer.Deserialize<CampaignDto>(argsJson, JsonOpts); }
            catch { /* ignore */ }
        }

        if ((dto is null || string.IsNullOrWhiteSpace(dto.Id)) && !string.IsNullOrWhiteSpace(toolResultJson))
        {
            try { dto = JsonSerializer.Deserialize<CampaignDto>(toolResultJson, JsonOpts); }
            catch { /* ignore */ }
        }

        if (string.IsNullOrWhiteSpace(dto?.Id)) return null;
        var status = dto.Status;
        if (string.IsNullOrWhiteSpace(status) && !string.IsNullOrWhiteSpace(toolResultJson))
            status = TryReadJsonStringProperty(toolResultJson, "status");
        if (string.IsNullOrWhiteSpace(status))
            status = CampaignStatusStrings.Draft;

        var events = dto.Events?.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).ToList() ?? [];
        var name = string.IsNullOrWhiteSpace(dto.Name) ? "Campaign" : dto.Name.Trim();
        return new CampaignAgentThreadCampaignRef(dto.Id!, status, name, events);
    }

    private static CampaignAgentThreadPatRef? TryParsePointAccountType(string? argsJson, string? toolResultJson)
    {
        PointAccountTypeDto? dto = null;
        if (!string.IsNullOrWhiteSpace(argsJson))
        {
            try { dto = JsonSerializer.Deserialize<PointAccountTypeDto>(argsJson, JsonOpts); }
            catch { /* ignore */ }
        }

        if ((dto is null || string.IsNullOrWhiteSpace(dto.Id)) && !string.IsNullOrWhiteSpace(toolResultJson))
        {
            try { dto = JsonSerializer.Deserialize<PointAccountTypeDto>(toolResultJson, JsonOpts); }
            catch { /* ignore */ }
        }

        if (string.IsNullOrWhiteSpace(dto?.Id)) return null;
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
        catch { /* ignore */ }

        return null;
    }

    private static bool IsUpsertCampaign(string? name) =>
        name != null && (string.Equals(name, "UpsertCampaign", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(name, "upsert_campaign", StringComparison.OrdinalIgnoreCase));

    private static bool IsUpsertPointAccountType(string? name) =>
        name != null && (string.Equals(name, "UpsertPointAccountType", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(name, "upsert_point_account_type", StringComparison.OrdinalIgnoreCase));

    private sealed record CallRef(long Sequence, string CallId, string ToolName, string? ArgsJson);
}
