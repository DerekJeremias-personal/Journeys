using System.Text.Json;
using Journeys.Core.Services;
using Xunit;

namespace Journeys.Tests.Services;

public class CampaignJourneyMaterializeValidationHelperTests
{
    [Fact]
    public void IsJsonMaterializationFailure_includes_JourneyMaterializePathException()
    {
        var inner = new JsonException("inner");
        var ex = new JourneyMaterializePathException(
            "[violation=JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT] path=journey/rules[0]/ruleJsonElement — test",
            inner);

        Assert.True(CampaignJourneyMaterializeValidationHelper.IsJsonMaterializationFailure(ex));
    }

    [Fact]
    public void BuildMaterializeMessage_preserves_path_violation_without_tag_outcome_hint()
    {
        var ex = new JourneyMaterializePathException(
            "[violation=JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT] path=journey/rules[0]/ruleJsonElement — wrap in AndRule",
            new JsonException("Cannot deserialize RuleBase: expected object, received Array."));

        var message = CampaignJourneyMaterializeValidationHelper.BuildMaterializeMessage(ex);

        Assert.Contains("JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT", message, StringComparison.Ordinal);
        Assert.Contains("path=journey/rules[0]/ruleJsonElement", message, StringComparison.Ordinal);
        Assert.DoesNotContain("TagOutcome", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildMaterializeMessage_normalizes_array_object_mismatch_without_path()
    {
        var ex = new InvalidOperationException(
            "The requested operation requires an element of type 'Object', but the target element has type 'Array'.")
        {
            Source = "System.Text.Json"
        };

        var message = CampaignJourneyMaterializeValidationHelper.BuildMaterializeMessage(ex);

        Assert.Contains("JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT", message, StringComparison.Ordinal);
        Assert.Contains("AndRule.Children", message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsStringEncodedShapeFailure_detects_received_String_JsonException()
    {
        var ex = new JsonException(
            "Cannot deserialize RuleBase: expected a JSON object with discriminator property, but received String.");

        Assert.True(CampaignJourneyMaterializeValidationHelper.IsStringEncodedShapeFailure(ex));
        Assert.False(CampaignJourneyMaterializeValidationHelper.IsArrayObjectShapeFailure(ex));
    }

    [Fact]
    public void BuildMaterializeMessage_preserves_string_encoded_violation_without_tag_outcome_hint()
    {
        var ex = new JourneyMaterializePathException(
            JourneyRuleShapeRules.FormatStringEncodedViolation("journey/rules[0]/ruleJsonElement", "ruleJsonElement"),
            new JsonException("Cannot deserialize RuleBase: expected a JSON object, but received String."));

        var message = CampaignJourneyMaterializeValidationHelper.BuildMaterializeMessage(ex);

        Assert.Contains(JourneyRuleShapeRules.StringEncodedViolationCode, message, StringComparison.Ordinal);
        Assert.Contains("path=journey/rules[0]/ruleJsonElement", message, StringComparison.Ordinal);
        Assert.DoesNotContain("TagOutcome", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildMaterializeMessage_normalizes_string_shape_failure_without_path()
    {
        var ex = new JsonException(
            "Cannot deserialize RuleBase: expected a JSON object with discriminator property, but received String. Rule JSON was stringified; embed the rule as a JSON object, not a quoted string.");

        var message = CampaignJourneyMaterializeValidationHelper.BuildMaterializeMessage(ex);

        Assert.Contains(JourneyRuleShapeRules.StringEncodedViolationCode, message, StringComparison.Ordinal);
        Assert.Contains("not a JSON string", message, StringComparison.OrdinalIgnoreCase);
    }
}
