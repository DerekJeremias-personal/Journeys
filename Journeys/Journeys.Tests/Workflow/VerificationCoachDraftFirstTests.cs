using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class VerificationCoachDraftFirstTests
{
    [Fact]
    public void GetCoachHint_in_CampaignJourney_with_draft_creation_complete_contains_anti_lecture()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Phase = CampaignWorkflowPhase.CampaignJourney;
        state.Artifacts.CampaignShellRef = """{"campaignId":"c1"}""";
        state.Artifacts.PointAccountManifest = """{"items":[{"id":"pat-1","displayLabel":"Spend","ledgerType":"Spendable"}]}""";
        CreationSnapshotArtifact.MergePatsFromWorkflowManifest(state);
        CreationSnapshotArtifact.MergeFromJourneyUpsert(state, "c1", CampaignStatusStrings.Draft, 2, 2);

        var hint = VerificationCoach.GetCoachHint(state);

        Assert.NotNull(hint);
        Assert.Contains("Do NOT tell", hint!, StringComparison.Ordinal);
        Assert.Contains("campaignId", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShouldApplyDraftFirstCoach_false_when_verification_complete()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Phase = CampaignWorkflowPhase.Verification;
        CreationSnapshotArtifact.MergeFromJourneyUpsert(state, "c1", CampaignStatusStrings.Draft, 2, 2);
        state.Artifacts.VerificationRecord = """{"appliedRuleSetIds":["rs1"]}""";
        state.Artifacts.VerificationProcessEventCampaignApplied = true;
        state.Artifacts.VerificationProcessEventRulesApplied = true;

        Assert.False(VerificationCoach.ShouldApplyDraftFirstCoach(state));
    }
}
