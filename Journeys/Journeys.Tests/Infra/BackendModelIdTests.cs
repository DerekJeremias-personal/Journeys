using Journeys.Infra.Backend;

namespace Journeys.Tests.Infra;

public class BackendModelIdTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown")]
    [InlineData("Unknown")]
    public void Require_rejects_unusable_ids(string? modelId)
    {
        Assert.Throws<ArgumentException>(() => BackendModelId.Require(modelId));
    }

    [Fact]
    public void Require_accepts_campaign_model_guid()
    {
        const string campaignModelId = "eeae67ca-7bf9-4d2b-9131-83717b219a3a";
        Assert.Equal(campaignModelId, BackendModelId.Require(campaignModelId));
    }

    [Fact]
    public void Serialize_omits_null_model_id_and_never_emits_unknown()
    {
        var json = BackendRequestJson.Serialize(new
        {
            ModelId = (string?)null,
            ModelType = BackendModelId.LoyaltyModelType,
            PageSize = 10
        });

        Assert.DoesNotContain("unknown", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ModelId", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("loyalty", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_rejects_unknown_model_id()
    {
        Assert.Throws<ArgumentException>(() => BackendRequestJson.Serialize(new
        {
            ModelId = "unknown",
            ModelType = BackendModelId.LoyaltyModelType
        }));
    }

    [Fact]
    public void Catalog_list_body_has_loyalty_type_and_no_model_id()
    {
        var json = BackendRequestJson.Serialize(
            BackendRequestJson.CreateCatalogListBody("TestTenant1", 500, null));

        Assert.Contains("\"ModelType\":\"loyalty\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ModelId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unknown", json, StringComparison.OrdinalIgnoreCase);
    }
}
