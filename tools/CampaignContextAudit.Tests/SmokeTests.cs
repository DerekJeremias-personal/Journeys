using Xunit;

namespace CampaignContextAudit.Tests;

public class SmokeTests
{
    [Fact]
    public void Program_type_is_reachable()
    {
        Assert.Equal("CampaignContextAudit", typeof(Program).Namespace);
    }
}
