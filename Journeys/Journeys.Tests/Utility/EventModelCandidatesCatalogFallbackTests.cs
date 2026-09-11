using System.Text.Json;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventModelCandidatesCatalogFallbackTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void TryApplyFromCatalogToolResult_replaces_fetch_failed_with_ranked_candidates()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet { FetchFailed = true });

        var digest = ModelCatalogDigester.Digest(BuildModelsCatalog(), sourceTag: "eventable");

        Assert.True(EventModelCandidatesCatalogFallback.TryApplyFromCatalogToolResult(state, digest.Json));

        var set = EventModelCandidatesArtifact.Read(state);
        Assert.NotNull(set);
        Assert.False(set!.FetchFailed);
        Assert.NotEmpty(set.Candidates);
        Assert.Equal("order-1", set.RecommendedDefaultId);
        Assert.Equal(CampaignWorkflowApprovalKind.EventModelSelection, state.Artifacts.AwaitingApproval);
    }

    [Fact]
    public void ApplyToolResults_list_models_after_fetch_failed_populates_candidates()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet { FetchFailed = true });

        var digest = ModelCatalogDigester.Digest(BuildModelsCatalog(), sourceTag: "eventable");
        CampaignWorkflowStepManager.ApplyToolResults(state, [("list_models", digest.Json)]);

        var set = EventModelCandidatesArtifact.Read(state);
        Assert.NotNull(set);
        Assert.False(set!.FetchFailed);
        Assert.NotEmpty(set.Candidates);
    }

    [Fact]
    public void ShouldAttemptMcpCatalogFetch_true_when_fetch_failed()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = "{}";
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet { FetchFailed = true });

        Assert.True(EventModelCandidatesCatalogFallback.ShouldAttemptMcpCatalogFetch(state));
        Assert.True(CampaignWorkflowEngine.ShouldRunEventModelPrep(state));
    }

    [Fact]
    public void ShouldAttemptMcpCatalogFetch_false_when_candidates_present()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = "{}";
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet
        {
            Candidates = [new EventModelCandidate { EventModelId = "o1", Name = "Order" }]
        });

        Assert.False(EventModelCandidatesCatalogFallback.ShouldAttemptMcpCatalogFetch(state));
    }

    private static string BuildModelsCatalog()
    {
        var order = """
            {
              "id": "order-1",
              "name": "order",
              "modelType": "loyalty",
              "tag": "eventable",
              "isContainer": false,
              "attributes": [
                { "symbol": "ordertotal", "type": "Primitive", "dataType": "Number" }
              ]
            }
            """;
        var review = """
            {
              "id": "r-1",
              "name": "Review",
              "modelType": "loyalty",
              "tag": "eventable",
              "isContainer": false,
              "attributes": []
            }
            """;
        return $$"""{ "models": [ {{order}}, {{review}} ] }""";
    }
}
