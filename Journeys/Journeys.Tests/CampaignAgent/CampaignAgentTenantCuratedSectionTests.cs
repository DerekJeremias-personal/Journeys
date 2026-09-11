using Journeys.API.CampaignAgent;

namespace Journeys.Tests.CampaignAgent;

public class CampaignAgentTenantCuratedSectionTests
{
    [Fact]
    public void Build_NullSlice_ReturnsEmpty()
    {
        var r = CampaignAgentTenantCuratedSection.Build(null);
        Assert.Null(r.SectionText);
        Assert.Null(r.ContentSha256Hex);
    }

    [Fact]
    public void Build_EmptySlice_ReturnsEmpty()
    {
        var slice = new CampaignAgentTenantLlmSlice(null, null, null, null, "v1");
        var r = CampaignAgentTenantCuratedSection.Build(slice);
        Assert.Null(r.SectionText);
        Assert.Null(r.ContentSha256Hex);
    }

    [Fact]
    public void Build_MarketingOnly_IncludesBlockAndHash()
    {
        var slice = new CampaignAgentTenantLlmSlice("Sell more coffee", null, "2026-01-01", null, "v1");
        var r = CampaignAgentTenantCuratedSection.Build(slice);
        Assert.NotNull(r.SectionText);
        Assert.Contains("TENANT CURATED CONTEXT", r.SectionText, StringComparison.Ordinal);
        Assert.Contains("Sell more coffee", r.SectionText, StringComparison.Ordinal);
        Assert.Contains("2026-01-01", r.SectionText, StringComparison.Ordinal);
        Assert.NotNull(r.ContentSha256Hex);
        Assert.Equal(64, r.ContentSha256Hex.Length);
    }

    [Fact]
    public void Build_AllowlistOnly_IncludesVerificationBlock()
    {
        var slice = new CampaignAgentTenantLlmSlice(null, null, null, null, "v1", ["test_exp_01", "test_exp_02"]);
        var r = CampaignAgentTenantCuratedSection.Build(slice);

        Assert.NotNull(r.SectionText);
        Assert.Contains("campaignTestAccountExtIds", r.SectionText, StringComparison.Ordinal);
        Assert.Contains("test_exp_01", r.SectionText, StringComparison.Ordinal);
        Assert.Contains("test_exp_02", r.SectionText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_EmptyAllowlist_ShowsBlockedMessage()
    {
        var slice = new CampaignAgentTenantLlmSlice(null, null, null, null, "v1", []);
        var r = CampaignAgentTenantCuratedSection.Build(slice);

        Assert.NotNull(r.SectionText);
        Assert.Contains("draft verification blocked", r.SectionText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_BothContexts_IncludesBoth()
    {
        var slice = new CampaignAgentTenantLlmSlice("Biz", "Shopper", "Ma", "Ca", "v2");
        var r = CampaignAgentTenantCuratedSection.Build(slice);
        Assert.Contains("Marketing / business context", r.SectionText, StringComparison.Ordinal);
        Assert.Contains("End-consumer / member audience", r.SectionText, StringComparison.Ordinal);
        Assert.Contains("Biz", r.SectionText, StringComparison.Ordinal);
        Assert.Contains("Shopper", r.SectionText, StringComparison.Ordinal);
    }
}
