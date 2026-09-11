namespace Journeys.Core.Utility;



/// <summary>

/// Maps validate/upsert/process_event failure text to BCP violation codes and coach hints.

/// </summary>

public static class ValidateLoopCoach

{

    public static string? MapViolationCode(string? errorText)

    {

        if (string.IsNullOrWhiteSpace(errorText))

            return null;



        var text = errorText;



        if (text.Contains("AlwaysTrueRule", StringComparison.OrdinalIgnoreCase))

            return "ALWAYS_TRUE_RULE_ANTIPATTERN";



        if (text.Contains("Unsupported type", StringComparison.OrdinalIgnoreCase))

            return "DEPOSIT_OUTCOME_UNSUPPORTED_TYPE";



        if (text.Contains("SimpleCalculation only supports", StringComparison.OrdinalIgnoreCase))

            return "DEPOSIT_SIMPLECALC_ANTIPATTERN";



        if (text.Contains("StaleRequest", StringComparison.OrdinalIgnoreCase))

            return "PROCESS_EVENT_STALE_REQUEST";



        if (text.Contains("Loyalty Account", StringComparison.OrdinalIgnoreCase)

            && text.Contains("not found", StringComparison.OrdinalIgnoreCase))

            return "VERIFY_ACCOUNT_NOT_FOUND";



        if (text.Contains("Target campaign", StringComparison.OrdinalIgnoreCase)

            && text.Contains("not found", StringComparison.OrdinalIgnoreCase))

            return "VERIFY_CAMPAIGN_NOT_FOUND";



        if (text.Contains("$.events[0]", StringComparison.OrdinalIgnoreCase)

            || (text.Contains("events[0]", StringComparison.OrdinalIgnoreCase)

                && text.Contains("System.String", StringComparison.OrdinalIgnoreCase)))

            return "EVENTS_ARRAY_STRING_IDS";



        if (text.Contains("leftProvider", StringComparison.OrdinalIgnoreCase)

            || (text.Contains("LeftProvider", StringComparison.OrdinalIgnoreCase)

                && text.Contains("PascalCase", StringComparison.OrdinalIgnoreCase)))

            return "RULE_JSON_PASCAL_CASE";



        if (text.Contains("TIER_A_OUTCOME_PAT_ALIAS", StringComparison.OrdinalIgnoreCase)

            || (text.Contains("AffectedPointAccountTypeIds", StringComparison.OrdinalIgnoreCase)

                && text.Contains("alias", StringComparison.OrdinalIgnoreCase)))

            return "AFFECTED_PAT_ALIAS";



        if (text.Contains("JOURNEY_SHAPE_NODES_NOT_CHILDREN", StringComparison.OrdinalIgnoreCase)
            || text.Contains("nodes[]", StringComparison.OrdinalIgnoreCase)
            || text.Contains("journey.nodes", StringComparison.OrdinalIgnoreCase))
            return "JOURNEY_CHILDREN_NOT_NODES";

        if (text.Contains("JOURNEY_NAV_ROOT_ENTRY_REQUIRED", StringComparison.OrdinalIgnoreCase)
            || (text.Contains("journey.navigation", StringComparison.OrdinalIgnoreCase)
                && text.Contains("Entry", StringComparison.OrdinalIgnoreCase)
                && text.Contains("required", StringComparison.OrdinalIgnoreCase)))
            return "JOURNEY_NAV_ROOT_ENTRY_REQUIRED";

        if (text.Contains("JOURNEY_NAV_TIER_TRANSITION_REQUIRED", StringComparison.OrdinalIgnoreCase)
            || (text.Contains("navigation.Entry", StringComparison.OrdinalIgnoreCase)
                && text.Contains("tier", StringComparison.OrdinalIgnoreCase)))
            return "JOURNEY_NAV_TIER_TRANSITION_REQUIRED";



        if (text.Contains("ruleSetCount", StringComparison.OrdinalIgnoreCase)

            && (text.Contains('0') || text.Contains("zero", StringComparison.OrdinalIgnoreCase)))

            return "RULESET_COUNT_ZERO";



        if (text.Contains("InvalidCastException", StringComparison.OrdinalIgnoreCase)

            && text.Contains("discounts", StringComparison.OrdinalIgnoreCase))

            return "PROCESS_EVENT_DISCOUNTS_LIST";



        if (text.Contains("InvalidCastException", StringComparison.OrdinalIgnoreCase)

            && text.Contains("sku", StringComparison.OrdinalIgnoreCase))

            return "PROCESS_EVENT_SKU_TAXONOMY";



        return null;

    }



    public static string? TryGetHint(string? violationCode)

    {

        if (string.IsNullOrWhiteSpace(violationCode))

            return null;



        return violationCode switch

        {

            "EVENTS_ARRAY_STRING_IDS" =>

                "Coach: events[] must be string GUID ids; move modelMetaData to campaign root keyed by event id.",

            "RULE_JSON_PASCAL_CASE" =>

                "Coach: ruleJsonElement/outcomes use PascalCase Kind, LeftProvider, RightProvider, Evaluator.",

            "AFFECTED_PAT_ALIAS" =>

                "Coach: AffectedPointAccountTypeIds must be PAT GUIDs from PointAccountManifest — not aliases.",

            "JOURNEY_CHILDREN_NOT_NODES" =>

                "Coach: journey tree uses children[] — not nodes[].",

            "JOURNEY_NAV_ROOT_ENTRY_REQUIRED" =>

                "Coach: root journey.navigation.Entry with SimpleNavigationCriteria $type and SimpleRule navConstraint.",

            "JOURNEY_NAV_TIER_TRANSITION_REQUIRED" =>

                "Coach: tier nodes use navigation.Transition with AndRule + PointBalanceProvider — not navigation.Entry.",

            "RULESET_COUNT_ZERO" =>

                "Coach: journey.rules[] empty or wrong shape — author rule sets before upsert.",

            "PROCESS_EVENT_DISCOUNTS_LIST" =>

                "Coach: discounts must be [] (List) — never scalar 0 or null.",

            "PROCESS_EVENT_SKU_TAXONOMY" =>

                "Coach: items[].sku must be { extId: \"...\" } for ModelTaxonomy — not a bare string.",

            "ALWAYS_TRUE_RULE_ANTIPATTERN" =>

                "Coach: use Kind SimpleRule with BoolEvaluation — ConstantValueProvider true on LeftProvider; AlwaysTrueRule is not in the contract.",

            "DEPOSIT_OUTCOME_UNSUPPORTED_TYPE" =>

                "Coach: DepositPointsOutcome DollarAmountProvider must be PathValueProvider on event.ordertotal with PointsPerDollar — never AggregateValueProvider or SimpleCalculationProvider.",

            "DEPOSIT_SIMPLECALC_ANTIPATTERN" =>

                "Coach: SimpleCalculationProvider is for HistoricalRule only — use PathValueProvider + PointsPerDollar on DepositPointsOutcome.",

            "PROCESS_EVENT_STALE_REQUEST" =>

                "Coach: increment timeOfOccurrence by at least 1 second for each sequential process_event test.",

            "VERIFY_ACCOUNT_NOT_FOUND" =>

                "Coach: call get_account with an allowlisted test ext id from SESSION before process_event.",

            "VERIFY_CAMPAIGN_NOT_FOUND" =>

                "Coach: use campaignId entity Id from CreationSnapshot or latest upsert_campaign digest — not external ref alone.",

            "REVALIDATE_BEFORE_UPSERT" =>

                "Coach: validate_campaign failed — re-validate and fix all errors until IsValid before upsert_campaign.",

            _ => null

        };

    }



    public static string? MapSalientFixLine(string? violationCode) =>

        violationCode switch

        {

            "EVENTS_ARRAY_STRING_IDS" => "lastValidateFix: events[] string ids",

            "RULE_JSON_PASCAL_CASE" => "lastValidateFix: PascalCase rule keys",

            "AFFECTED_PAT_ALIAS" => "lastValidateFix: PAT GUIDs not aliases",

            "JOURNEY_CHILDREN_NOT_NODES" => "lastValidateFix: children not nodes",

            "JOURNEY_NAV_ROOT_ENTRY_REQUIRED" => "lastValidateFix: root navigation.Entry",

            "JOURNEY_NAV_TIER_TRANSITION_REQUIRED" => "lastValidateFix: tier Transition not Entry",

            "RULESET_COUNT_ZERO" => "lastValidateFix: RuleSetCount=0",

            "ALWAYS_TRUE_RULE_ANTIPATTERN" => "lastValidateFix: SimpleRule not AlwaysTrueRule",

            "DEPOSIT_OUTCOME_UNSUPPORTED_TYPE" => "lastValidateFix: PathValueProvider + PointsPerDollar",

            "DEPOSIT_SIMPLECALC_ANTIPATTERN" => "lastValidateFix: no SimpleCalculation on deposit",

            "PROCESS_EVENT_STALE_REQUEST" => "lastValidateFix: advance timeOfOccurrence",

            "VERIFY_ACCOUNT_NOT_FOUND" => "lastValidateFix: get_account first",

            "VERIFY_CAMPAIGN_NOT_FOUND" => "lastValidateFix: entity campaignId from upsert",

            "REVALIDATE_BEFORE_UPSERT" => "lastValidateFix: re-validate before upsert",

            _ => null

        };

}

