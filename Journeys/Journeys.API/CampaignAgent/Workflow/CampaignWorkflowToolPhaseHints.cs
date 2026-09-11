using Journeys.Core.Models;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// Enriches generic MCP "tool not found" errors with workflow-phase context.
/// </summary>
public static class CampaignWorkflowToolPhaseHints
{
    public const string PhaseBlockedSuffix =
        "[workflow_phase_blocked] Mutators unlock after event model resolution. This is not a missing MCP tool.";

    public static string EnrichToolResultJson(CampaignWorkflowPhase phase, string? toolName, string resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return resultJson;

        if (!LooksLikeToolNotFound(resultJson))
            return resultJson;

        var hint = BuildHint(phase, toolName);
        return hint is null ? resultJson : $"{StripJsonStringQuotes(resultJson)} {hint}";
    }

    private static string? BuildHint(CampaignWorkflowPhase phase, string? toolName)
    {
        if (phase == CampaignWorkflowPhase.PointAccountTypes
            && IsUpsertCampaignTool(toolName))
        {
            return "Workflow hint: upsert_campaign is only available in CampaignJourney phase. "
                   + "Register each PAT you will reference via GetPointAccountType (or UpsertPointAccountType for new PATs) "
                   + "until PointAccountManifest in WORKFLOW ARTIFACTS is populated, then author the journey.";
        }

        if (phase == CampaignWorkflowPhase.EventModels
            && IsEventModelsPhaseBlockedTool(toolName))
        {
            var baseHint = GetEventModelsPhaseBlockedBaseHint(toolName);
            return baseHint is null
                ? PhaseBlockedSuffix
                : $"{baseHint} {PhaseBlockedSuffix}";
        }

        return null;
    }

    private static string? GetEventModelsPhaseBlockedBaseHint(string? toolName)
    {
        if (IsUpsertCampaignTool(toolName))
        {
            return "Workflow hint: upsert_campaign is only available in CampaignSetup (shell) and CampaignJourney phases. "
                   + "Complete event model resolution first.";
        }

        if (IsUpsertPatTool(toolName))
        {
            return "Workflow hint: upsert_point_account_type is only available after event model resolution. "
                   + "Complete event model resolution first.";
        }

        if (IsSaveModelTool(toolName))
        {
            return "Workflow hint: save_model may be unavailable while the event model gate is closed. "
                   + "Complete event model resolution first.";
        }

        return null;
    }

    private static bool IsEventModelsPhaseBlockedTool(string? toolName) =>
        IsUpsertCampaignTool(toolName)
        || IsUpsertPatTool(toolName)
        || IsSaveModelTool(toolName);

    private static bool IsUpsertCampaignTool(string? name) =>
        string.Equals(name, "UpsertCampaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "upsert_campaign", StringComparison.OrdinalIgnoreCase);

    private static bool IsUpsertPatTool(string? name) =>
        string.Equals(name, "UpsertPointAccountType", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "upsert_point_account_type", StringComparison.OrdinalIgnoreCase);

    private static bool IsSaveModelTool(string? name) =>
        string.Equals(name, "SaveModel", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "save_model", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeToolNotFound(string json)
    {
        if (json.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return true;

        var unquoted = StripJsonStringQuotes(json);
        return unquoted.Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripJsonStringQuotes(string json)
    {
        var t = json.Trim();
        if (t.Length >= 2 && t.StartsWith('"') && t.EndsWith('"'))
            return t[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal);

        return t;
    }
}
