using CampaignContextAudit.Analysis;
using CampaignContextAudit.Models;
using Xunit;

namespace CampaignContextAudit.Tests;

public class CompetencyDetectorsTests
{
    private static ToolEvent Ev(long seq, string name, int chars = 0) => new(seq, name, $"c{seq}", chars);

    [Fact]
    public void Flags_consecutive_redundant_discovery_calls()
    {
        var timeline = new[] { Ev(2, "get_all_models"), Ev(4, "list_models") };
        var findings = CompetencyDetectors.RedundantDiscovery(timeline);
        Assert.Single(findings);
        Assert.Contains("2", findings[0].CitedSequences);
        Assert.Contains("4", findings[0].CitedSequences);
    }

    [Fact]
    public void Flags_rediscovery_after_model_creation()
    {
        var timeline = new[] { Ev(14, "save_model"), Ev(16, "save_model"), Ev(30, "get_all_models") };
        var findings = CompetencyDetectors.RediscoveryAfterCreation(timeline);
        Assert.Single(findings);
        Assert.Contains("30", findings[0].CitedSequences);
    }

    [Fact]
    public void Ranks_largest_tool_results_as_bloat()
    {
        var timeline = new[] { Ev(10, "get_all_models", 50000), Ev(12, "list_models", 48000), Ev(18, "save_model", 200) };
        var findings = CompetencyDetectors.TopBloat(timeline, top: 2);
        Assert.Equal(2, findings.Count);
        Assert.Contains("10", findings[0].CitedSequences);
    }

    [Fact]
    public void WorkflowStateDrift_flags_when_models_created_but_gate_not_passed()
    {
        var workflowRows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 99, Role = "workflow", WorkflowModelGatePassed = false }
        };
        var timeline = new[] { Ev(14, "save_model") };
        var findings = CompetencyDetectors.WorkflowStateDrift(workflowRows, timeline);
        Assert.Single(findings);
        Assert.Equal("WORKFLOW_STATE_DRIFT", findings[0].Code);
    }

    [Fact]
    public void WorkflowStateDrift_no_finding_when_gate_passed_and_no_campaign_drift()
    {
        var workflowRows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 99, Role = "workflow", WorkflowModelGatePassed = true }
        };
        var timeline = new[] { Ev(14, "save_model") };
        var findings = CompetencyDetectors.WorkflowStateDrift(workflowRows, timeline);
        Assert.Empty(findings);
    }

    [Fact]
    public void WorkflowStateDrift_flags_when_upsert_campaign_used_but_creation_incomplete()
    {
        var workflowRows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 99, Role = "workflow", WorkflowModelGatePassed = true }
        };
        var timeline = new[] { Ev(24, "upsert_campaign") };
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "Done",
            PatManifestCount: 3,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: true,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: false,
            FetchFailed: false);

        var findings = CompetencyDetectors.WorkflowStateDrift(workflowRows, timeline, snapshot);

        Assert.Contains(findings, f => f.Code == "WORKFLOW_STATE_DRIFT");
    }
}
