using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class CampaignTestAccountAllowlistTests
{
    [Theory]
    [InlineData("test_exp_01", "TEST_EXP_01", true)]
    [InlineData("test_exp_01", "other", false)]
    public void IsAllowlisted_case_insensitive(string allowlisted, string candidate, bool expected)
    {
        var list = new List<string> { allowlisted };
        Assert.Equal(expected, CampaignTestAccountAllowlist.IsAllowlisted(list, candidate));
    }

    [Fact]
    public void IsAllowlisted_empty_list_returns_false()
    {
        Assert.False(CampaignTestAccountAllowlist.IsAllowlisted(null, "test_exp_01"));
        Assert.False(CampaignTestAccountAllowlist.IsAllowlisted(new List<string>(), "test_exp_01"));
    }

    [Fact]
    public void IsAllowlisted_blank_candidate_returns_false()
    {
        Assert.False(CampaignTestAccountAllowlist.IsAllowlisted(new List<string> { "a" }, "  "));
    }
}
