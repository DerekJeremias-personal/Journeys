using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class CampaignWorkflowToolPhaseHintsTests
{
    [Fact]
    public void EnrichToolResultJson_PointAccountTypes_upsert_campaign_adds_hint()
    {
        var raw = "\"Error: Requested function \\\"upsert_campaign\\\" not found.\"";
        var enriched = CampaignWorkflowToolPhaseHints.EnrichToolResultJson(
            CampaignWorkflowPhase.PointAccountTypes,
            "upsert_campaign",
            raw);

        Assert.Contains("CampaignJourney", enriched);
        Assert.Contains("PointAccountManifest", enriched);
    }

    [Fact]
    public void EnrichToolResultJson_EventModels_upsert_campaign_adds_phase_blocked_tag()
    {
        var raw = "\"Error: Requested function \\\"upsert_campaign\\\" not found.\"";
        var enriched = CampaignWorkflowToolPhaseHints.EnrichToolResultJson(
            CampaignWorkflowPhase.EventModels,
            "upsert_campaign",
            raw);

        Assert.Contains("[workflow_phase_blocked]", enriched);
        Assert.Contains("not a missing MCP tool", enriched);
    }

    [Fact]
    public void EnrichToolResultJson_unrelated_error_unchanged()
    {
        var raw = """{"error":"validation failed"}""";
        var enriched = CampaignWorkflowToolPhaseHints.EnrichToolResultJson(
            CampaignWorkflowPhase.CampaignJourney,
            "upsert_campaign",
            raw);

        Assert.Equal(raw, enriched);
    }
}
