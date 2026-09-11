using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class JourneyProceedIntentTests
{
    [Theory]
    [InlineData("continue to the journey", true)]
    [InlineData("author the journey now", true)]
    [InlineData("build the tiers and journey", true)]
    [InlineData("proceed to create the campaign", true)]
    [InlineData("what tiers do you recommend?", false)]
    public void LooksLikeProceedToJourney(string message, bool expected) =>
        Assert.Equal(expected, JourneyProceedIntent.LooksLikeProceedToJourney(message));
}
