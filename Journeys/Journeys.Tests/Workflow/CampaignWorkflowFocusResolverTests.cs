using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class CampaignWorkflowFocusResolverTests
{
    [Fact]
    public void Default_focus_DataAnalysis_when_no_brief()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var focus = CampaignWorkflowFocusResolver.Resolve(s, userMessage: null);
        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, focus);
    }

    [Fact]
    public void User_intent_journey_overrides_coach_default()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        var focus = CampaignWorkflowFocusResolver.Resolve(s, "let's author the journey rules");
        Assert.Equal(CampaignWorkflowPhase.CampaignBuild, focus);
    }

    [Fact]
    public void Coach_default_EventModels_when_brief_done_events_incomplete()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        var focus = CampaignWorkflowFocusResolver.Resolve(s, userMessage: "ok");
        Assert.Equal(CampaignWorkflowPhase.EventModels, focus);
    }

    [Fact]
    public void CampaignFixPhrases_focus_build()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        var focus = CampaignWorkflowFocusResolver.Resolve(s, WorkflowUserPhraseCatalog.CampaignFixPhrases[0]);
        Assert.Equal(CampaignWorkflowPhase.CampaignBuild, focus);
    }

    [Fact]
    public void Coach_default_CampaignBuild_when_shell_exists_but_creation_incomplete()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        s.Artifacts.CampaignDesignBriefApproved = s.Artifacts.CampaignDesignBriefProposed;
        s.ModelGatePassed = true;
        s.UserSkippedEventModels = true;
        s.CampaignKind = CampaignWorkflowKind.TagFirst;
        s.Artifacts.CampaignShellRef = """{"campaignId":"camp-1"}""";
        s.Artifacts.PointAccountManifest = """{"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"P","role":"spendable"}]}""";
        CreationSnapshotArtifact.MergeShellOnly(s, "camp-1", "draft");
        CreationSnapshotArtifact.MergePatFromUpsertResult(s, """{"Id":"pat-1","Name":"P","LedgerType":"Spendable"}""");

        var focus = CampaignWorkflowFocusResolver.CoachDefaultFocus(s);

        Assert.Equal(CampaignWorkflowPhase.CampaignBuild, focus);
    }

    [Fact]
    public void Coach_default_CampaignBuild_when_gate_open_patRequired()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"Bronze Silver Gold tiers"}""";
        s.Artifacts.CampaignDesignBriefApproved = s.Artifacts.CampaignDesignBriefProposed;
        s.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"a6edbbc5-bf43-4c57-b2f1-e015b9efaf03","eventModelName":"Order","isProcessEventEligible":true}]""";
        s.ModelGatePassed = false;

        var focus = CampaignWorkflowFocusResolver.CoachDefaultFocus(s);

        Assert.Equal(CampaignWorkflowPhase.CampaignBuild, focus);
    }

    [Fact]
    public void Coach_default_EventModels_when_gate_closed_events_incomplete()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";

        var focus = CampaignWorkflowFocusResolver.CoachDefaultFocus(s);

        Assert.Equal(CampaignWorkflowPhase.EventModels, focus);
    }
}
