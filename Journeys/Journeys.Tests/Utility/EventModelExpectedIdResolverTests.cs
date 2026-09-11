using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventModelExpectedIdResolverTests
{
    [Fact]
    public void Resolve_prefers_selected_over_recommended()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.SelectedEventModelId = "selected-1";
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet
        {
            RecommendedDefaultId = "rec-1",
            Candidates = [new EventModelCandidate { EventModelId = "rec-1" }]
        });

        Assert.Equal("selected-1", EventModelExpectedIdResolver.Resolve(state));
    }

    [Fact]
    public void Resolve_uses_planned_ids_from_brief()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefApproved =
            """{"version":1,"plannedEventModelIds":["planned-1","planned-2"]}""";

        Assert.Equal("planned-1", EventModelExpectedIdResolver.Resolve(state));
    }

    [Fact]
    public void Resolve_falls_back_to_recommended_default()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet
        {
            RecommendedDefaultId = "rec-1",
            Candidates = [new EventModelCandidate { EventModelId = "rec-1" }]
        });

        Assert.Equal("rec-1", EventModelExpectedIdResolver.Resolve(state));
    }
}
