using Journeys.Core.Services;

namespace Journeys.Tests.Services;

public class CampaignEventMatchingTests
{
    [Fact]
    public void MatchesEventPayloadModel_true_when_events_list_contains_model_id()
    {
        var eventModelId = "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03";
        var events = new List<string> { "other-id", eventModelId };
        Assert.True(CampaignEventMatching.MatchesEventPayloadModel(events, eventModelId));
    }

    [Fact]
    public void MatchesEventPayloadModel_false_when_events_missing_model_id()
    {
        var events = new List<string> { "11111111-1111-1111-1111-111111111111" };
        Assert.False(CampaignEventMatching.MatchesEventPayloadModel(events, "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03"));
    }

    [Fact]
    public void MatchesEventPayloadModel_false_when_events_null()
    {
        Assert.False(CampaignEventMatching.MatchesEventPayloadModel(null, "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03"));
    }

    [Fact]
    public void MatchesEventPayloadModel_false_when_event_model_id_empty()
    {
        Assert.False(CampaignEventMatching.MatchesEventPayloadModel(new List<string> { "x" }, ""));
    }
}
