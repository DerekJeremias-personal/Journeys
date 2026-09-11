using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public sealed class JourneyDigestUtilityTests
{
    [Fact]
    public void TryReadRuleSetCountFromDigest_reads_camelCase()
    {
        Assert.Equal(3, JourneyDigestUtility.TryReadRuleSetCountFromDigest("""{"ruleSetCount":3}"""));
    }

    [Fact]
    public void TryReadRuleSetCountFromDigest_returns_zero_when_missing()
    {
        Assert.Equal(0, JourneyDigestUtility.TryReadRuleSetCountFromDigest("""{"hasRules":true}"""));
    }
}
