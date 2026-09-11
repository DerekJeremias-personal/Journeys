using System.Text.Json;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Utility;

public class CampaignAssistantContextDigesterTests
{
    [Fact]
    public void Digest_keeps_digests_and_contracts_omits_template()
    {
        var ctx = new CampaignAssistantContextDto
        {
            TenantId = "primo",
            CampaignId = "camp-1",
            Status = "draft",
            CampaignShell = new CampaignShellDigest { CampaignId = "camp-1", HasJourneyPayload = true },
            Journey = new CampaignJourneyArtifactDigest { CampaignId = "camp-1", RuleSetCount = 2 },
            EventModels =
            [
                new EventModelAssistantContextDto
                {
                    ModelId = "m1",
                    Name = "order",
                    ModelType = "loyalty",
                    ProcessingContract = new EventProcessingContractDigest
                    {
                        EventModelId = "m1",
                        IsProcessEventEligible = true,
                        AccountLink = new AccountLinkDigest { SymbolPath = "profileid" }
                    },
                    Attributes =
                    [
                        new AttributeSymbolRowDto
                        {
                            Symbol = "ordertotal",
                            DataType = "decimal",
                            AttributeType = "Primitive",
                            Status = "Live",
                            RestrictionSummary = "Very long restriction narrative that should be dropped."
                        }
                    ]
                }
            ],
            SampleScaffold = new SamplePayloadScaffoldDto
            {
                RequiredEventSymbols = ["orderid", "ordertotal"],
                JsonTemplate = """{"orderid":null,"ordertotal":null,"profileid":null}""",
                AccountLinkSymbolPath = "profileid"
            },
            Warnings = ["ok"]
        };

        var raw = JsonSerializer.Serialize(ctx, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = CampaignAssistantContextDigester.Digest(raw);

        Assert.True(result.Transformed);
        Assert.True(result.DigestChars < result.OriginalChars);
        Assert.DoesNotContain("restrictionSummary", result.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jsonTemplate", result.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("allowedModelAttributeTypes", result.Json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("processingContract", result.Json, StringComparison.Ordinal);
        Assert.Contains("campaignShell", result.Json, StringComparison.Ordinal);
        Assert.Contains("requiredEventSymbols", result.Json, StringComparison.Ordinal);
        Assert.Contains("attributeCount", result.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"attributes\"", result.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("restrictionSummary", result.Json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Digest_typical_primo_fixture_under_4k_chars()
    {
        var attrs = Enumerable.Range(0, 24)
            .Select(i => new AttributeSymbolRowDto
            {
                Symbol = $"field{i}",
                DataType = "decimal",
                AttributeType = "Primitive",
                Status = "Live",
                RestrictionSummary = new string('x', 120)
            })
            .ToList();

        var ctx = new CampaignAssistantContextDto
        {
            TenantId = "primo",
            CampaignId = "camp-1",
            Status = "draft",
            CampaignShell = new CampaignShellDigest { CampaignId = "camp-1", HasJourneyPayload = true },
            Journey = new CampaignJourneyArtifactDigest { CampaignId = "camp-1", RuleSetCount = 3 },
            EventModels =
            [
                new EventModelAssistantContextDto
                {
                    ModelId = "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03",
                    Name = "order",
                    ModelType = "loyalty",
                    ProcessingContract = new EventProcessingContractDigest
                    {
                        EventModelId = "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03",
                        IsProcessEventEligible = true,
                        AccountLink = new AccountLinkDigest { SymbolPath = "profileid" }
                    },
                    Attributes = attrs
                }
            ],
            SampleScaffold = new SamplePayloadScaffoldDto
            {
                RequiredEventSymbols = ["orderid", "ordertotal", "profileid"],
                JsonTemplate = """{"orderid":null,"ordertotal":null,"profileid":null}""",
                AccountLinkSymbolPath = "profileid",
                NaturalKeySymbols = ["orderid"],
                FieldRoles = new Dictionary<string, string> { ["orderid"] = "naturalKey" }
            }
        };

        var raw = JsonSerializer.Serialize(ctx, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.True(raw.Length > 4000, "fixture should start large");

        var result = CampaignAssistantContextDigester.Digest(raw);

        Assert.True(result.Transformed);
        Assert.True(result.DigestChars <= 4000, $"digest was {result.DigestChars} chars");
        Assert.Contains("attributeCount", result.Json, StringComparison.Ordinal);
    }

    [Fact]
    public void Digest_passthrough_for_not_found_text()
    {
        const string raw = "Campaign not found: tenantId=primo, campaignId=x, status=Live";
        var result = CampaignAssistantContextDigester.Digest(raw);
        Assert.False(result.Transformed);
        Assert.Equal(raw, result.Json);
    }
}
