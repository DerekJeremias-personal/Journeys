using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.CampaignAgent.Remediation;

public class BackendModelValidationRemediationCatalogModelIdTests
{
    [Fact]
    public void Build_returns_hint_for_attribute_model_id_required()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "SaveModel",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["attributes[2].modelId"] =
                    "Model Id must be provided and valid when adding a model attribute of type Object."
            }
        };

        var payload = BackendModelValidationRemediationCatalog.Build("SaveModel", failure);

        Assert.NotNull(payload);
        Assert.Contains(payload!.MatchedRules, r => r == "attribute_model_id_required");
        Assert.Contains(payload.Hints, h =>
            h.Action.Contains("modelId", StringComparison.OrdinalIgnoreCase)
            && h.Action.Contains("modelType", StringComparison.OrdinalIgnoreCase));
    }
}
