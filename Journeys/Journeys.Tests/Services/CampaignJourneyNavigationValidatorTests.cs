using System.Text.Json;
using Journeys.Core.Services;
using Journeys.DTO.Exceptions;
using Xunit;

namespace Journeys.Tests.Services;

public class CampaignJourneyNavigationValidatorTests
{
    private const string ChildRulesNoNavJson = """
    {
      "id": "camp-1",
      "journey": {
        "name": "Tier root",
        "rules": [],
        "children": [
          {
            "id": "Bronze",
            "name": "Bronze",
            "rules": [
              {
                "name": "Bronze earn",
                "ruleJsonElement": { "Kind": "SimpleRule" },
                "outcomesJsonElement": [ { "Kind": "DepositPointsOutcome" } ]
              }
            ]
          }
        ]
      }
    }
    """;

    private const string InventedProviderJson = """
    {
      "id": "camp-1",
      "journey": {
        "name": "Tier root",
        "navigation": {
          "Entry": {
            "$type": "SimpleNavigationCriteria",
            "navConstraint": {
              "Kind": "NumericPropertyRule",
              "LeftProvider": { "$type": "CurrentBalanceProvider" },
              "RightProvider": { "$type": "ConstantValueProvider", "ConstantValue": 0 },
              "Evaluator": { "$type": "NumericEvaluation", "comparison": "GreaterThanOrEqual" }
            }
          }
        },
        "rules": [
          {
            "name": "earn",
            "ruleJsonElement": { "Kind": "SimpleRule" },
            "outcomesJsonElement": [ { "Kind": "DepositPointsOutcome" } ]
          }
        ]
      }
    }
    """;

    private const string ValidTierNavJson = """
    {
      "id": "camp-1",
      "journey": {
        "name": "Tier root",
        "navigation": {
          "Entry": {
            "$type": "SimpleNavigationCriteria",
            "navConstraint": {
              "Kind": "SimpleRule",
              "LeftProvider": { "$type": "ConstantValueProvider", "ConstantValue": true },
              "RightProvider": { "$type": "ConstantValueProvider", "ConstantValue": true },
              "Evaluator": { "$type": "BoolEvaluation" }
            }
          }
        },
        "children": [
          {
            "id": "Bronze",
            "name": "Bronze",
            "rules": [
              {
                "name": "Bronze earn",
                "ruleJsonElement": { "Kind": "SimpleRule" },
                "outcomesJsonElement": [ { "Kind": "DepositPointsOutcome" } ]
              }
            ],
            "navigation": {
              "Transition": {
                "$type": "SimpleNavigationCriteria",
                "navConstraint": {
                  "Kind": "NumericPropertyRule",
                  "LeftProvider": {
                    "$type": "PointBalanceProvider",
                    "PointAccountTypeId": "pat-tqp"
                  },
                  "RightProvider": { "$type": "ConstantValueProvider", "ConstantValue": 0 },
                  "Evaluator": { "$type": "NumericEvaluation", "comparison": "GreaterThanOrEqual" }
                }
              }
            }
          }
        ]
      }
    }
    """;

    [Fact]
    public void ValidateCampaignJson_rejects_child_rule_node_without_navigation()
    {
        using var doc = JsonDocument.Parse(ChildRulesNoNavJson);
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyNavigationValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Contains(ex.Errors.Keys, k => k.StartsWith("journey.navigation.", StringComparison.Ordinal));
        var combined = string.Join(' ', ex.Errors.Values);
        Assert.Contains("JOURNEY_NAV_MISSING_FOR_RULE_NODE", combined, StringComparison.Ordinal);
        Assert.Contains("JOURNEY_NAV_ROOT_ENTRY_REQUIRED", combined, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCampaignJson_rejects_invented_CurrentBalanceProvider()
    {
        using var doc = JsonDocument.Parse(InventedProviderJson);
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyNavigationValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Contains("JOURNEY_NAV_INVENTED_PROVIDER", ex.Errors.Values.First(), StringComparison.Ordinal);
        Assert.Contains("CurrentBalanceProvider", ex.Errors.Values.First(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCampaignJson_allows_tier_entry_and_transition_pattern()
    {
        using var doc = JsonDocument.Parse(ValidTierNavJson);
        var ex = Record.Exception(() =>
            CampaignJourneyNavigationValidator.ValidateCampaignJson(doc.RootElement));
        Assert.Null(ex);
    }
}
