using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Xunit;

namespace Journeys.Tests.Utility;

public class CampaignDraftVerificationResolverTests
{
    [Fact]
    public void ValidateDraftForEvent_succeeds_when_event_subscribed()
    {
        var campaign = new Campaign("ext", CampaignStatusStrings.Draft, "n", new List<string> { "event-guid" },
            DateTimeOffset.UtcNow, null, null, null, "t", "c1");
        var result = CampaignDraftVerificationResolver.ValidateDraftForEvent(campaign, "event-guid");
        Assert.Same(campaign, result);
    }

    [Fact]
    public void ValidateDraftForEvent_throws_campaignEventMismatch()
    {
        var campaign = new Campaign("ext", CampaignStatusStrings.Draft, "n", new List<string> { "other" },
            DateTimeOffset.UtcNow, null, null, null, "t", "c1");
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignDraftVerificationResolver.ValidateDraftForEvent(campaign, "event-guid"));
        Assert.True(ex.Errors.ContainsKey("campaignEventMismatch"));
    }

    [Fact]
    public void ValidateDraftForEvent_throws_campaignNotFound_when_null()
    {
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignDraftVerificationResolver.ValidateDraftForEvent(null, "event-guid"));
        Assert.True(ex.Errors.ContainsKey("campaignNotFound"));
    }
}
