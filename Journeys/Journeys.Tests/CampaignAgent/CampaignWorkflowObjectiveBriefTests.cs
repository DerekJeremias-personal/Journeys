using System.Text.Json;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;

namespace Journeys.Tests.CampaignAgent;

public class CampaignWorkflowObjectiveBriefTests
{
    [Fact]
    public void BuildDesignBriefFromObjective_EventDriven_SetsUserStatedBriefWithNoInsights()
    {
        var s = CampaignWorkflowState.CreateDefault("tenant-a", "u", "c");
        var toolResult = """
            {"captured":true,"objective":"Boost weekend grocery spend","audience":"Card members","mechanic":"Double points Sat-Sun","campaignClass":"event-driven","successCriteria":"Weekend txn lift"}
            """;

        var campaignClass = CampaignWorkflowArtifactBuilder.BuildDesignBriefFromObjective(s, toolResult);

        Assert.Equal("event-driven", campaignClass);
        Assert.False(string.IsNullOrEmpty(s.Artifacts.CampaignDesignBriefProposed));
        using var doc = JsonDocument.Parse(s.Artifacts.CampaignDesignBriefProposed!);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("tenant-a", root.GetProperty("tenantId").GetString());
        Assert.Equal("user-stated", root.GetProperty("source").GetString());
        Assert.Equal("Boost weekend grocery spend", root.GetProperty("objective").GetString());
        Assert.Equal(0, root.GetProperty("insights").GetArrayLength());
        Assert.Equal("event-driven", root.GetProperty("recommendedProgramShape").GetProperty("campaignClass").GetString());
    }

    [Fact]
    public void BuildDesignBriefFromObjective_MissingCampaignClass_DefaultsToEventDriven()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var campaignClass = CampaignWorkflowArtifactBuilder.BuildDesignBriefFromObjective(
            s, """{"objective":"Grow tier 2 adoption"}""");

        Assert.Equal("event-driven", campaignClass);
        using var doc = JsonDocument.Parse(s.Artifacts.CampaignDesignBriefProposed!);
        Assert.Equal("event-driven", doc.RootElement.GetProperty("recommendedProgramShape").GetProperty("campaignClass").GetString());
    }

    [Fact]
    public void BuildDesignBriefFromObjective_TagFirst_ReturnsTagFirstClass()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var campaignClass = CampaignWorkflowArtifactBuilder.BuildDesignBriefFromObjective(
            s, """{"objective":"Reward strategic accounts","campaignClass":"tag-first"}""");

        Assert.Equal("tag-first", campaignClass);
        using var doc = JsonDocument.Parse(s.Artifacts.CampaignDesignBriefProposed!);
        Assert.Equal("tag-first", doc.RootElement.GetProperty("recommendedProgramShape").GetProperty("campaignClass").GetString());
    }
}
