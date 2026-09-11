using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class JourneyCoachTests
{
    [Fact]
    public void ApplyUpsertOutcome_sets_remediation_when_journey_phase_and_zero_rule_sets()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.CampaignJourney;
        state.Artifacts.CampaignShellRef = """{"campaignId":"camp-1"}""";
        CreationSnapshotArtifact.MergeShellOnly(state, "camp-1", "draft");

        const string ack = """
        {
          "note": "Full campaign definition suppressed for context efficiency; call get_campaign_assistant_context for the persisted detail.",
          "campaign": { "Id": "camp-1", "Status": "draft" },
          "journey": { "nodes": [] }
        }
        """;

        JourneyCoach.ApplyUpsertOutcome(state, ack, success: true);

        Assert.Contains("journey.ruleSetCount is 0", state.Artifacts.LastToolRemediationSummary!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ruleJsonElement", state.Artifacts.LastToolRemediationSummary!, StringComparison.Ordinal);
    }

    [Fact]
    public void GetCoachHint_returns_message_when_snapshot_has_zero_rule_sets()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.CampaignJourney;
        state.Artifacts.CampaignShellRef = """{"campaignId":"camp-1"}""";
        state.Artifacts.JourneyDigestApproved = """{"ruleSetCount":0}""";
        CreationSnapshotArtifact.MergeFromJourneyUpsert(state, "camp-1", "draft", ruleSetCount: 0, outcomeCount: 0);

        var hint = JourneyCoach.GetCoachHint(state);

        Assert.NotNull(hint);
        Assert.Contains("ruleJsonElement", hint!, StringComparison.Ordinal);
    }

    [Fact]
    public void GetCoachHint_returns_in_Done_when_creation_incomplete()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.Done;
        state.Artifacts.CampaignShellRef = """{"campaignId":"camp-1"}""";
        CreationSnapshotArtifact.MergeFromJourneyUpsert(state, "camp-1", "draft", ruleSetCount: 0, outcomeCount: 0);

        var hint = JourneyCoach.GetCoachHint(state);

        Assert.NotNull(hint);
        Assert.Contains("journey.ruleSetCount is 0", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoachHint_returns_in_CampaignBuild_when_creation_incomplete()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.CampaignBuild;
        state.Artifacts.CampaignShellRef = """{"campaignId":"camp-1"}""";
        state.Artifacts.PointAccountManifest =
            """{"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"Spend","ledgerType":"Spendable"}]}""";
        CreationSnapshotArtifact.MergeFromJourneyUpsert(state, "camp-1", "draft", ruleSetCount: 0, outcomeCount: 0);

        var hint = JourneyCoach.GetCoachHint(state);

        Assert.NotNull(hint);
        Assert.Contains("ruleJsonElement", hint!, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyAssistantContextOutcome_merges_rule_set_count_into_snapshot()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignShellRef = """{"campaignId":"camp-1"}""";

        const string ctx = """
        {
          "campaignId": "camp-1",
          "status": "draft",
          "journey": { "schemaVersion": 1, "campaignId": "camp-1", "ruleSetCount": 2, "journeyNodeCount": 3 }
        }
        """;

        JourneyCoach.ApplyAssistantContextOutcome(state, ctx, success: true);

        var snap = CreationSnapshotArtifact.Read(state);
        Assert.NotNull(snap);
        Assert.Equal(2, snap!.JourneyRuleSetCount);
        Assert.Contains("ruleSetCount", state.Artifacts.JourneyDigestProposed!, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyAssistantContextOutcome_clears_stale_empty_journey_remediation_when_rule_sets_persisted()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignShellRef = """{"campaignId":"camp-1"}""";
        state.Artifacts.LastToolRemediationSummary =
            "Campaign shell exists but journey.ruleSetCount is 0 — journey is not persisted.";
        CreationSnapshotArtifact.MergeFromJourneyUpsert(state, "camp-1", "draft", ruleSetCount: 1, outcomeCount: 0);

        const string ctx = """
        {
          "campaignId": "camp-1",
          "status": "draft",
          "journey": { "schemaVersion": 1, "campaignId": "camp-1", "ruleSetCount": 1, "journeyNodeCount": 1 }
        }
        """;

        JourneyCoach.ApplyAssistantContextOutcome(state, ctx, success: true);

        Assert.Null(state.Artifacts.LastToolRemediationSummary);
    }

    [Fact]
    public void ApplyAssistantContextOutcome_sets_post_journey_handoff_when_rule_sets_first_persisted()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignShellRef = """{"campaignId":"camp-1"}""";
        CreationSnapshotArtifact.MergeFromJourneyUpsert(state, "camp-1", CampaignStatusStrings.Draft, 0, 0);

        const string ctx = """
        {
          "campaignId": "camp-1",
          "status": "draft",
          "journey": { "schemaVersion": 1, "campaignId": "camp-1", "ruleSetCount": 2, "journeyNodeCount": 3 }
        }
        """;

        JourneyCoach.ApplyAssistantContextOutcome(state, ctx, success: true);

        Assert.True(state.Artifacts.PostJourneyVerifyHandoffShown);
        Assert.Contains("draft verification", state.Artifacts.LastToolRemediationSummary!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not promote", state.Artifacts.LastToolRemediationSummary!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyInvocationFailure_sets_non_transient_remediation()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");

        JourneyCoach.ApplyInvocationFailure(state, "upsert_campaign");

        Assert.Contains("MCP invocation error", state.Artifacts.LastToolRemediationSummary!, StringComparison.Ordinal);
        Assert.Contains("transient connectivity", state.Artifacts.LastToolRemediationSummary!, StringComparison.OrdinalIgnoreCase);
        Assert.True(CampaignValidationCoach.Read(state)!.UpsertFailedSinceValidate);
    }
}
