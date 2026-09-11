using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

internal static class CampaignWorkflowArtifactBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void AppendWarehouseInsight(CampaignWorkflowState state, string toolName, string resultJson)
    {
        var brief = ParseBrief(state.Artifacts.CampaignDesignBriefProposed) ?? NewBrief(state);
        brief.Insights ??= new List<WarehouseInsightEntry>();
        brief.Insights.Add(new WarehouseInsightEntry
        {
            Source = toolName,
            Finding = Truncate(resultJson, 500)
        });
        state.Artifacts.CampaignDesignBriefProposed = SerializeBrief(brief);
    }

    public static string BuildDesignBrief(CampaignWorkflowState state)
    {
        var brief = ParseBrief(state.Artifacts.CampaignDesignBriefProposed) ?? NewBrief(state);
        brief.RecommendedProgramShape ??= new ProgramShapeRecommendation
        {
            CampaignClass = state.CampaignKind == CampaignWorkflowKind.TagFirst ? "tag-first" : "event-driven"
        };
        return SerializeBrief(brief);
    }

    /// <summary>
    /// Builds a user-stated design brief from a <c>ProposeCampaignDesignBrief</c> tool result
    /// (warehouse tools disabled). Returns the resolved campaign class ("event-driven" | "tag-first").
    /// </summary>
    public static string BuildDesignBriefFromObjective(CampaignWorkflowState state, string toolResultJson)
    {
        string? objective = null, audience = null, mechanic = null, successCriteria = null, campaignClass = null;
        try
        {
            using var doc = JsonDocument.Parse(toolResultJson);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                objective = ReadString(root, "objective");
                audience = ReadString(root, "audience");
                mechanic = ReadString(root, "mechanic");
                successCriteria = ReadString(root, "successCriteria");
                campaignClass = ReadString(root, "campaignClass");
            }
        }
        catch (JsonException)
        {
            // Non-JSON tool result: keep nulls; brief still records source + default class.
        }

        var resolvedClass = string.Equals(campaignClass?.Trim(), "tag-first", StringComparison.OrdinalIgnoreCase)
            ? "tag-first"
            : "event-driven";

        var brief = ParseBrief(state.Artifacts.CampaignDesignBriefProposed) ?? NewBrief(state);
        brief.Source = "user-stated";
        brief.Insights ??= new List<WarehouseInsightEntry>();
        if (!string.IsNullOrWhiteSpace(objective)) brief.Objective = objective;
        if (!string.IsNullOrWhiteSpace(audience)) brief.Audience = audience;
        if (!string.IsNullOrWhiteSpace(mechanic)) brief.Mechanic = mechanic;
        if (!string.IsNullOrWhiteSpace(successCriteria)) brief.SuccessCriteria = successCriteria;
        brief.RecommendedProgramShape = new ProgramShapeRecommendation { CampaignClass = resolvedClass };

        state.Artifacts.CampaignDesignBriefProposed = SerializeBrief(brief);
        return resolvedClass;
    }

    private static string? ReadString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    public static void AppendPointAccountType(CampaignWorkflowState state, string patJson) =>
        PointAccountManifestBuilder.AppendFromPatResult(state, patJson);

    public static string BuildJourneyDigest(string upsertCampaignJson) =>
        JsonSerializer.Serialize(new JourneyDigestDto
        {
            Version = 1,
            Summary = "Campaign journey saved via UpsertCampaign.",
            PayloadPreviewChars = Math.Min(upsertCampaignJson?.Length ?? 0, 400)
        }, JsonOpts);

    private static CampaignDesignBriefDto NewBrief(CampaignWorkflowState state) =>
        new()
        {
            Version = 1,
            TenantId = state.TenantId,
            Insights = []
        };

    private static CampaignDesignBriefDto? ParseBrief(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<CampaignDesignBriefDto>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static string SerializeBrief(CampaignDesignBriefDto brief) =>
        JsonSerializer.Serialize(brief, JsonOpts);

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "…";

    private sealed class CampaignDesignBriefDto
    {
        public int Version { get; set; }
        public string? TenantId { get; set; }
        public string? Source { get; set; }
        public string? Objective { get; set; }
        public string? Audience { get; set; }
        public string? Mechanic { get; set; }
        public string? SuccessCriteria { get; set; }
        public List<WarehouseInsightEntry>? Insights { get; set; }
        public ProgramShapeRecommendation? RecommendedProgramShape { get; set; }
        public List<string>? PlannedEventModelIds { get; set; }
    }

    private sealed class WarehouseInsightEntry
    {
        public string? Source { get; set; }
        public string? Finding { get; set; }
    }

    private sealed class ProgramShapeRecommendation
    {
        public string? CampaignClass { get; set; }
    }

    private sealed class JourneyDigestDto
    {
        public int Version { get; set; }
        public string? Summary { get; set; }
        public int PayloadPreviewChars { get; set; }
    }
}
