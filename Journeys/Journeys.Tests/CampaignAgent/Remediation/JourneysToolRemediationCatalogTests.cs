using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.CampaignAgent.Remediation;

public class JourneysToolRemediationCatalogTests
{
    [Fact]
    public void Build_upsert_array_ruleJsonElement_emits_and_rule_hint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "upsert_campaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.shape.0"] =
                    "[violation=JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT] path=journey/rules[0]/ruleJsonElement — test"
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("upsert_campaign", failure);

        Assert.NotNull(payload);
        Assert.Contains(payload!.Hints, h =>
            h.Action.Contains("AndRule", StringComparison.Ordinal)
            && h.Action.Contains("ruleJsonElement", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_upsert_string_ruleJsonElement_emits_unstringify_hint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "upsert_campaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.shape.0"] =
                    "[violation=JOURNEY_SHAPE_RULE_JSON_STRING_ENCODED] path=journey/rules[0]/ruleJsonElement — test"
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("upsert_campaign", failure);

        Assert.NotNull(payload);
        Assert.Contains(payload!.Hints, h =>
            h.Issue == "journey_shape_rule_string"
            && (h.Action.Contains("stringify", StringComparison.OrdinalIgnoreCase)
                || h.Action.Contains("embedded JSON object", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Build_validate_campaign_journey_shape_reuses_upsert_hints()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "validate_campaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.shape.0"] = "journey.rules[] must contain RuleSet objects."
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("validate_campaign", failure);

        Assert.NotNull(payload);
        Assert.Contains("journey_shape", payload!.MatchedRules);
        Assert.True(payload.RetryRecommended);
        Assert.Contains("RuleSet", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_upsert_campaign_missing_affected_pat()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "upsert_campaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.validation.0"] =
                    "[violation=TIER_A_DEPOSIT_MISSING_AFFECTED_PAT] ruleSet=Earn field=AffectedPointAccountTypeIds"
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("upsert_campaign", failure);

        Assert.NotNull(payload);
        Assert.Contains(payload!.Hints, h =>
            h.Action.Contains("AffectedPointAccountTypeIds", StringComparison.Ordinal)
            && !h.Action.Contains("affectedPointAccountTypeIds:", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_tier_a_missing_left_provider_includes_pascal_case_hint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "validate_campaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.validation.0"] =
                    "[violation=TIER_A_SIMPLE_RULE_MISSING_LEFT_PROVIDER] ruleSet=Earn field=LeftProvider"
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("validate_campaign", failure)!;

        Assert.Contains(payload.Hints, h =>
            h.Action.Contains("LeftProvider", StringComparison.Ordinal)
            && h.Action.Contains("camelCase", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryBuildValidateAdvisory_warnings_only_retry_not_recommended()
    {
        const string json = """
        {
          "isValid": true,
          "summary": { "warningCount": 2 },
          "validation": { "warnings": [ { "code": "WARN_X" } ] }
        }
        """;

        var payload = JourneysToolRemediationCatalog.TryBuildValidateAdvisory("validate_campaign", json);

        Assert.NotNull(payload);
        Assert.Contains("validate_has_warnings", payload!.MatchedRules);
        Assert.False(payload.RetryRecommended);
    }

    [Fact]
    public void Build_upsert_campaign_deposit_missing_dollar_amount_provider()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "UpsertCampaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.validation.0"] =
                    "[violation=TIER_A_DEPOSIT_MISSING_DOLLAR_AMOUNT_PROVIDER] ruleSet=Main nodeId=n1 outcomeIndex=0 kind=DepositPoints field=DollarAmountProvider Rule set outcome [DepositPoints] #1: DollarAmountProvider is required."
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("UpsertCampaign", failure);

        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Hints);
        Assert.True(payload.RetryRecommended);
        Assert.Contains("TIER_A_DEPOSIT_MISSING_DOLLAR_AMOUNT_PROVIDER", payload.MatchedRules);
        Assert.Contains("dollarAmountProvider", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_upsert_campaign_outcome_missing_event_id_provider()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "UpsertCampaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.validation.0"] =
                    "[violation=TIER_A_OUTCOME_MISSING_EVENT_ID_PROVIDER] ruleSet=Main nodeId=n1 outcomeIndex=0 outcomeOrdinal=1 kind=DepositPoints field=EventIdProvider Rule set outcome [DepositPoints] #1: EventIdProvider is required."
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("UpsertCampaign", failure);

        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Hints);
        Assert.True(payload.RetryRecommended);
        Assert.Contains("TIER_A_OUTCOME_MISSING_EVENT_ID_PROVIDER", payload.MatchedRules);
        Assert.Equal("EventIdProvider", payload.Hints[0].Field);
        Assert.Contains("eventIdProvider", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PathValueProvider", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_upsert_campaign_journey_navigation_hint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "upsert_campaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.navigation.0"] =
                    "[violation=JOURNEY_NAV_MISSING_FOR_RULE_NODE] path=journey/children[0]"
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("upsert_campaign", failure);

        Assert.NotNull(payload);
        Assert.Contains("journey_navigation", payload!.MatchedRules);
        Assert.Contains("PointBalanceProvider", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_upsert_campaign_journey_shape_hint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "upsert_campaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.shape.0"] =
                    "[violation=JOURNEY_SHAPE_FLAT_RULE_IN_RULESET_ARRAY] path=journey/rules[0]"
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("upsert_campaign", failure);

        Assert.NotNull(payload);
        Assert.Contains("journey_shape", payload!.MatchedRules);
        Assert.Contains("ruleJsonElement", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_upsert_campaign_journey_materialize_enum_hint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "upsert_campaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.materialize"] =
                    "The JSON value could not be converted to Journeys.Core.RulesEngine.Comparitors.Enums.NumEvalType."
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("upsert_campaign", failure);

        Assert.NotNull(payload);
        Assert.Contains("journey_materialize", payload!.MatchedRules);
        Assert.Contains("enumCatalog", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_upsert_campaign_unknown_type_discriminator_hint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "UpsertCampaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.validation.0"] =
                    "[violation=TIER_A_UNKNOWN_TYPE_DISCRIMINATOR] ruleSet=Earn nodeId=n1 rulePath=RuleTree/HistoricalRule field=HistoricalValueProvider $type=PointBalanceHistoricalValueProvider allowed=SimpleCalculationProvider"
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("UpsertCampaign", failure);

        Assert.NotNull(payload);
        Assert.Contains("TIER_A_UNKNOWN_TYPE_DISCRIMINATOR", payload!.MatchedRules);
        Assert.Equal("HistoricalValueProvider", payload.Hints[0].Field);
        Assert.Contains("typeDiscriminatorCatalog", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SimpleCalculationProvider", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PointBalanceProvider", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_upsert_campaign_missing_type_discriminator_hint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "UpsertCampaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.validation.0"] =
                    "[violation=TIER_A_MISSING_TYPE_DISCRIMINATOR] ruleSet=Earn nodeId=n1 rulePath=RuleTree/NumericPropertyRule field=LeftProvider — provider object requires $type."
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("UpsertCampaign", failure);

        Assert.NotNull(payload);
        Assert.Contains("TIER_A_MISSING_TYPE_DISCRIMINATOR", payload!.MatchedRules);
        Assert.Equal("LeftProvider", payload.Hints[0].Field);
        Assert.Contains("typeDiscriminatorCatalog", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$type", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_upsert_campaign_journey_materialize_type_discriminator_hint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "upsert_campaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.materialize"] =
                    "Read unrecognized type discriminator id 'PointBalanceHistoricalValueProvider'."
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("upsert_campaign", failure);

        Assert.NotNull(payload);
        Assert.Contains("journey_materialize", payload!.MatchedRules);
        Assert.Equal(2, payload.Hints.Count);
        Assert.Contains("enumCatalog", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("typeDiscriminatorCatalog", payload.Hints[1].Action, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SimpleCalculationProvider", payload.Hints[1].Action, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PointBalanceProvider", payload.Hints[1].Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_process_event_invalid_model_event_models_hint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "ProcessEvent",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["InvalidModel"] =
                    "OrderEvent is not correctly configured to process within the Rules Engine, please create the necessary RuleState wrapper and set the Wrapper Meta-Data property with the Wrapper Id on the model."
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("ProcessEvent", failure);

        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Hints);
        Assert.Contains("invalid_model_metadata", payload.MatchedRules);
        Assert.Contains("EventModels", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_process_event_opaque_mcp_invocation_hint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "process_event",
            ParseStatus = BackendToolFailureParseStatus.Success,
            Message = "An error occurred invoking 'process_event'."
        };

        var payload = JourneysToolRemediationCatalog.Build("process_event", failure);

        Assert.NotNull(payload);
        Assert.Contains("opaque_mcp_invocation", payload!.MatchedRules);
        Assert.False(payload.RetryRecommended);
    }

    [Fact]
    public void Build_process_event_field_validation_errors_produce_hints()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "process_event",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["reviewdate"] = "Review date is required.",
                ["numberofstars"] = "Value must be a number."
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("process_event", failure);

        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Hints);
        Assert.True(payload.RetryRecommended);
    }

    [Fact]
    public void Build_upsert_campaign_historical_missing_value_provider_references_pattern_recipes()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "upsert_campaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["journey.validation.0"] =
                    "[violation=TIER_A_HISTORICAL_MISSING_VALUE_PROVIDER] ruleSet=Spend rulePath= ruleKind=HistoricalRule field=HistoricalValueProvider"
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("upsert_campaign", failure);

        Assert.NotNull(payload);
        Assert.Contains(payload!.Hints, h =>
            h.Action.Contains("get_rule_pattern_recipes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_process_event_account_not_found_hint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "ProcessEvent",
            ParseStatus = BackendToolFailureParseStatus.Success,
            Message = "Account not found: tenantId=primo, accountId=CUST123"
        };

        var payload = JourneysToolRemediationCatalog.Build("ProcessEvent", failure);

        Assert.NotNull(payload);
        Assert.Contains("account_id_not_from_user", payload!.MatchedRules);
        Assert.Contains("CUST123", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
    }
}
