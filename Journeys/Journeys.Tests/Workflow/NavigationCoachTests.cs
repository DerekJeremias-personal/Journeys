using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class NavigationCoachTests
{
    private const string ProcessEventNoRules = """
    {
      "AppliedCampaigns": ["camp-1"],
      "AppliedRuleSetIds": [],
      "SpendablePointsAwarded": null
    }
    """;

    private const string ProcessEventWithRules = """
    {
      "AppliedCampaigns": ["camp-1"],
      "AppliedRuleSetIds": ["rs-bronze"],
      "SpendablePointsAwarded": 1.0
    }
    """;

    [Fact]
    public void ApplyProcessEventOutcome_sets_flags_when_campaign_applies_without_rules()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        CreationSnapshotArtifact.MergeFromJourneyUpsert(state, "camp-1", "live", ruleSetCount: 3, outcomeCount: 3);

        NavigationCoach.ApplyProcessEventOutcome(state, ProcessEventNoRules, success: true);

        Assert.True(state.Artifacts.VerificationProcessEventCampaignApplied);
        Assert.False(state.Artifacts.VerificationProcessEventRulesApplied);
        Assert.Contains("AppliedRuleSetIds is empty", state.Artifacts.LastToolRemediationSummary!, StringComparison.Ordinal);
        Assert.Contains("PointBalanceProvider", state.Artifacts.LastToolRemediationSummary!, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyProcessEventOutcome_clears_remediation_when_rules_apply()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        CreationSnapshotArtifact.MergeFromJourneyUpsert(state, "camp-1", "live", ruleSetCount: 3, outcomeCount: 3);
        state.Artifacts.LastToolRemediationSummary = "prior";

        NavigationCoach.ApplyProcessEventOutcome(state, ProcessEventWithRules, success: true);

        Assert.True(state.Artifacts.VerificationProcessEventRulesApplied);
    }

    [Fact]
    public void GetCoachHint_returns_navigation_guidance_when_rules_missing()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.Verification;
        CreationSnapshotArtifact.MergeFromJourneyUpsert(state, "camp-1", "live", ruleSetCount: 3, outcomeCount: 3);
        state.Artifacts.VerificationProcessEventCampaignApplied = true;
        state.Artifacts.VerificationProcessEventRulesApplied = false;

        var hint = NavigationCoach.GetCoachHint(state);

        Assert.NotNull(hint);
        Assert.Contains("Entry", hint!, StringComparison.Ordinal);
        Assert.Contains("production-ready", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseProcessEventOutcome_matches_expected_campaign_id()
    {
        var outcome = NavigationCoach.TryParseProcessEventOutcome(ProcessEventNoRules, "camp-1");
        Assert.True(outcome.CampaignApplied);
        Assert.False(outcome.RulesApplied);
    }
}
