using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.CampaignAgent.Remediation;

public class BackendModelValidationRemediationCatalogTests
{
    [Fact]
    public void Build_symbol_max_length_from_field_key()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "SaveModel",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["attributes[1].symbol"] = "Symbol must not exceed 100 characters."
            }
        };

        var payload = BackendModelValidationRemediationCatalog.Build("SaveModel", failure);
        Assert.NotNull(payload);
        Assert.True(payload!.RetryRecommended);
        Assert.Contains("symbol_max_length_100", payload.MatchedRules);
        Assert.Equal(100, payload.Hints[0].Limit);
    }

    [Fact]
    public void Build_missing_eventable_tag_from_model_shape_in_raw_json()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "SaveModel",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>(),
            RawJson = """
                {
                  "id": "m1",
                  "name": "Order",
                  "modelMetaData": {
                    "Wrapper": "w1",
                    "ProcessingType": "Engine"
                  }
                }
                """
        };

        var payload = BackendModelValidationRemediationCatalog.Build("SaveModel", failure);
        Assert.NotNull(payload);
        Assert.Contains("missing_eventable_tag", payload!.MatchedRules);
        Assert.Contains(payload.Hints, h => h.Field == "tag");
    }

    [Fact]
    public void Build_generic_when_unrecognized_field_errors()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "SaveModel",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string> { ["foo"] = "bar" }
        };

        var payload = BackendModelValidationRemediationCatalog.Build("SaveModel", failure);
        Assert.NotNull(payload);
        Assert.False(payload!.RetryRecommended);
        Assert.Contains("generic_validation", payload.MatchedRules);
    }

    [Fact]
    public void Build_natural_key_symbols_invalid()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "SaveModel",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["modelMetaData.NaturalKeySymbols"] = "Invalid JSON array"
            }
        };

        var payload = BackendModelValidationRemediationCatalog.Build("SaveModel", failure);
        Assert.NotNull(payload);
        Assert.Contains("natural_key_symbols_invalid", payload!.MatchedRules);
    }

    [Fact]
    public void Build_argument_null_key_from_message()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "save_model",
            ParseStatus = BackendToolFailureParseStatus.Success,
            Message = "System.ArgumentNullException: Value cannot be null. (Parameter 'key')"
        };

        var payload = BackendModelValidationRemediationCatalog.Build("save_model", failure);

        Assert.NotNull(payload);
        Assert.Contains("argument_null_key", payload!.MatchedRules);
        Assert.True(payload.RetryRecommended);
        Assert.Contains("tenantId", payload.Hints[0].Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_SaveModel_KeyValueElementTypeRequired_ReturnsHint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "save_model",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["attributes[2].keyValueAttributeDataType"] =
                    "KeyValue attribute element type (keyValueAttributeDataType) is required."
            }
        };

        var payload = BackendModelValidationRemediationCatalog.Build("save_model", failure);

        Assert.NotNull(payload);
        Assert.Contains("key_value_element_type_required", payload!.MatchedRules);
        Assert.True(payload.RetryRecommended);
        Assert.Contains(payload.Hints, h =>
            h.Action.Contains("keyValueAttributeDataType", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_wrapper_validation_uses_build_event_wrapper_hint()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "save_model",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["wrapperContractValidation[0].errors[0]"] = "Wrapper contract invalid: providerstates missing modelId."
            }
        };

        var payload = BackendModelValidationRemediationCatalog.Build("save_model", failure);

        Assert.NotNull(payload);
        Assert.Contains("wrapper_validation_factory_repair", payload!.MatchedRules);
        Assert.Contains(payload.Hints, h =>
            h.Action.Contains("build_event_wrapper", StringComparison.OrdinalIgnoreCase));
    }
}
