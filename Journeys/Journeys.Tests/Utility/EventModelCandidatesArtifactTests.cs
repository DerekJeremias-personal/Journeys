using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventModelCandidatesArtifactTests
{
    [Fact]
    public void Write_then_Read_round_trips_the_set()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var set = new EventModelCandidateSet
        {
            RecommendedDefaultId = "o1",
            Candidates = { new EventModelCandidate { EventModelId = "o1", Name = "Order", IsStrongMatch = true } }
        };

        EventModelCandidatesArtifact.Write(state, set);

        Assert.True(EventModelCandidatesArtifact.HasComputed(state));
        var read = EventModelCandidatesArtifact.Read(state);
        Assert.NotNull(read);
        Assert.Equal("o1", read!.RecommendedDefaultId);
        Assert.Single(read.Candidates);
        Assert.Equal("Order", read.Candidates[0].Name);
    }

    [Fact]
    public void HasComputed_is_false_before_write()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        Assert.False(EventModelCandidatesArtifact.HasComputed(state));
        Assert.Null(EventModelCandidatesArtifact.Read(state));
    }

    [Fact]
    public void Clear_resets_candidates_and_selection()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet
        {
            Candidates = { new EventModelCandidate { EventModelId = "o1" } }
        });
        state.Artifacts.SelectedEventModelId = "o1";
        state.Artifacts.EarningIntent = EarningIntent.PointEarning;

        EventModelCandidatesArtifact.Clear(state);

        Assert.False(EventModelCandidatesArtifact.HasComputed(state));
        Assert.Null(state.Artifacts.SelectedEventModelId);
        Assert.Equal(EarningIntent.Unknown, state.Artifacts.EarningIntent);
    }
}
