using CampaignContextAudit.Governance;
using Xunit;

namespace CampaignContextAudit.Tests;

public class CrossLayerDuplicationAnalyzerTests
{
    [Fact]
    public void Analyze_deduplicates_to_single_cluster()
    {
        var rules = new List<NormalizedRule>
        {
            RuleNormalizer.ExtractRules("- Call validate_campaign before upsert_campaign with full JSON.", GuidanceLayer.Coach, "CampaignValidationCoach.cs")[0],
            RuleNormalizer.ExtractRules("Call validate_campaign before upsert_campaign with full JSON.", GuidanceLayer.SalientFacts, "SalientFactsPromptBuilder.cs")[0],
            RuleNormalizer.ExtractRules("Coach: Call validate_campaign before upsert_campaign with full JSON.", GuidanceLayer.Remediation, "ElpToolRemediationCatalog.cs")[0]
        };

        var clusters = CrossLayerDuplicationAnalyzer.Analyze(rules);
        Assert.Single(clusters);
        Assert.Equal("Journey validate/upsert", clusters[0].Theme);
        Assert.True(clusters[0].LayerCount >= 2);
    }
}
