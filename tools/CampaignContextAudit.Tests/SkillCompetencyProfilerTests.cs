using CampaignContextAudit.Governance;
using Xunit;

namespace CampaignContextAudit.Tests;

public class SkillCompetencyProfilerTests
{
    [Fact]
    public void CampaignBuild_high_redundancy_gets_band_C_or_worse()
    {
        var rules = new List<NormalizedRule>();
        for (var i = 0; i < 5; i++)
        {
            rules.AddRange(RuleNormalizer.ExtractRules(
                "- Call upsert_point_account_type for PAT manifest.",
                GuidanceLayer.Phase,
                "phase.txt"));
            rules.AddRange(RuleNormalizer.ExtractRules(
                "Call upsert_point_account_type for PAT manifest now.",
                GuidanceLayer.Coach,
                "CampaignBuildCoach.cs"));
        }

        var profiles = SkillCompetencyProfiler.BuildSynthetic(rules, []);
        var build = profiles.First(p => p.Skill == "CampaignBuild");
        Assert.True(build.CompetencyBand is "C" or "D" or "F");
    }
}
