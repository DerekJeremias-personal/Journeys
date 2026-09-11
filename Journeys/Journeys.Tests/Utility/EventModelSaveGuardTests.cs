using System.Text.Json;
using Backend.Dto.Structures.Model;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventModelSaveGuardTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void LooksLikePatFabrication_tierQualPoints_metadata_variant()
    {
        var model = Deserialize(TierQualPointsFabricationJson());
        Assert.True(EventModelSaveGuard.LooksLikePatFabrication(model));
    }

    [Fact]
    public void LooksLikePatFabrication_classic_container()
    {
        var model = Deserialize(ClassicPatContainerJson());
        Assert.True(EventModelSaveGuard.LooksLikePatFabrication(model));
    }

    [Fact]
    public void LooksLikePatFabrication_eventable_order_is_false_positive_guard()
    {
        var model = Deserialize(
            """
            {
              "id": "evt-1",
              "name": "Order",
              "tag": "eventable",
              "modelMetaData": {
                "Wrapper": "wrap-1",
                "NaturalKeySymbols": "[\"orderid\"]",
                "AccountXIdSymbol": "customerid",
                "TimeOfOccurrence": "orderdate"
              },
              "attributes": [
                { "symbol": "customerid", "type": "Primitive", "dataType": "String" },
                { "symbol": "orderid", "type": "Primitive", "dataType": "String" }
              ]
            }
            """);

        Assert.False(EventModelSaveGuard.LooksLikePatFabrication(model));
    }

    [Fact]
    public void LooksLikePatFabrication_earn_only_spendable_name_without_pat_signals()
    {
        var model = Deserialize(
            """
            {
              "id": "m1",
              "name": "MemberPoints",
              "isContainer": false,
              "attributes": [
                { "symbol": "amount", "type": "Primitive", "dataType": "Number" }
              ]
            }
            """);

        Assert.False(EventModelSaveGuard.LooksLikePatFabrication(model));
    }

    [Fact]
    public void GetBypassRemediation_prefers_pat_fabrication_message_for_tier_variant()
    {
        var remediation = EventModelSaveGuard.GetBypassRemediation(TierQualPointsFabricationJson());
        Assert.NotNull(remediation);
        Assert.Contains("upsert_point_account_type", remediation!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("after the Events gate clears", remediation!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LooksLikePointAccountTypeModel_still_true_for_classic_container()
    {
        var model = Deserialize(ClassicPatContainerJson());
        Assert.True(EventModelSaveGuard.LooksLikePointAccountTypeModel(model));
    }

    private static ModelDto Deserialize(string json) =>
        JsonSerializer.Deserialize<ModelDto>(json, JsonOpts)!;

    private static string TierQualPointsFabricationJson() =>
        """
        {
          "id": "e68d2c52-ee33-450e-8921-96e63c144b1e",
          "tenantId": "primo",
          "modelType": "loyalty",
          "name": "TierQualPoints",
          "displayName": "TierQualPoints",
          "isContainer": false,
          "isPerTenancy": true,
          "partitionKey1Symbol": "",
          "partitionKey2Symbol": "",
          "attributes": [
            {
              "type": "Primitive",
              "symbol": "balance",
              "dataType": "Number",
              "displayName": "Balance"
            }
          ],
          "modelMetaData": {
            "ledgerType": "NonSpendable",
            "isSpendable": "false",
            "pointAccountTypeName": "TierQualPoints",
            "description": "Tier qualification counter"
          }
        }
        """;

    private static string ClassicPatContainerJson() =>
        """
        {
          "id": "6983defa-b920-4a4f-a3fa-d4508083dddf",
          "name": "ReviewRewards",
          "modelType": "loyalty",
          "isContainer": true,
          "attributes": [
            { "symbol": "extaccountid", "type": "Primitive", "dataType": "String" },
            { "symbol": "pointsourceid", "type": "Primitive", "dataType": "String" },
            { "symbol": "ledgertype", "type": "Primitive", "dataType": "String" }
          ]
        }
        """;
}
