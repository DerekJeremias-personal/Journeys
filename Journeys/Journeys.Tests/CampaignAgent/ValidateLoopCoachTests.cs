using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class ValidateLoopCoachTests
{
    [Theory]
    [InlineData("JOURNEY_SHAPE_NODES_NOT_CHILDREN", "JOURNEY_CHILDREN_NOT_NODES")]
    [InlineData("journey.navigation.Entry required at root", "JOURNEY_NAV_ROOT_ENTRY_REQUIRED")]
    [InlineData("JOURNEY_NAV_TIER_TRANSITION_REQUIRED on tier Bronze", "JOURNEY_NAV_TIER_TRANSITION_REQUIRED")]
    public void Map_journey_shape_and_nav_codes(string error, string expectedCode)
    {
        Assert.Equal(expectedCode, ValidateLoopCoach.MapViolationCode(error));
    }

    [Theory]
    [InlineData("JsonException: $.events[0]", "EVENTS_ARRAY_STRING_IDS")]
    [InlineData("leftProvider must be PascalCase", "RULE_JSON_PASCAL_CASE")]
    [InlineData("TIER_A_OUTCOME_PAT_ALIAS", "AFFECTED_PAT_ALIAS")]
    [InlineData("journey.nodes[] invalid", "JOURNEY_CHILDREN_NOT_NODES")]
    [InlineData("ruleSetCount is 0", "RULESET_COUNT_ZERO")]
    [InlineData("InvalidCastException discounts", "PROCESS_EVENT_DISCOUNTS_LIST")]
    [InlineData("InvalidCastException sku taxonomy", "PROCESS_EVENT_SKU_TAXONOMY")]
    [InlineData("TIER_A_UNKNOWN_RULE_KIND rulePath=navigation.Entry Kind=AlwaysTrueRule", "ALWAYS_TRUE_RULE_ANTIPATTERN")]
    [InlineData("Kind=AlwaysTrueRule not allowed", "ALWAYS_TRUE_RULE_ANTIPATTERN")]
    [InlineData("Unsupported type", "DEPOSIT_OUTCOME_UNSUPPORTED_TYPE")]
    [InlineData("SimpleCalculation only supports generation of HistoricalStateBase", "DEPOSIT_SIMPLECALC_ANTIPATTERN")]
    [InlineData("StaleRequest timestamp stale", "PROCESS_EVENT_STALE_REQUEST")]
    [InlineData("Loyalty Account with External Reference of x was not found", "VERIFY_ACCOUNT_NOT_FOUND")]
    [InlineData("Target campaign abc not found", "VERIFY_CAMPAIGN_NOT_FOUND")]
    public void Map_validate_error_to_code(string error, string expectedCode)
    {
        Assert.Equal(expectedCode, ValidateLoopCoach.MapViolationCode(error));
    }

    [Fact]
    public void TryGetHint_returns_coach_for_known_code()
    {
        var hint = ValidateLoopCoach.TryGetHint("EVENTS_ARRAY_STRING_IDS");
        Assert.NotNull(hint);
        Assert.Contains("events[]", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetHint_always_true_rule_mentions_simple_rule()
    {
        var hint = ValidateLoopCoach.TryGetHint("ALWAYS_TRUE_RULE_ANTIPATTERN");
        Assert.NotNull(hint);
        Assert.Contains("SimpleRule", hint!, StringComparison.Ordinal);
        Assert.Contains("AlwaysTrueRule", hint!, StringComparison.Ordinal);
    }

    [Fact]
    public void MapSalientFixLine_always_true_rule()
    {
        Assert.Equal("lastValidateFix: SimpleRule not AlwaysTrueRule",
            ValidateLoopCoach.MapSalientFixLine("ALWAYS_TRUE_RULE_ANTIPATTERN"));
    }
}
