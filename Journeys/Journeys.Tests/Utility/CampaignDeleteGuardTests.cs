using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Xunit;

namespace Journeys.Tests.Utility;

public class CampaignDeleteGuardTests
{
    [Fact]
    public void ValidateCanHardDelete_allows_never_promoted_draft()
    {
        var draft = new Campaign("ext", CampaignStatusStrings.Draft, "n", null,
            DateTimeOffset.UtcNow, null, null, null, "t", "c1");
        CampaignDeleteGuard.ValidateCanHardDelete(draft);
    }

    [Fact]
    public void ValidateCanHardDelete_rejects_live()
    {
        var live = new Campaign("ext", CampaignStatusStrings.Live, "n", null,
            DateTimeOffset.UtcNow, null, null, null, "t", "c1", DateTimeOffset.UtcNow, null);
        var ex = Assert.Throws<APIErrorsException>(() => CampaignDeleteGuard.ValidateCanHardDelete(live));
        Assert.True(ex.Errors.ContainsKey("deleteNotPermitted"));
    }

    [Fact]
    public void ValidateCanHardDelete_rejects_deployed_draft()
    {
        var draft = new Campaign("ext", CampaignStatusStrings.Draft, "n", null,
            DateTimeOffset.UtcNow, null, null, null, "t", "c1", DateTimeOffset.UtcNow, null);
        var ex = Assert.Throws<APIErrorsException>(() => CampaignDeleteGuard.ValidateCanHardDelete(draft));
        Assert.True(ex.Errors.ContainsKey("deleteNotPermitted"));
    }
}
