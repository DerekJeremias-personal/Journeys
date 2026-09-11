using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.Core.Workflow;

/// <summary>
/// Blocks upsert_campaign with journey payload when validate has not proven ruleSetCount &gt; 0.
/// </summary>
public static class JourneyDeliveryGate
{
    public static bool ShouldBlockJourneyUpsert(CampaignWorkflowState state, string? campaignJson)
    {
        if (string.IsNullOrWhiteSpace(campaignJson))
            return false;
        if (CreationSnapshotArtifact.Read(state) is { CreationComplete: true })
            return false;
        if (PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count == 0)
            return false;
        if (!ContainsJourneyAuthoringPayload(campaignJson))
            return false;

        var lastValidate = state.Artifacts.LastValidateRuleSetCount ?? 0;
        var persisted = CreationSnapshotArtifact.Read(state)?.JourneyRuleSetCount ?? 0;
        return lastValidate <= 0 && persisted <= 0;
    }

    public static string BuildBlockedToolResult() =>
        """
        {"errors":{"buildGate":["[violation=BUILD_GATE_JOURNEY_RULESETS_REQUIRED] validate_campaign reported 0 rule sets (or no validate this session). Author journey.rules[] / tier children with RuleSet wrappers, re-validate until RuleSetCount > 0, then upsert."]},"_agentRemediation":"Do not upsert journey JSON until get_campaign_assistant_context or validate summary shows ruleSetCount > 0."}
        """;

    public static void ApplyBlock(CampaignWorkflowState state) =>
        state.Artifacts.LastToolRemediationSummary = "BUILD_GATE_JOURNEY_RULESETS_REQUIRED";

    internal static bool ContainsJourneyAuthoringPayload(string json) =>
        CampaignBuildGate.ContainsJourneyContentForGate(json);

    public static int WorkflowPatCount(CampaignWorkflowState state) =>
        PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count;
}
