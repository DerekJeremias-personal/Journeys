using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventModelCreateNewIntentTests
{
    [Theory]
    [InlineData("I want to make a new campaign that has three tiers")]
    [InlineData("yes, create it")]
    [InlineData("let's create the campaign")]
    [InlineData("ok, proceed to create the campaign")]
    public void Campaign_phrasing_is_not_new_event_model_intent(string message)
    {
        Assert.False(EventModelCreateNewIntent.LooksLikeRequest(message));
    }

    [Theory]
    [InlineData("create the model")]
    [InlineData("create a model called Review for campaign reviews")]
    [InlineData("create a new event model")]
    [InlineData("create a new Order event")]
    [InlineData("new event model for purchases")]
    [InlineData("save the model")]
    public void Event_model_phrasing_is_detected(string message)
    {
        Assert.True(EventModelCreateNewIntent.LooksLikeRequest(message));
    }
}
