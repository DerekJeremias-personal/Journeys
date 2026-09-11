using System.Text.RegularExpressions;

using System.Text.Json;

namespace Journeys.CampaignAgent.Remediation;

public static class JourneysToolRemediationCatalog
{
    private static readonly Regex ViolationRegex = new(@"\[violation=(TIER_A_[A-Z0-9_]+)\]", RegexOptions.Compiled);
    private static readonly Regex FieldRegex = new(@"field=([A-Za-z0-9_]+)", RegexOptions.Compiled);

    public static AgentRemediationPayload? Build(string toolName, BackendToolFailure failure)
    {
        if (failure.ParseStatus != BackendToolFailureParseStatus.Success)
            return null;

        var matched = new List<string>();
        var hints = new List<RemediationHint>();
        var seenHintKeys = new HashSet<string>(StringComparer.Ordinal);

        if (IsUpsertCampaign(toolName))
            BuildUpsertCampaignHints(failure, matched, hints, seenHintKeys);
        else if (IsValidateCampaign(toolName))
            BuildValidateCampaignHints(failure, matched, hints, seenHintKeys);
        else if (IsProcessEvent(toolName))
            BuildProcessEventHints(failure, matched, hints, seenHintKeys);

        if (hints.Count == 0)
            return null;

        var retry = matched.Any(r =>
            !string.Equals(r, "generic_journey_validation", StringComparison.Ordinal)
            && !string.Equals(r, "opaque_mcp_invocation", StringComparison.Ordinal)
            && !string.Equals(r, "account_id_not_from_user", StringComparison.Ordinal)
            && !string.Equals(r, "validate_has_warnings", StringComparison.Ordinal));
        return new AgentRemediationPayload
        {
            Tool = toolName,
            RetryRecommended = retry,
            MatchedRules = matched.Distinct(StringComparer.Ordinal).ToList(),
            Hints = hints
        };
    }

    public static AgentRemediationPayload? TryBuildValidateAdvisory(string toolName, string? rawJson)
    {
        if (!IsValidateCampaign(toolName) || string.IsNullOrWhiteSpace(rawJson))
            return null;

        var matched = new List<string>();
        var hints = new List<RemediationHint>();
        var seenHintKeys = new HashSet<string>(StringComparer.Ordinal);

        if (!TryBuildValidateWarningsOnly(rawJson, matched, hints, seenHintKeys))
            return null;

        return new AgentRemediationPayload
        {
            Tool = toolName,
            RetryRecommended = false,
            MatchedRules = matched,
            Hints = hints
        };
    }

    private static void BuildUpsertCampaignHints(
        BackendToolFailure failure,
        List<string> matched,
        List<RemediationHint> hints,
        HashSet<string> seenHintKeys)
    {
        var hasJourneyValidationWithoutViolation = false;

        foreach (var (key, value) in failure.ValidationErrors)
        {
            if (TryAddCampaignJsonErrorHint(key, value, matched, hints, seenHintKeys))
                continue;

            var violationMatch = ViolationRegex.Match(value);
            if (violationMatch.Success)
            {
                var code = violationMatch.Groups[1].Value;
                TryAddTierAViolationHint(code, value, hints, matched, seenHintKeys);
                continue;
            }

            if (key.Equals("journey.materialize", StringComparison.OrdinalIgnoreCase))
            {
                matched.Add("journey_materialize");
                if (value.Contains("JOURNEY_SHAPE_RULE_JSON_STRING_ENCODED", StringComparison.Ordinal))
                {
                    matched.Add("journey_shape_rule_string");
                    AddHint(hints, seenHintKeys, new RemediationHint
                    {
                        Field = null,
                        Issue = "journey_shape_rule_string",
                        Action =
                            "ruleJsonElement must be an embedded JSON object with Kind — not a JSON string. " +
                            "Remove quotes and paste { Kind: \"...\", ... } directly. " +
                            "Mirror get_example_campaign governance skeleton."
                    });
                }
                else if (value.Contains("JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT", StringComparison.Ordinal))
                {
                    matched.Add("journey_shape_rule_array");
                    AddHint(hints, seenHintKeys, new RemediationHint
                    {
                        Field = null,
                        Issue = "journey_shape_rule_array",
                        Action =
                            "ruleJsonElement must be a single rule object with Kind — not an array. " +
                            "Wrap multiple rules: { Kind: \"AndRule\", Children: [ ... ] }. " +
                            "Or split into multiple RuleSet entries in journey.rules[]. " +
                            "Mirror get_example_campaign governance skeleton."
                    });
                }
                else
                {
                    AddHint(hints, seenHintKeys, new RemediationHint
                    {
                        Field = null,
                        Issue = "journey_materialize",
                        Action =
                            "If the error mentions enum conversion or an unknown enum value, call GetRulesEngineContractSummary and match the failing property to enumCatalog (valid strings and $type names) before retrying UpsertCampaign."
                    });
                }
                if (value.Contains("unrecognized type discriminator", StringComparison.OrdinalIgnoreCase))
                {
                    AddHint(hints, seenHintKeys, new RemediationHint
                    {
                        Field = null,
                        Issue = "journey_materialize_type_discriminator",
                        Action =
                            "Call GetRulesEngineContractSummary and match the failing property to typeDiscriminatorCatalog.jsonContexts; replace $type with a listed allowed value. "
                            + "For HistoricalValueProvider use SimpleCalculationProvider (not invented names like PointBalanceHistoricalValueProvider); "
                            + "nest PointBalanceProvider under instanceValueProvider or aggregationValueProvider for balance reads, then retry UpsertCampaign."
                    });
                }
                continue;
            }

            if (key.StartsWith("journey.validation.", StringComparison.OrdinalIgnoreCase))
                hasJourneyValidationWithoutViolation = true;

            if (key.StartsWith("journey.shape.", StringComparison.OrdinalIgnoreCase))
            {
                matched.Add("journey_shape");
                if (value.Contains("JOURNEY_SHAPE_RULE_JSON_STRING_ENCODED", StringComparison.Ordinal))
                {
                    matched.Add("journey_shape_rule_string");
                    AddHint(hints, seenHintKeys, new RemediationHint
                    {
                        Field = null,
                        Issue = "journey_shape_rule_string",
                        Action =
                            "ruleJsonElement must be an embedded JSON object with Kind — not a JSON string. " +
                            "Remove quotes and paste { Kind: \"...\", ... } directly. " +
                            "Mirror get_example_campaign governance skeleton."
                    });
                }
                else if (value.Contains("JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT", StringComparison.Ordinal))
                {
                    matched.Add("journey_shape_rule_array");
                    AddHint(hints, seenHintKeys, new RemediationHint
                    {
                        Field = null,
                        Issue = "journey_shape_rule_array",
                        Action =
                            "ruleJsonElement must be a single rule object with Kind — not an array. " +
                            "Wrap multiple rules: { Kind: \"AndRule\", Children: [ ... ] }. " +
                            "Or split into multiple RuleSet entries in journey.rules[]. " +
                            "Mirror get_example_campaign governance skeleton."
                    });
                }
                else
                {
                    AddHint(hints, seenHintKeys, new RemediationHint
                    {
                        Field = null,
                        Issue = "journey_shape",
                        Action = CampaignAgentGuidanceText.RuleSetWrapperShapeRemediationHint
                    });
                }
            }

            if (key.StartsWith("journey.navigation.", StringComparison.OrdinalIgnoreCase))
            {
                matched.Add("journey_navigation");
                AddHint(hints, seenHintKeys, new RemediationHint
                {
                    Field = null,
                    Issue = "journey_navigation",
                    Action = CampaignAgentGuidanceText.JourneyNavigationRemediationCatalogHint
                });
            }
        }

        if (hasJourneyValidationWithoutViolation && !matched.Any(m => m.StartsWith("TIER_A_", StringComparison.Ordinal)))
        {
            matched.Add("generic_journey_validation");
            AddHint(hints, seenHintKeys, new RemediationHint
            {
                Field = null,
                Issue = "journey_validation",
                Action =
                    "Fix each journey.validation error using ruleSet, nodeId, rulePath, kind, and field tokens in the message, then call UpsertCampaign again with the corrected journey JSON."
            });
        }
    }

    private static void BuildProcessEventHints(
        BackendToolFailure failure,
        List<string> matched,
        List<RemediationHint> hints,
        HashSet<string> seenHintKeys)
    {
        foreach (var (key, value) in failure.ValidationErrors)
        {
            var combined = $"{key} {value}";
            if (IsInvalidModelKey(key) || ReferencesWrapperMetadata(combined))
            {
                matched.Add("invalid_model_metadata");
                AddHint(hints, seenHintKeys, new RemediationHint
                {
                    Field = key.Contains("InvalidModel", StringComparison.OrdinalIgnoreCase) ? key : null,
                    Issue = "invalid_model_metadata",
                    Action =
                        "Fix the event model metadata in the EventModels phase: set Wrapper, NaturalKeySymbols, TimeOfOccurrence, and AccountXIdSymbol on modelMetaData via SaveModel, then retry ProcessEvent with the correct root payload."
                });
                continue;
            }

            if (IsInvalidPayloadKey(key))
            {
                matched.Add("invalid_payload_metadata");
                AddHint(hints, seenHintKeys, new RemediationHint
                {
                    Field = key,
                    Issue = "invalid_payload_metadata",
                    Action =
                        "Fix the event model meta-data referenced in the error (e.g. ProcessingType, Wrapper). Update the model in the EventModels phase, then retry ProcessEvent."
                });
                continue;
            }

            if (ReferencesLoyaltyAccountOrInvalidState(combined))
            {
                matched.Add("account_link_resolution");
                AddHint(hints, seenHintKeys, new RemediationHint
                {
                    Field = "processingContract.accountLink.symbolPath",
                    Issue = "account_link_resolution",
                    Action =
                        "Include the account link field at the ProcessEvent payload root using processingContract.accountLink.symbolPath from GetCampaignAssistantContext. Verify the external id value matches an existing loyalty account for AccountXIdSymbol."
                });
                continue;
            }

            matched.Add("process_event_field_validation");
            AddHint(hints, seenHintKeys, new RemediationHint
            {
                Field = key,
                Issue = "process_event_field_validation",
                Action = $"Fix ProcessEvent payload field '{key}': {value}"
            });
        }

        TryAddOpaqueMcpInvocation(failure, matched, hints, seenHintKeys);
        TryAddAccountIdNotFromUser(failure, matched, hints, seenHintKeys);

        if (hints.Count == 0 && !string.IsNullOrEmpty(failure.Message))
        {
            var message = failure.Message;
            if (ReferencesWrapperMetadata(message))
            {
                matched.Add("invalid_model_metadata");
                AddHint(hints, seenHintKeys, new RemediationHint
                {
                    Field = null,
                    Issue = "invalid_model_metadata",
                    Action =
                        "Fix the event model metadata in the EventModels phase: set Wrapper, NaturalKeySymbols, TimeOfOccurrence, and AccountXIdSymbol on modelMetaData via SaveModel, then retry ProcessEvent with the correct root payload."
                });
            }
            else if (ReferencesLoyaltyAccountOrInvalidState(message))
            {
                matched.Add("account_link_resolution");
                AddHint(hints, seenHintKeys, new RemediationHint
                {
                    Field = "processingContract.accountLink.symbolPath",
                    Issue = "account_link_resolution",
                    Action =
                        "Include the account link field at the ProcessEvent payload root using processingContract.accountLink.symbolPath from GetCampaignAssistantContext. Verify the external id value matches an existing loyalty account for AccountXIdSymbol."
                });
            }
        }
    }

    private static void TryAddTierAViolationHint(
        string code,
        string value,
        List<RemediationHint> hints,
        List<string> matched,
        HashSet<string> seenHintKeys)
    {
        matched.Add(code);
        var field = TryExtractField(value);
        var action = code switch
        {
            "TIER_A_DEPOSIT_MISSING_DOLLAR_AMOUNT_PROVIDER" =>
                "Add DollarAmountProvider on the cited DepositPoints outcome, then call UpsertCampaign again with the corrected journey JSON.",
            "TIER_A_DEPOSIT_MISSING_AFFECTED_PAT" or
            "TIER_A_SPEND_MISSING_AFFECTED_PAT" or
            "TIER_A_EXPIRE_MISSING_AFFECTED_PAT" =>
                CampaignAgentGuidanceText.JsonCasingAffectedPatHint + " " + CampaignAgentGuidanceText.JsonCasingAffectedPatKeyCheckHint,
            "TIER_A_OUTCOME_PAT_ALIAS_MISUSED" =>
                "Replace pointAccountTypeId with AffectedPointAccountTypeIds[] (PascalCase array of PAT GUIDs) on the cited DepositPointsOutcome, SpendPointsOutcome, or ExpirePointsOutcome.",
            "TIER_A_OUTCOME_PAT_NOT_FOUND" =>
                "Call list_point_account_types or use PointAccountManifest; fix the patId to a real tenant PAT — never invent ids.",
            "TIER_A_SPEND_MISSING_WITHDRAWAL_AMOUNT_PROVIDER" =>
                "Add WithdrawlAmountProvider on the cited SpendPoints outcome (spelling matches engine), then call UpsertCampaign again with the corrected journey JSON.",
            "TIER_A_OUTCOME_MISSING_EVENT_ID_PROVIDER" =>
                "Add EventIdProvider with a PathValueProvider or ConstantValueProvider on the cited outcome, then call UpsertCampaign again with the corrected journey JSON.",
            "TIER_A_COMPOSITE_EMPTY_CHILDREN" =>
                "Add at least one child rule to the cited AndRule/OrRule composite, then call UpsertCampaign again with the corrected journey JSON.",
            "TIER_A_NOT_RULE_CHILD_COUNT" =>
                "NotRule must have exactly one child rule. Fix the cited composite, then call UpsertCampaign again with the corrected journey JSON.",
            "TIER_A_UNKNOWN_TYPE_DISCRIMINATOR" =>
                "Call GetRulesEngineContractSummary and match the cited field to typeDiscriminatorCatalog.jsonContexts; replace $type with a listed allowed value. "
                + "For HistoricalValueProvider use SimpleCalculationProvider (not invented names like PointBalanceHistoricalValueProvider); "
                + "nest PointBalanceProvider under instanceValueProvider or aggregationValueProvider for balance reads, then call UpsertCampaign again.",
            "TIER_A_HISTORICAL_MISSING_VALUE_PROVIDER" =>
                "Call get_rule_pattern_recipes and use pattern historical-spend-threshold or historical-event-count: HistoricalRule requires historicalValueProvider.$type = SimpleCalculationProvider with temporalConstraint and instanceValueProvider, plus matching aggregationValueProvider with shared Id.",
            "TIER_A_MISSING_TYPE_DISCRIMINATOR" when value.Contains("historicalValueProvider", StringComparison.OrdinalIgnoreCase)
                || value.Contains("HistoricalValueProvider", StringComparison.Ordinal) =>
                "Call get_rule_pattern_recipes for HistoricalRule skeletons: historicalValueProvider.$type must be SimpleCalculationProvider (never PointBalanceProvider or invented composed names).",
            "TIER_A_MISSING_TYPE_DISCRIMINATOR" =>
                "Add $type on the cited provider object using GetRulesEngineContractSummary typeDiscriminatorCatalog for allowed values on that field, then call UpsertCampaign again.",
            "TIER_A_SIMPLE_RULE_MISSING_LEFT_PROVIDER" or
            "TIER_A_SIMPLE_RULE_MISSING_RIGHT_PROVIDER" or
            "TIER_A_SIMPLE_RULE_MISSING_EVALUATOR" =>
                CampaignAgentGuidanceText.JsonCasingPascalCaseRuleProvidersHint
                + " Complete the cited SimpleRule family with LeftProvider, RightProvider, and Evaluator, then re-validate.",
            _ when code.Contains("_MISSING_", StringComparison.Ordinal)
                   && code.Contains("_PROVIDER", StringComparison.Ordinal) =>
                CampaignAgentGuidanceText.JsonCasingPascalCaseRuleProvidersHint + " "
                + $"Add the required provider on the cited rule or outcome (see GetRulesEngineContractSummary criticalRows for {code}), then call UpsertCampaign again.",
            _ when code.StartsWith("TIER_A_", StringComparison.Ordinal) =>
                "Call GetRulesEngineContractSummary (or journeys://rules-engine/campaign-contract/v1) for Tier A rule/outcome required fields and violation codes. Fix the cited ruleSet, rulePath, kind, and field from the error, then call UpsertCampaign again.",
            _ => null
        };

        if (action is null)
            return;

        AddHint(hints, seenHintKeys, new RemediationHint
        {
            Field = field,
            Issue = code,
            Action = action
        });
    }

    private static string? TryExtractField(string value)
    {
        var match = FieldRegex.Match(value);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static void BuildValidateCampaignHints(
        BackendToolFailure failure,
        List<string> matched,
        List<RemediationHint> hints,
        HashSet<string> seenHintKeys)
    {
        if (TryBuildValidateWarningsOnly(failure.RawJson, matched, hints, seenHintKeys))
            return;

        BuildUpsertCampaignHints(failure, matched, hints, seenHintKeys);
    }

    private static bool TryBuildValidateWarningsOnly(
        string? rawJson,
        List<string> matched,
        List<RemediationHint> hints,
        HashSet<string> seenHintKeys)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return false;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("isValid", out var isValidEl) || isValidEl.ValueKind != JsonValueKind.True
                || !isValidEl.GetBoolean())
                return false;

            var warningCount = 0;
            if (root.TryGetProperty("summary", out var summary) && summary.TryGetProperty("warningCount", out var wc))
                warningCount = wc.GetInt32();

            if (warningCount <= 0)
                return false;

            matched.Add("validate_has_warnings");
            AddHint(hints, seenHintKeys, new RemediationHint
            {
                Field = null,
                Issue = "validate_has_warnings",
                Action =
                    "Validation passed with advisory warnings. Address top warnings, re-validate, then upsert unless the user explicitly asked to save now."
            });
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static bool IsValidateCampaign(string toolName) =>
        string.Equals(toolName, "ValidateCampaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "validate_campaign", StringComparison.OrdinalIgnoreCase);

    private static bool IsUpsertCampaign(string toolName) =>
        string.Equals(toolName, "UpsertCampaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "upsert_campaign", StringComparison.OrdinalIgnoreCase);

    private static bool IsProcessEvent(string toolName) =>
        string.Equals(toolName, "ProcessEvent", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "process_event", StringComparison.OrdinalIgnoreCase);

    private static bool TryAddCampaignJsonErrorHint(
        string key,
        string value,
        List<string> matched,
        List<RemediationHint> hints,
        HashSet<string> seenHintKeys)
    {
        if (!key.Equals("campaignJson", StringComparison.OrdinalIgnoreCase))
            return false;

        matched.Add("campaign_json_parse");
        var action = value.Contains("could not be converted to System.String", StringComparison.OrdinalIgnoreCase)
                     && value.Contains("events", StringComparison.OrdinalIgnoreCase)
            ? "CampaignDto.events must be a string[] of event model ids (e.g. \"events\": [\"<eventModelId>\"]), not objects. Keep modelMetaData on the campaign root."
            : "Fix campaignJson so it deserializes to CampaignDto: valid JSON syntax, events as string ids, PascalCase provider fields on journey rules (LeftProvider, Value, PropertyPath, AffectedPointAccountTypeIds, etc.); lowercase PropertyPath segment values from GetModel symbols.";

        AddHint(hints, seenHintKeys, new RemediationHint
        {
            Field = "campaignJson",
            Issue = "campaign_json_parse",
            Action = action
        });
        return true;
    }

    private static void TryAddOpaqueMcpInvocation(
        BackendToolFailure failure,
        List<string> matched,
        List<RemediationHint> hints,
        HashSet<string> seenHintKeys)
    {
        var message = failure.Message ?? failure.RawJson ?? "";
        if (!message.Contains("An error occurred invoking", StringComparison.OrdinalIgnoreCase))
            return;

        matched.Add("opaque_mcp_invocation");
        AddHint(hints, seenHintKeys, new RemediationHint
        {
            Field = null,
            Issue = "opaque_mcp_invocation",
            Action =
                "Structured error was hidden by the MCP host. Do not retry an identical payload. Check API logs, campaign Live status, event model deployment, and engine connectivity. If errors.processEvent is now present, use that message."
        });
    }

    private static void TryAddAccountIdNotFromUser(
        BackendToolFailure failure,
        List<string> matched,
        List<RemediationHint> hints,
        HashSet<string> seenHintKeys)
    {
        var combined = failure.Message ?? "";
        foreach (var (_, value) in failure.ValidationErrors)
            combined += " " + value;

        if (!ReferencesLoyaltyAccountOrInvalidState(combined)
            && !combined.Contains("Account not found", StringComparison.OrdinalIgnoreCase)
            && !combined.Contains("CUST", StringComparison.OrdinalIgnoreCase))
            return;

        if (combined.Contains("Account not found", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("NOTFOUND", StringComparison.OrdinalIgnoreCase)
            || InventedAccountPattern.IsMatch(combined))
        {
            matched.Add("account_id_not_from_user");
            AddHint(hints, seenHintKeys, new RemediationHint
            {
                Field = "processingContract.accountLink.symbolPath",
                Issue = "account_id_not_from_user",
                Action =
                    "Use the test account the user provided. Call get_account first to confirm it exists. Do not invent placeholder ids like CUST123."
            });
        }
    }

    private static readonly Regex InventedAccountPattern = new(
        @"\bCUST\d+\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool IsInvalidModelKey(string key) =>
        string.Equals(key, "InvalidModel", StringComparison.OrdinalIgnoreCase);

    private static bool IsInvalidPayloadKey(string key) =>
        string.Equals(key, "Invalid Payload", StringComparison.OrdinalIgnoreCase);

    private static bool ReferencesWrapperMetadata(string text) =>
        text.Contains("Wrapper", StringComparison.OrdinalIgnoreCase)
        || text.Contains("NaturalKeySymbols", StringComparison.OrdinalIgnoreCase)
        || text.Contains("TimeOfOccurrence", StringComparison.OrdinalIgnoreCase)
        || text.Contains("AccountXIdSymbol", StringComparison.OrdinalIgnoreCase)
        || text.Contains("RuleState wrapper", StringComparison.OrdinalIgnoreCase)
        || text.Contains("wrapper and set", StringComparison.OrdinalIgnoreCase);

    private static bool ReferencesLoyaltyAccountOrInvalidState(string text) =>
        text.Contains("loyalty account", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Loyalty Account", StringComparison.Ordinal)
        || text.Contains("invalid state", StringComparison.OrdinalIgnoreCase)
        || text.Contains("External Reference", StringComparison.OrdinalIgnoreCase)
        || text.Contains("External ID", StringComparison.OrdinalIgnoreCase)
        || text.Contains("NOTFOUND", StringComparison.OrdinalIgnoreCase);

    private static void AddHint(List<RemediationHint> hints, HashSet<string> seen, RemediationHint hint)
    {
        var dedupeKey = $"{hint.Issue}|{hint.Field}|{hint.Limit}";
        if (!seen.Add(dedupeKey))
            return;
        hints.Add(hint);
    }
}
