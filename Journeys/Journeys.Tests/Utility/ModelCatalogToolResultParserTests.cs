using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class ModelCatalogToolResultParserTests
{
    [Fact]
    public void Parses_digested_catalog_and_retained_models()
    {
        var raw = """
            {
              "note": "digested",
              "summary": { "total": 2, "retainedFull": 1, "eventable": 1, "containers": 0 },
              "retainedModels": [
                {
                  "id": "order-1",
                  "name": "order",
                  "modelType": "loyalty",
                  "tag": "eventable",
                  "isContainer": false,
                  "attributes": [
                    { "symbol": "ordertotal", "type": "Primitive", "dataType": "Number" }
                  ]
                }
              ],
              "catalog": [
                { "id": "order-1", "name": "order", "modelType": "loyalty", "tag": "eventable", "isContainer": false },
                { "id": "r-1", "name": "Review", "modelType": "loyalty", "tag": "eventable", "isContainer": false }
              ]
            }
            """;

        Assert.True(ModelCatalogToolResultParser.TryParseModels(raw, out var models));
        Assert.Equal(2, models.Count);
        Assert.Contains(models, m => m.ID == "order-1" && m.Attributes?.Count > 0);
        Assert.Contains(models, m => m.ID == "r-1");
    }

    [Fact]
    public void IsCatalogTool_recognizes_list_and_get_all_aliases()
    {
        Assert.True(ModelCatalogToolResultParser.IsCatalogTool("list_models"));
        Assert.True(ModelCatalogToolResultParser.IsCatalogTool("GetAllModels"));
        Assert.False(ModelCatalogToolResultParser.IsCatalogTool("get_model"));
    }
}
