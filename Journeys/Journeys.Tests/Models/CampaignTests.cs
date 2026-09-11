using Journeys.Core.Models;
using System;
using System.Linq;
using Xunit;

namespace Journeys.Tests.Models
{
    public class CampaignTests
    {
        /// <summary>Ensures PointAccountTypeCache is initialized so TestCampaignFactory can cache point account types.</summary>
        private static void EnsureCacheInitialized()
        {
            _ = new Stubs.StubPointAccountTypeCache();
        }

        [Fact]
        public void Constructor_sets_ExtCampaignId_Status_Name_Events_Journey()
        {
            var journey = new Journeys.Core.RulesEngine.Journey.JourneyNode("Root", null, "root-1", "root-1", null, null);
            var campaign = new Campaign(
                extCampaignId: "ext-1",
                status: CampaignStatusStrings.Live,
                name: "Test Campaign",
                events: new System.Collections.Generic.List<string> { "evt1" },
                startDate: DateTimeOffset.UtcNow.AddDays(-1),
                endDate: null,
                segments: null,
                journey: journey,
                tenantId: "T1",
                id: "camp-1");
            Assert.Equal("ext-1", campaign.ExtCampaignId);
            Assert.Equal(CampaignStatusStrings.Live, campaign.Status);
            Assert.Equal("Test Campaign", campaign.Name);
            Assert.Single(campaign.Events);
            Assert.Equal("evt1", campaign.Events![0]);
            Assert.NotNull(campaign.Journey);
            Assert.Equal("root-1", campaign.Journey!.Id);
        }

        [Fact]
        public void ExternalId_returns_ExtCampaignId()
        {
            var campaign = new Campaign("ext-2", "Draft", "Draft Campaign", null,
                DateTimeOffset.UtcNow, null, null, null, "T1", "camp-2");
            Assert.Equal("ext-2", campaign.ExternalId);
        }

        [Fact]
        public void ExternalIdType_returns_Campaign()
        {
            var campaign = new Campaign(null, "Archive", "Archived", null,
                DateTimeOffset.UtcNow, null, null, null, "T1", "camp-3");
            Assert.Equal("Campaign", campaign.ExternalIdType);
        }

        [Fact]
        public void CampaignStatusStrings_has_Live_Draft_Archive()
        {
            Assert.Equal("Live", CampaignStatusStrings.Live);
            Assert.Equal("Draft", CampaignStatusStrings.Draft);
            Assert.Equal("Archive", CampaignStatusStrings.Archive);
        }

        [Fact]
        public void EndDate_and_Segments_nullable()
        {
            var campaign = new Campaign("e", "Live", "N", new System.Collections.Generic.List<string>(),
                DateTimeOffset.UtcNow, null, null, null, "T", "id");
            Assert.Null(campaign.EndDate);
            Assert.Null(campaign.Segments);
        }

        [Fact]
        public void GetTierCampaignV1_has_root_and_three_children()
        {
            EnsureCacheInitialized();
            var campaign = TestCampaignFactory.GetTierCampaignV1();
            Assert.NotNull(campaign.Journey);
            Assert.Equal(TestCampaignFactory.TierCampaignV1Id, campaign.Id);
            Assert.Equal(TestCampaignFactory.TenantA, campaign.TenantId);
            Assert.Equal(3, campaign.Journey!.Children!.Count);
            Assert.Contains(campaign.Journey.Children, c => c.Id == "Bronze");
            Assert.Contains(campaign.Journey.Children, c => c.Id == "Silver");
            Assert.Contains(campaign.Journey.Children, c => c.Id == "Gold");
        }

        [Fact]
        public void GetTierCampaignV2_has_same_structure_as_V1_different_tenant()
        {
            EnsureCacheInitialized();
            var campaign = TestCampaignFactory.GetTierCampaignV2();
            Assert.Equal(TestCampaignFactory.TenantB, campaign.TenantId);
            Assert.Equal(3, campaign.Journey!.Children!.Count);
        }

        [Fact]
        public void GetTaxonomyCampaignV1_has_taxonomic_rule_and_included_tree_nodes()
        {
            EnsureCacheInitialized();
            var campaign = TestCampaignFactory.GetTaxonomyCampaignV1();
            Assert.NotNull(campaign.Journey?.Rules);
            var ruleSet = campaign.Journey!.Rules![0];
            var taxRule = ruleSet.FlattenToRulesOfType<Journeys.Core.RulesEngine.Rules.TaxonomicRule>();
            Assert.NotEmpty(taxRule);
            Assert.NotNull(taxRule[0].IncludedTreeNodes);
            Assert.Equal("sku", taxRule[0].KeySymbolPath);
        }

        [Fact]
        public void GetTaxonomyCampaignV2_has_excluded_tree_nodes()
        {
            EnsureCacheInitialized();
            var campaign = TestCampaignFactory.GetTaxonomyCampaignV2();
            var ruleSet = campaign.Journey!.Rules![0];
            var taxRule = ruleSet.FlattenToRulesOfType<Journeys.Core.RulesEngine.Rules.TaxonomicRule>();
            Assert.NotEmpty(taxRule[0].ExcludedTreeNodes);
        }

        [Fact]
        public void GetHistoricalCampaignV1_has_historical_rule_90d()
        {
            EnsureCacheInitialized();
            var campaign = TestCampaignFactory.GetHistoricalCampaignV1();
            var historicalRules = campaign.Journey!.Rules!
                .SelectMany(r => r.FlattenToRulesOfType<Journeys.Core.RulesEngine.Rules.HistoricalRule>())
                .ToList();
            Assert.NotEmpty(historicalRules);
            Assert.Equal("orders-90d", historicalRules[0].Id);
        }

        [Fact]
        public void GetMinimalCampaign_has_single_node_and_one_ruleset()
        {
            EnsureCacheInitialized();
            var campaign = TestCampaignFactory.GetMinimalCampaign();
            Assert.NotNull(campaign.Journey);
            Assert.Null(campaign.Journey.Children);
            Assert.Single(campaign.Journey.Rules!);
        }

        [Fact]
        public void GetNotStartedCampaign_StartDate_in_future()
        {
            EnsureCacheInitialized();
            var campaign = TestCampaignFactory.GetNotStartedCampaign();
            Assert.True(campaign.StartDate > DateTimeOffset.UtcNow);
        }

        [Fact]
        public void GetEndedCampaign_EndDate_in_past()
        {
            EnsureCacheInitialized();
            var campaign = TestCampaignFactory.GetEndedCampaign();
            Assert.True(campaign.EndDate!.Value < DateTimeOffset.UtcNow);
        }
    }
}
