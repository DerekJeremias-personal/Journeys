using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class EarningIntentResolverTests
{
    [Fact]
    public void Null_or_empty_brief_returns_Unknown()
    {
        Assert.Equal(EarningIntent.Unknown, EarningIntentResolver.Resolve(null));
        Assert.Equal(EarningIntent.Unknown, EarningIntentResolver.Resolve(""));
        Assert.Equal(EarningIntent.Unknown, EarningIntentResolver.Resolve("   "));
    }

    [Fact]
    public void Structured_earningIntent_field_wins()
    {
        Assert.Equal(EarningIntent.PointEarning,
            EarningIntentResolver.Resolve("{\"earningIntent\":\"PointEarning\"}"));
        Assert.Equal(EarningIntent.NotPointEarning,
            EarningIntentResolver.Resolve("{\"earningIntent\":\"NotPointEarning\"}"));
    }

    [Fact]
    public void Keyword_scan_detects_point_earning()
    {
        Assert.Equal(EarningIntent.PointEarning,
            EarningIntentResolver.Resolve("{\"objective\":\"Members earn points for every purchase.\"}"));
        Assert.Equal(EarningIntent.PointEarning,
            EarningIntentResolver.Resolve("Reward accrual based on spend."));
    }

    [Fact]
    public void Brief_without_earning_signal_returns_Unknown()
    {
        Assert.Equal(EarningIntent.Unknown,
            EarningIntentResolver.Resolve("{\"objective\":\"Send a birthday greeting email.\"}"));
    }
}
