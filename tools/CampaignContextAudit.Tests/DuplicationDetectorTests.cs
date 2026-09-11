using CampaignContextAudit.Governance;
using Xunit;

namespace CampaignContextAudit.Tests;

public class DuplicationDetectorTests
{
    [Fact]
    public void Detect_finds_cross_layer_duplicate()
    {
        var rules = new List<NormalizedRule>
        {
            RuleNormalizer.ExtractRules("- Use children[] not nodes[] in journey rules.", GuidanceLayer.Phase, "phase.txt")[0],
            RuleNormalizer.ExtractRules("- Use children[] not nodes[] in journey rules.", GuidanceLayer.Coach, "JourneyCoach.cs")[0]
        };

        var findings = DuplicationDetector.Detect(rules);
        Assert.Contains(findings, f => f.Code == "DUPLICATE_RULE");
    }
}
