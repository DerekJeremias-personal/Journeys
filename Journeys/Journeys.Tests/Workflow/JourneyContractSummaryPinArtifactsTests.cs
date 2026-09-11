using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class JourneyContractSummaryPinArtifactsTests
{
    [Fact]
    public void RefreshEpisodeState_activates_when_manifest_ready_and_zero_rules()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.PointAccountManifest =
            """{"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"Spend","ledgerType":"Spendable"}]}""";

        JourneyContractSummaryPinArtifacts.RefreshEpisodeState(state);

        Assert.True(state.Artifacts.JourneyContractSummaryPinActive);
    }

    [Fact]
    public void OnContractSummaryFetched_sets_matrix_version_and_critical_rows()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.PointAccountManifest =
            """{"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"Spend","ledgerType":"Spendable"}]}""";

        var json = """
            {
              "matrixVersion": "2026-06-20",
              "criticalRows": [
                { "id": "simple_rule_three_part" },
                { "id": "outcome_affected_pat_ids" }
              ]
            }
            """;

        JourneyContractSummaryPinArtifacts.OnContractSummaryFetched(state, json);

        Assert.True(state.Artifacts.JourneyContractSummaryFetchedThisEpisode);
        Assert.Equal("2026-06-20", state.Artifacts.JourneyContractSummaryMatrixVersion);
        Assert.Contains("simple_rule_three_part", state.Artifacts.JourneyContractCriticalRowIds);
    }

    [Fact]
    public void Clear_clears_pin_latches_when_journey_persisted()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.JourneyContractSummaryPinActive = true;
        state.Artifacts.JourneyContractSummaryFetchedThisEpisode = true;
        state.Artifacts.JourneyContractSummaryMatrixVersion = "2026-06-20";
        state.Artifacts.CreationSnapshot = """
            {
              "schemaVersion": 1,
              "creationComplete": true,
              "campaignId": "camp-1",
              "journeyRuleSetCount": 2
            }
            """;

        JourneyPatternArtifacts.ClearIfJourneyPersisted(state);

        Assert.False(state.Artifacts.JourneyContractSummaryPinActive);
        Assert.False(state.Artifacts.JourneyContractSummaryFetchedThisEpisode);
        Assert.Null(state.Artifacts.JourneyContractSummaryMatrixVersion);
    }
}
