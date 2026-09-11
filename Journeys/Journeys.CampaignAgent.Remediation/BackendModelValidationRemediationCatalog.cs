namespace Journeys.CampaignAgent.Remediation;

public static class BackendModelValidationRemediationCatalog
{
    public static AgentRemediationPayload? Build(string toolName, BackendToolFailure failure)
    {
        if (failure.ParseStatus != BackendToolFailureParseStatus.Success)
            return null;

        var matched = new List<string>();
        var hints = new List<RemediationHint>();
        var seenHintKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, value) in failure.ValidationErrors)
        {
            TryAddWrapperValidationFactoryHint(key, value, hints, matched, seenHintKeys);
            TryAddSymbolMaxLength(key, value, hints, matched, seenHintKeys);
            TryAddMissingType(key, value, hints, matched, seenHintKeys);
            TryAddNaturalKeySymbols(key, value, hints, matched, seenHintKeys);
            TryAddMissingAttributeModelId(key, value, hints, matched, seenHintKeys);
            TryAddMissingElementType(key, value, hints, matched, seenHintKeys);
        }

        TryAddMissingEventableTag(failure.RawJson, hints, matched, seenHintKeys);

        TryAddArgumentNullKey(failure, hints, matched, seenHintKeys);
        TryAddOpaqueMcpInvocation(failure, hints, matched, seenHintKeys);

        if (failure.ValidationErrors.Count == 0 && !string.IsNullOrEmpty(failure.Message))
        {
            TryAddSymbolMaxLengthFromMessage(failure.Message, hints, matched, seenHintKeys);
            TryAddMissingTypeFromMessage(failure.Message, hints, matched, seenHintKeys);
            TryAddNaturalKeySymbolsFromMessage(failure.Message, hints, matched, seenHintKeys);
        }

        if (hints.Count == 0 && failure.ValidationErrors.Count > 0)
        {
            matched.Add("generic_validation");
            AddHint(hints, seenHintKeys, new RemediationHint
            {
                Field = null,
                Issue = "validation",
                Action =
                    "Fix each field listed in errors or validationErrors, then call SaveModel again with the corrected model JSON."
            });
        }

        if (hints.Count == 0)
            return null;

        var retry = matched.Any(r =>
            !string.Equals(r, "generic_validation", StringComparison.Ordinal)
            && !string.Equals(r, "opaque_mcp_invocation", StringComparison.Ordinal));
        return new AgentRemediationPayload
        {
            Tool = toolName,
            RetryRecommended = retry,
            MatchedRules = matched.Distinct(StringComparer.Ordinal).ToList(),
            Hints = hints
        };
    }

    private static void TryAddSymbolMaxLength(string key, string value, List<RemediationHint> hints, List<string> matched, HashSet<string> seen)
    {
        if (!LooksLikeSymbolLengthViolation(key, value))
            return;

        matched.Add("symbol_max_length_100");
        var field = key.Contains("symbol", StringComparison.OrdinalIgnoreCase) ? key : null;
        AddHint(hints, seen, new RemediationHint
        {
            Field = field,
            Issue = "max_length",
            Limit = 100,
            Action =
                "Shorten this symbol to at most 100 characters. Keep symbols stable for rules and event payloads. Call SaveModel again with the corrected model JSON (same model id/name)."
        });
    }

    private static void TryAddSymbolMaxLengthFromMessage(string message, List<RemediationHint> hints, List<string> matched, HashSet<string> seen)
    {
        if (!LooksLikeSymbolLengthViolation(null, message))
            return;

        matched.Add("symbol_max_length_100");
        AddHint(hints, seen, new RemediationHint
        {
            Field = null,
            Issue = "max_length",
            Limit = 100,
            Action =
                "Shorten the offending attribute symbol to at most 100 characters, then call SaveModel again with the corrected model JSON."
        });
    }

    private static bool LooksLikeSymbolLengthViolation(string? key, string value)
    {
        var combined = $"{key} {value}";
        if (!combined.Contains("symbol", StringComparison.OrdinalIgnoreCase))
            return false;

        return combined.Contains("100", StringComparison.Ordinal)
               || combined.Contains("exceed", StringComparison.OrdinalIgnoreCase)
               || combined.Contains("max length", StringComparison.OrdinalIgnoreCase)
               || combined.Contains("maximum length", StringComparison.OrdinalIgnoreCase)
               || combined.Contains("too long", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryAddMissingType(string key, string value, List<RemediationHint> hints, List<string> matched, HashSet<string> seen)
    {
        if (!LooksLikeMissingType(key, value))
            return;

        matched.Add("attribute_missing_type");
        AddHint(hints, seen, new RemediationHint
        {
            Field = key.Contains("type", StringComparison.OrdinalIgnoreCase) ? key : null,
            Issue = "missing_type",
            Action =
                "Every attribute in the model JSON must include a type field. Add type on each attribute, then call SaveModel again."
        });
    }

    private static void TryAddMissingTypeFromMessage(string message, List<RemediationHint> hints, List<string> matched, HashSet<string> seen)
    {
        if (!LooksLikeMissingType(null, message))
            return;

        matched.Add("attribute_missing_type");
        AddHint(hints, seen, new RemediationHint
        {
            Field = null,
            Issue = "missing_type",
            Action =
                "Every attribute must include type. If the tool returned requiresTypeConfirmation, confirm types with the user before SaveModel."
        });
    }

    private static bool LooksLikeMissingType(string? key, string value)
    {
        var combined = $"{key} {value}";
        return combined.Contains("requiresTypeConfirmation", StringComparison.OrdinalIgnoreCase)
               || (combined.Contains("type", StringComparison.OrdinalIgnoreCase)
                   && combined.Contains("attribute", StringComparison.OrdinalIgnoreCase));
    }

    private static void TryAddWrapperValidationFactoryHint(
        string key,
        string value,
        List<RemediationHint> hints,
        List<string> matched,
        HashSet<string> seen)
    {
        if (!LooksLikeWrapperValidationIssue(key, value))
            return;

        matched.Add("wrapper_validation_factory_repair");
        AddHint(hints, seen, new RemediationHint
        {
            Field = string.IsNullOrWhiteSpace(key) ? null : key,
            Issue = "wrapper_validation",
            Action =
                "Repair wrapper contract issues with build_event_wrapper (include eventModelId and wrapperModelId when available), then call SaveModel with the generated wrapper JSON."
        });
    }

    private static bool LooksLikeWrapperValidationIssue(string? key, string value)
    {
        var combined = $"{key} {value}";
        if (!combined.Contains("wrapper", StringComparison.OrdinalIgnoreCase))
            return false;

        return combined.Contains("wrapperContractValidation", StringComparison.OrdinalIgnoreCase)
               || combined.Contains("wrapper contract", StringComparison.OrdinalIgnoreCase)
               || combined.Contains("AndRuleState", StringComparison.OrdinalIgnoreCase)
               || combined.Contains("providerstates", StringComparison.OrdinalIgnoreCase)
               || combined.Contains("journeystates", StringComparison.OrdinalIgnoreCase)
               || combined.Contains("outcomestates", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryAddMissingAttributeModelId(string key, string value, List<RemediationHint> hints, List<string> matched, HashSet<string> seen)
    {
        if (!LooksLikeMissingAttributeModelId(key, value))
            return;

        matched.Add("attribute_model_id_required");
        AddHint(hints, seen, new RemediationHint
        {
            Field = key.Contains("modelId", StringComparison.OrdinalIgnoreCase) ? key : null,
            Issue = "missing_attribute_model_id",
            Action =
                "Object-typed attribute refs require modelId and modelType. Add both on the cited attribute, then call SaveModel again. For wrapper engine fields (providerstates, journeystates, outcomestates), copy modelIds from an existing *AndRuleState wrapper in this tenant via GetModel."
        });
    }

    private static bool LooksLikeMissingAttributeModelId(string? key, string value)
    {
        var combined = $"{key} {value}";
        return combined.Contains("modelId", StringComparison.OrdinalIgnoreCase)
               && combined.Contains("Model Id must be provided", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryAddMissingElementType(
        string key,
        string value,
        List<RemediationHint> hints,
        List<string> matched,
        HashSet<string> seen)
    {
        if (TryAddKeyValueElementType(key, value, hints, matched, seen))
            return;
        if (TryAddListElementType(key, value, hints, matched, seen))
            return;
        TryAddPrimitiveDataType(key, value, hints, matched, seen);
    }

    private static bool TryAddKeyValueElementType(
        string key,
        string value,
        List<RemediationHint> hints,
        List<string> matched,
        HashSet<string> seen)
    {
        if (!LooksLikeMissingKeyValueElementType(key, value))
            return false;

        matched.Add("key_value_element_type_required");
        AddHint(hints, seen, new RemediationHint
        {
            Field = key.Contains("keyValueAttributeDataType", StringComparison.OrdinalIgnoreCase) ? key : null,
            Issue = "missing_key_value_element_type",
            Action =
                "KeyValue attributes require keyValueAttributeDataType (usually \"Object\" for wrapper engine fields like providerstates and journeystates). Add the field on the cited attribute, then call SaveModel again."
        });
        return true;
    }

    private static bool TryAddListElementType(
        string key,
        string value,
        List<RemediationHint> hints,
        List<string> matched,
        HashSet<string> seen)
    {
        if (!LooksLikeMissingListElementType(key, value))
            return false;

        matched.Add("list_element_type_required");
        AddHint(hints, seen, new RemediationHint
        {
            Field = key.Contains("listAttributeDataType", StringComparison.OrdinalIgnoreCase) ? key : null,
            Issue = "missing_list_element_type",
            Action =
                "List attributes require listAttributeDataType (element type, e.g. \"String\" or \"Object\"). Add the field on the cited attribute, then call SaveModel again."
        });
        return true;
    }

    private static bool TryAddPrimitiveDataType(
        string key,
        string value,
        List<RemediationHint> hints,
        List<string> matched,
        HashSet<string> seen)
    {
        if (!LooksLikeMissingPrimitiveDataType(key, value))
            return false;

        matched.Add("attribute_data_type_required");
        AddHint(hints, seen, new RemediationHint
        {
            Field = key.EndsWith(".dataType", StringComparison.OrdinalIgnoreCase) ? key : null,
            Issue = "missing_primitive_data_type",
            Action =
                "Primitive attributes require dataType (e.g. \"string\", \"date\", \"number\"). Add dataType on the cited attribute, then call SaveModel again."
        });
        return true;
    }

    private static bool LooksLikeMissingKeyValueElementType(string? key, string value)
    {
        var combined = $"{key} {value}";
        return combined.Contains("keyValueAttributeDataType", StringComparison.OrdinalIgnoreCase)
               || combined.Contains("KEY_VALUE_ELEMENT_TYPE_REQUIRED", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeMissingListElementType(string? key, string value)
    {
        var combined = $"{key} {value}";
        return combined.Contains("listAttributeDataType", StringComparison.OrdinalIgnoreCase)
               || combined.Contains("LIST_ELEMENT_TYPE_REQUIRED", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeMissingPrimitiveDataType(string? key, string value)
    {
        var combined = $"{key} {value}";
        return (key != null && key.EndsWith(".dataType", StringComparison.OrdinalIgnoreCase)
                && combined.Contains("data type is required", StringComparison.OrdinalIgnoreCase))
               || combined.Contains("ATTRIBUTE_DATA_TYPE_REQUIRED", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryAddNaturalKeySymbols(string key, string value, List<RemediationHint> hints, List<string> matched, HashSet<string> seen)
    {
        if (!combinedReferencesNaturalKeySymbols(key, value))
            return;

        matched.Add("natural_key_symbols_invalid");
        AddHint(hints, seen, new RemediationHint
        {
            Field = key.Contains("NaturalKeySymbols", StringComparison.OrdinalIgnoreCase) ? key : "modelMetaData.NaturalKeySymbols",
            Issue = "invalid_natural_key_symbols",
            Action =
                "Set modelMetaData.NaturalKeySymbols to a JSON array string of attribute symbols (e.g. \"[\\\"orderId\\\"]\"). Each symbol must exist on the event model."
        });
    }

    private static void TryAddNaturalKeySymbolsFromMessage(string message, List<RemediationHint> hints, List<string> matched, HashSet<string> seen)
    {
        if (!combinedReferencesNaturalKeySymbols(null, message))
            return;

        matched.Add("natural_key_symbols_invalid");
        AddHint(hints, seen, new RemediationHint
        {
            Field = "modelMetaData.NaturalKeySymbols",
            Issue = "invalid_natural_key_symbols",
            Action =
                "Fix NaturalKeySymbols in modelMetaData: valid JSON array string; symbols must be declared on the model."
        });
    }

    private static bool combinedReferencesNaturalKeySymbols(string? key, string value)
    {
        var combined = $"{key} {value}";
        return combined.Contains("NaturalKeySymbols", StringComparison.OrdinalIgnoreCase)
               && (combined.Contains("json", StringComparison.OrdinalIgnoreCase)
                   || combined.Contains("parse", StringComparison.OrdinalIgnoreCase)
                   || combined.Contains("invalid", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddHint(List<RemediationHint> hints, HashSet<string> seen, RemediationHint hint)
    {
        var dedupeKey = $"{hint.Issue}|{hint.Field}|{hint.Limit}";
        if (!seen.Add(dedupeKey))
            return;
        hints.Add(hint);
    }

    private static void TryAddMissingEventableTag(
        string? rawJson,
        List<RemediationHint> hints,
        List<string> matched,
        HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return;

            var root = doc.RootElement;
            if (!root.TryGetProperty("modelMetaData", out var meta) || meta.ValueKind != System.Text.Json.JsonValueKind.Object)
                return;

            var looksEngine = meta.TryGetProperty("Wrapper", out _)
                              || (meta.TryGetProperty("ProcessingType", out var pt)
                                  && pt.ValueKind == System.Text.Json.JsonValueKind.String
                                  && pt.GetString()?.Contains("Engine", StringComparison.OrdinalIgnoreCase) == true);

            if (!looksEngine)
                return;

            var tag = root.TryGetProperty("tag", out var tagEl) && tagEl.ValueKind == System.Text.Json.JsonValueKind.String
                ? tagEl.GetString()
                : null;

            if (string.Equals(tag, "eventable", StringComparison.Ordinal))
                return;

            matched.Add("missing_eventable_tag");
            AddHint(hints, seen, new RemediationHint
            {
                Field = "tag",
                Issue = "missing_eventable_tag",
                Action =
                    "Set \"tag\": \"eventable\" on this event payload model so Journeys ProcessEvent can process it and it may appear in Campaign.Events."
            });
        }
        catch (System.Text.Json.JsonException)
        {
            /* ignore */
        }
    }

    private static void TryAddArgumentNullKey(
        BackendToolFailure failure,
        List<RemediationHint> hints,
        List<string> matched,
        HashSet<string> seen)
    {
        var combined = failure.Message ?? "";
        foreach (var (_, value) in failure.ValidationErrors)
            combined += " " + value;

        if (!combined.Contains("ArgumentNullException", StringComparison.OrdinalIgnoreCase)
            || !combined.Contains("key", StringComparison.OrdinalIgnoreCase))
            return;

        matched.Add("argument_null_key");
        AddHint(hints, seen, new RemediationHint
        {
            Field = null,
            Issue = "argument_null_key",
            Action =
                "SaveModel create payload is incomplete. Include tenantId, name, modelType, modelVersion, isContainer, isPerTenancy, partitionKey1Symbol, partitionKey2Symbol, and attributes (each with type). See EventModels governance — SaveModel minimum create DTO."
        });
    }

    private static void TryAddOpaqueMcpInvocation(
        BackendToolFailure failure,
        List<RemediationHint> hints,
        List<string> matched,
        HashSet<string> seen)
    {
        var message = failure.Message ?? failure.RawJson ?? "";
        if (!message.Contains("An error occurred invoking", StringComparison.OrdinalIgnoreCase))
            return;

        matched.Add("opaque_mcp_invocation");
        AddHint(hints, seen, new RemediationHint
        {
            Field = null,
            Issue = "opaque_mcp_invocation",
            Action =
                "SaveModel failed inside the MCP host without structured validation. Check API logs and ensure the minimum create DTO fields are present before retrying."
        });
    }
}
