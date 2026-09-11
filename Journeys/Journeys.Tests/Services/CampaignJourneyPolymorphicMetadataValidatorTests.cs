using System.Text.Json;
using Journeys.Core.Extensions;
using Journeys.Core.Services;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Services;

public class CampaignJourneyPolymorphicMetadataValidatorTests
{
    private const string PointBalanceHistoricalValueProviderCampaignJson = """
    {
      "id": "camp-1",
      "name": "Test",
      "journey": {
        "id": "root",
        "rules": [{
          "name": "TierQual",
          "ruleJsonElement": {
            "Kind": "HistoricalRule",
            "HistoricalValueProvider": {
              "$type": "PointBalanceHistoricalValueProvider",
              "PointAccountTypeId": "pat-tier"
            }
          },
          "outcomesJsonElement": []
        }]
      }
    }
    """;

    [Fact]
    public void ValidateCampaignJson_rejects_PointBalanceHistoricalValueProvider_on_HistoricalValueProvider()
    {
        using var doc = JsonDocument.Parse(PointBalanceHistoricalValueProviderCampaignJson);
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyPolymorphicMetadataValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Contains(ex.Errors.Keys, k => k.StartsWith("journey.validation.", StringComparison.Ordinal));
        var message = ex.Errors.Values.First();
        Assert.Contains("TIER_A_UNKNOWN_TYPE_DISCRIMINATOR", message, StringComparison.Ordinal);
        Assert.Contains("PointBalanceHistoricalValueProvider", message, StringComparison.Ordinal);
        Assert.Contains("HistoricalValueProvider", message, StringComparison.Ordinal);
        Assert.Contains("allowed=SimpleCalculationProvider", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCampaign_rejects_PointBalanceHistoricalValueProvider_when_rule_not_materialized()
    {
        var dto = JsonSerializer.Deserialize<CampaignDto>(PointBalanceHistoricalValueProviderCampaignJson, JsonOpts())!;
        var campaign = dto.FromDto()!;

        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyPolymorphicMetadataValidator.ValidateCampaign(campaign));

        Assert.Contains(ex.Errors.Keys, k => k.StartsWith("journey.validation.", StringComparison.Ordinal));
        var message = ex.Errors.Values.First();
        Assert.Contains("TIER_A_UNKNOWN_TYPE_DISCRIMINATOR", message, StringComparison.Ordinal);
        Assert.Contains("PointBalanceHistoricalValueProvider", message, StringComparison.Ordinal);
        Assert.Contains("HistoricalValueProvider", message, StringComparison.Ordinal);
        Assert.Contains("allowed=SimpleCalculationProvider", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCampaignJson_rejects_pointAccountTypeId_alias_on_deposit_outcome()
    {
        const string json = """
        {
          "id": "camp-1",
          "name": "Test",
          "journey": {
            "id": "j1",
            "rules": [{
              "name": "Earn",
              "ruleJsonElement": { "Kind": "SimpleRule" },
              "outcomesJsonElement": [{
                "Kind": "DepositPointsOutcome",
                "pointAccountTypeId": "pat-spendable"
              }]
            }]
          }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyPolymorphicMetadataValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Contains(ex.Errors.Values, v =>
            v.Contains("TIER_A_OUTCOME_PAT_ALIAS_MISUSED", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateCampaignJson_rejects_deposit_without_affectedPointAccountTypeIds()
    {
        const string json = """
        {
          "id": "camp-1",
          "name": "Test",
          "journey": {
            "id": "j1",
            "rules": [{
              "name": "Earn",
              "ruleJsonElement": { "Kind": "SimpleRule" },
              "outcomesJsonElement": [{ "Kind": "DepositPointsOutcome" }]
            }]
          }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyPolymorphicMetadataValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Contains(ex.Errors.Values, v =>
            v.Contains("TIER_A_DEPOSIT_MISSING_AFFECTED_PAT", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateCampaignJson_allows_valid_historical_rule()
    {
        const string json = """
        {
          "id": "camp-1",
          "name": "Test",
          "journey": {
            "id": "root",
            "rules": [{
              "name": "HistRule",
              "ruleJsonElement": {
                "Kind": "HistoricalRule",
                "HistoricalValueProvider": {
                  "$type": "SimpleCalculationProvider"
                }
              },
              "outcomesJsonElement": []
            }]
          }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var ex = Record.Exception(() =>
            CampaignJourneyPolymorphicMetadataValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Null(ex);
    }

    [Fact]
    public void ValidateCampaignJson_rejects_unknown_rule_kind()
    {
        const string json = """
        {
          "id": "camp-1",
          "name": "Test",
          "journey": {
            "id": "root",
            "rules": [{
              "name": "BadRule",
              "ruleJsonElement": {
                "Kind": "PropertyValueComparison"
              },
              "outcomesJsonElement": []
            }]
          }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyPolymorphicMetadataValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Contains(ex.Errors.Keys, k => k.StartsWith("journey.validation.", StringComparison.Ordinal));
        var message = ex.Errors.Values.First();
        Assert.Contains("TIER_A_UNKNOWN_RULE_KIND", message, StringComparison.Ordinal);
        Assert.Contains("PropertyValueComparison", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCampaignJson_rejects_missing_type_on_leftProvider()
    {
        const string json = """
        {
          "id": "camp-1",
          "name": "Test",
          "journey": {
            "id": "root",
            "rules": [{
              "name": "NumRule",
              "ruleJsonElement": {
                "Kind": "NumericPropertyRule",
                "LeftProvider": {
                  "Kind": "PathValueProvider",
                  "PropertyPath": "event.amount"
                }
              },
              "outcomesJsonElement": []
            }]
          }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyPolymorphicMetadataValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Contains(ex.Errors.Keys, k => k.StartsWith("journey.validation.", StringComparison.Ordinal));
        var message = ex.Errors.Values.First();
        Assert.Contains("TIER_A_MISSING_TYPE_DISCRIMINATOR", message, StringComparison.Ordinal);
        Assert.Contains("LeftProvider", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCampaignJson_allows_historical_by_category_campaign_fixture()
    {
        var fixturePath = Path.Combine(
            ApiContentRoot,
            "Examples",
            "Journeys",
            "Campaigns",
            "historical-by-category-campaign.json");

        if (!File.Exists(fixturePath))
            return;

        var json = File.ReadAllText(fixturePath);
        using var doc = JsonDocument.Parse(json);
        var ex = Record.Exception(() =>
            CampaignJourneyPolymorphicMetadataValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Null(ex);
    }

    private static readonly string ApiContentRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "Journeys.API"));

    private static JsonSerializerOptions JsonOpts() => new() { PropertyNameCaseInsensitive = true };
}
