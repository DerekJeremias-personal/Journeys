using CampaignContextAudit.Governance;
using Xunit;

namespace CampaignContextAudit.Tests;

public class RuleNormalizerTests
{
    [Fact]
    public void ExtractRules_keeps_bullet_lines()
    {
        var rules = RuleNormalizer.ExtractRules(
            "- Call upsert_point_account_type now.",
            GuidanceLayer.Phase,
            "test.txt");
        Assert.Single(rules);
        Assert.Contains("upsert_point_account_type", rules[0].Excerpt);
    }

    [Fact]
    public void Jaccard_identical_strings_is_one()
    {
        var a = RuleNormalizer.Tokenize("call upsert_point_account_type now");
        var b = RuleNormalizer.Tokenize("call upsert_point_account_type now");
        Assert.Equal(1.0, RuleNormalizer.Jaccard(a, b), 3);
    }
}
