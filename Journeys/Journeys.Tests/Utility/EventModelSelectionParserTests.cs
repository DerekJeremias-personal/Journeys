using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventModelSelectionParserTests
{
    private static EventModelCandidateSet Set() => new()
    {
        RecommendedDefaultId = "o1",
        Candidates =
        {
            new EventModelCandidate { EventModelId = "o1", Name = "Order", IsStrongMatch = true },
            new EventModelCandidate { EventModelId = "e1", Name = "EmailOpened" }
        }
    };

    [Fact]
    public void Affirmative_confirm_resolves_recommended_default()
    {
        var r = EventModelSelectionParser.Resolve("yes, use it", Set());
        Assert.Equal(EventModelSelectionKind.PickExisting, r.Kind);
        Assert.Equal("o1", r.SelectedEventModelId);
    }

    [Fact]
    public void Pick_by_name_resolves_that_candidate()
    {
        var r = EventModelSelectionParser.Resolve("let's use EmailOpened", Set());
        Assert.Equal(EventModelSelectionKind.PickExisting, r.Kind);
        Assert.Equal("e1", r.SelectedEventModelId);
    }

    [Fact]
    public void Pick_by_id_resolves_that_candidate()
    {
        var r = EventModelSelectionParser.Resolve("e1", Set());
        Assert.Equal(EventModelSelectionKind.PickExisting, r.Kind);
        Assert.Equal("e1", r.SelectedEventModelId);
    }

    [Fact]
    public void Create_new_intent_is_detected()
    {
        var r = EventModelSelectionParser.Resolve("create a new event model", Set());
        Assert.Equal(EventModelSelectionKind.CreateNew, r.Kind);
        Assert.Null(r.SelectedEventModelId);
    }

    [Fact]
    public void Create_the_model_is_detected()
    {
        var r = EventModelSelectionParser.Resolve("create the model", Set());
        Assert.Equal(EventModelSelectionKind.CreateNew, r.Kind);
        Assert.Null(r.SelectedEventModelId);
    }

    [Fact]
    public void Create_a_model_called_is_detected()
    {
        var r = EventModelSelectionParser.Resolve("create a model called Review for campaign reviews", Set());
        Assert.Equal(EventModelSelectionKind.CreateNew, r.Kind);
        Assert.Null(r.SelectedEventModelId);
    }

    [Fact]
    public void Confirm_without_recommended_default_is_ambiguous()
    {
        var noDefault = Set();
        noDefault.RecommendedDefaultId = null;
        var r = EventModelSelectionParser.Resolve("yes", noDefault);
        Assert.Equal(EventModelSelectionKind.Ambiguous, r.Kind);
    }

    [Fact]
    public void Unrelated_message_is_ambiguous()
    {
        var r = EventModelSelectionParser.Resolve("what does eventable mean?", Set());
        Assert.Equal(EventModelSelectionKind.Ambiguous, r.Kind);
    }

    [Fact]
    public void Create_new_takes_precedence_over_name_substring()
    {
        var r = EventModelSelectionParser.Resolve("create a new Order event", Set());
        Assert.Equal(EventModelSelectionKind.CreateNew, r.Kind);
    }

    [Fact]
    public void Proceed_to_create_campaign_confirms_strong_recommended_default()
    {
        var r = EventModelSelectionParser.Resolve("ok, proceed to create the campaign", Set());
        Assert.Equal(EventModelSelectionKind.PickExisting, r.Kind);
        Assert.Equal("o1", r.SelectedEventModelId);
    }

    [Fact]
    public void Create_the_campaign_without_strong_default_is_ambiguous()
    {
        var set = Set();
        set.Candidates[0].IsStrongMatch = false;
        var r = EventModelSelectionParser.Resolve("let's create the campaign", set);
        Assert.Equal(EventModelSelectionKind.Ambiguous, r.Kind);
    }
}
