using CampaignContextAudit.Governance;
using Xunit;

namespace CampaignContextAudit.Tests;

public class ContradictionDetectorTests
{
    [Fact]
    public void Detect_contradiction_when_patterns_in_different_layers()
    {
        var corpus = new Dictionary<string, string>
        {
            ["phase.txt"] = "mutating tools available after brief",
            ["registry.txt"] = "blockedUntilPatManifest: validate_campaign"
        };
        var findings = ContradictionDetector.Detect(corpus);
        Assert.Contains(findings, f => f.Code == "CONTRADICTORY_GUIDANCE");
    }
}
