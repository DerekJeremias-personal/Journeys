using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Microsoft.Extensions.AI;

namespace Journeys.Tests.Workflow;

public class CampaignWorkflowToolFilterSelectionTests
{
    private static List<AITool> Tools() => new()
    {
        Tool("GetModel"),
        Tool("GetAllModels"),
        Tool("ListModels"),
        Tool("SaveModel"),
        Tool("UpsertCampaign")
    };

    [Fact]
    public void EventModelSelection_gate_exposes_readonly_backend_tools_and_hides_SaveModel()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.EventModels;
        state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.EventModelSelection;

        var filtered = CampaignWorkflowToolFilter.Apply(Tools(), state).Select(t => t.Name).ToList();

        Assert.Contains("GetModel", filtered);
        Assert.Contains("GetAllModels", filtered);
        Assert.Contains("ListModels", filtered);
        Assert.DoesNotContain("SaveModel", filtered);
        Assert.DoesNotContain("UpsertCampaign", filtered);
    }

    [Fact]
    public void PostBrief_exposes_SaveModel()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";

        var filtered = CampaignWorkflowToolFilter.Apply(Tools(), state).Select(t => t.Name).ToList();

        Assert.Contains("SaveModel", filtered);
    }

    private static AITool Tool(string name) => new TestTool(name);

    private sealed class TestTool(string name) : AITool
    {
        public override string Name => name;
        public override string Description => name;
    }
}
