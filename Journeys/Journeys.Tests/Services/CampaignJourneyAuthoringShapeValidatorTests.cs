using System.Text.Json;
using Journeys.Core.Extensions;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.Services;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Services;

public class CampaignJourneyAuthoringShapeValidatorTests
{
    private const string FlatRuleCampaignJson = """
    {
      "id": "camp-1",
      "name": "Test",
      "journey": {
        "rules": [
          {
            "kind": "SimpleRule",
            "leftProvider": { "$type": "PathValueProvider", "propertyPath": "event.ordertotal" },
            "rightProvider": { "$type": "ConstantValueProvider", "constantValue": 0 },
            "evaluator": { "$type": "NumericEvaluation", "evalType": "GreaterThan" },
            "outcomes": [
              { "kind": "DepositPointsOutcome", "pointAccountTypeId": "pat-1" }
            ]
          }
        ]
      }
    }
    """;

    private const string ArrayRuleJsonElementCampaignJson = """
    {
      "id": "camp-1",
      "name": "Test",
      "journey": {
        "rules": [
          {
            "name": "Earn",
            "ruleJsonElement": [
              { "Kind": "NumericPropertyRule" },
              { "Kind": "HistoricalRule" }
            ],
            "outcomesJsonElement": [ { "Kind": "DepositPointsOutcome" } ]
          }
        ]
      }
    }
    """;

    private const string ValidAndRuleWrapperCampaignJson = """
    {
      "id": "camp-1",
      "name": "Test",
      "journey": {
        "rules": [
          {
            "name": "Earn",
            "ruleJsonElement": {
              "Kind": "AndRule",
              "Children": [
                { "Kind": "NumericPropertyRule" },
                { "Kind": "HistoricalRule" }
              ]
            },
            "outcomesJsonElement": [ { "Kind": "DepositPointsOutcome" } ]
          }
        ]
      }
    }
    """;

    private const string StringRuleJsonElementCampaignJson = """
    {
      "id": "camp-1",
      "name": "Test",
      "journey": {
        "rules": [
          {
            "name": "Earn",
            "ruleJsonElement": "{\"Kind\":\"NumericPropertyRule\",\"leftProvider\":{\"$type\":\"ConstantValueProvider\",\"value\":\"0\"},\"rightProvider\":{\"$type\":\"ConstantValueProvider\",\"value\":\"0\"},\"evaluator\":{\"$type\":\"NumericEvaluation\",\"evalType\":\"GreaterThan\"}}",
            "outcomesJsonElement": [ { "Kind": "DepositPointsOutcome" } ]
          }
        ]
      }
    }
    """;

    private const string ValidRuleSetCampaignJson = """
    {
      "id": "camp-1",
      "name": "Test",
      "journey": {
        "rules": [
          {
            "name": "Earn",
            "ruleJsonElement": { "Kind": "NumericPropertyRule" },
            "outcomesJsonElement": [ { "Kind": "DepositPointsOutcome" } ]
          }
        ]
      }
    }
    """;

    [Fact]
    public void ValidateCampaignJson_rejects_flat_rule_in_rules_array()
    {
        using var doc = JsonDocument.Parse(FlatRuleCampaignJson);
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyAuthoringShapeValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Contains(ex.Errors.Keys, k => k.StartsWith("journey.shape.", StringComparison.Ordinal));
        Assert.Contains("JOURNEY_SHAPE_FLAT_RULE_IN_RULESET_ARRAY", ex.Errors.Values.First(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCampaignJson_allows_rule_set_wrapper_shape()
    {
        using var doc = JsonDocument.Parse(ValidRuleSetCampaignJson);
        var ex = Record.Exception(() =>
            CampaignJourneyAuthoringShapeValidator.ValidateCampaignJson(doc.RootElement));
        Assert.Null(ex);
    }

    [Fact]
    public void ValidateCampaign_rejects_deserialized_empty_rule_sets()
    {
        var dto = JsonSerializer.Deserialize<CampaignDto>(FlatRuleCampaignJson, JsonOpts())!;
        var campaign = dto.FromDto()!;

        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyAuthoringShapeValidator.ValidateCampaign(campaign));

        Assert.Contains("JOURNEY_SHAPE_EMPTY_RULESET", ex.Errors.Values.First(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCampaignJson_rejects_array_ruleJsonElement()
    {
        using var doc = JsonDocument.Parse(ArrayRuleJsonElementCampaignJson);
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyAuthoringShapeValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Contains(ex.Errors.Values, v =>
            v.Contains("JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT", StringComparison.Ordinal));
        Assert.Contains("path=journey/rules[0]/ruleJsonElement", ex.Errors.Values.First(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCampaignJson_allows_and_rule_wrapper()
    {
        using var doc = JsonDocument.Parse(ValidAndRuleWrapperCampaignJson);
        var ex = Record.Exception(() =>
            CampaignJourneyAuthoringShapeValidator.ValidateCampaignJson(doc.RootElement));
        Assert.Null(ex);
    }

    [Fact]
    public void ValidateJourneyDto_rejects_array_ruleJsonElement()
    {
        var dto = JsonSerializer.Deserialize<CampaignDto>(ArrayRuleJsonElementCampaignJson, JsonOpts())!;
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyAuthoringShapeValidator.ValidateJourneyDto(dto.Journey));

        Assert.Contains("JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT", ex.Errors.Values.First(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCampaignJson_rejects_string_ruleJsonElement()
    {
        using var doc = JsonDocument.Parse(StringRuleJsonElementCampaignJson);
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyAuthoringShapeValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Contains(ex.Errors.Values, v =>
            v.Contains(JourneyRuleShapeRules.StringEncodedViolationCode, StringComparison.Ordinal));
        Assert.Contains("path=journey/rules[0]/ruleJsonElement", ex.Errors.Values.First(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateJourneyDto_rejects_string_ruleJsonElement()
    {
        var dto = JsonSerializer.Deserialize<CampaignDto>(StringRuleJsonElementCampaignJson, JsonOpts())!;
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyAuthoringShapeValidator.ValidateJourneyDto(dto.Journey));

        Assert.Contains(JourneyRuleShapeRules.StringEncodedViolationCode, ex.Errors.Values.First(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCampaignJson_rejects_journey_nodes_array()
    {
        const string json = """
            {
              "journey": {
                "nodes": [
                  { "id": "n1", "name": "Bronze", "rules": [] }
                ]
              }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyAuthoringShapeValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Contains("JOURNEY_SHAPE_NODES_NOT_CHILDREN", ex.Errors.Values.First(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateInlineManifestForbidden_rejects_when_workflow_has_pats()
    {
        const string json = """{"pointAccountManifest":{"items":[{"id":"pat-1"}]}}""";
        using var doc = JsonDocument.Parse(json);
        var ex = Assert.Throws<APIErrorsException>(() =>
            CampaignJourneyAuthoringShapeValidator.ValidateInlineManifestForbidden(doc.RootElement, workflowPatCount: 2));

        Assert.Contains("JOURNEY_SHAPE_INLINE_MANIFEST", ex.Errors.Values.First(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCampaign_allows_empty_rules_array_for_shell()
    {
        var journey = new JourneyNode("root", new List<RuleSet>(), "j1", "j1", null, null);
        var campaign = new Campaign("ext", CampaignStatusStrings.Draft, "n", null, DateTimeOffset.UtcNow, null, null, journey, "t", "c");

        var ex = Record.Exception(() => CampaignJourneyAuthoringShapeValidator.ValidateCampaign(campaign));
        Assert.Null(ex);
    }

    private static JsonSerializerOptions JsonOpts() => new() { PropertyNameCaseInsensitive = true };
}
