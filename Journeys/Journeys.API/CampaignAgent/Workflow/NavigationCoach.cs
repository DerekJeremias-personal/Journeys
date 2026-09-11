using System.Text.Json;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// Coaches navigation/entry criteria when process_event applies a campaign but no rule sets fire.
/// </summary>
public static class NavigationCoach
{
    public static void ApplyProcessEventOutcome(CampaignWorkflowState state, string resultJson, bool success)
    {
        if (!success)
        {
            state.Artifacts.VerificationProcessEventCampaignApplied = false;
            state.Artifacts.VerificationProcessEventRulesApplied = false;
            return;
        }

        var outcome = TryParseProcessEventOutcome(resultJson, CreationSnapshotArtifact.Read(state)?.CampaignId);
        state.Artifacts.VerificationProcessEventCampaignApplied = outcome.CampaignApplied;
        state.Artifacts.VerificationProcessEventRulesApplied = outcome.RulesApplied;

        if (outcome.CampaignApplied && !outcome.RulesApplied)
            state.Artifacts.LastToolRemediationSummary = CampaignAgentGuidanceText.ProcessEventEmptyAppliedRuleSetsRemediation;
    }

    public static string? GetCoachHint(CampaignWorkflowState state)
    {
        if (!state.Artifacts.VerificationProcessEventCampaignApplied
            || state.Artifacts.VerificationProcessEventRulesApplied)
            return null;

        if (!CreationSnapshotArtifact.IsCreationComplete(state)
            && state.Phase != CampaignWorkflowPhase.Verification
            && !state.Artifacts.VerificationUserTestIntentThisTurn)
            return null;

        return "Coach: " + CampaignAgentGuidanceText.ProcessEventEmptyAppliedRuleSetsRemediation
               + " Do not claim verification complete or production-ready until AppliedRuleSetIds is non-empty.";
    }

    internal static ProcessEventOutcome TryParseProcessEventOutcome(string resultJson, string? expectedCampaignId)
    {
        resultJson = ToolResultJsonNormalizer.Unwrap(resultJson) ?? resultJson;
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            var appliedCampaigns = ReadStringArray(root, "AppliedCampaigns", "appliedCampaigns");
            var appliedRuleSets = ReadStringArray(root, "AppliedRuleSetIds", "appliedRuleSetIds");

            var campaignApplied = !string.IsNullOrWhiteSpace(expectedCampaignId)
                && appliedCampaigns.Any(id => string.Equals(id, expectedCampaignId, StringComparison.OrdinalIgnoreCase));
            if (!campaignApplied && appliedCampaigns.Count > 0 && string.IsNullOrWhiteSpace(expectedCampaignId))
                campaignApplied = true;

            return new ProcessEventOutcome(campaignApplied, appliedRuleSets.Count > 0);
        }
        catch (JsonException)
        {
            return new ProcessEventOutcome(false, false);
        }
    }

    private static List<string> ReadStringArray(JsonElement root, string pascal, string camel)
    {
        var list = new List<string>();
        if (!root.TryGetProperty(pascal, out var el) && !root.TryGetProperty(camel, out el))
            return list;
        if (el.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                list.Add(item.GetString()!);
        }

        return list;
    }

    internal readonly record struct ProcessEventOutcome(bool CampaignApplied, bool RulesApplied);
}
