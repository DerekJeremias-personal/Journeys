using Backend.Dto.Structures.Model;
using Backend.Dto.Structures.Model.Attributes;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventModelContractValidationTests
{
    [Fact]
    public void ValidateNaturalKeySymbols_throws_when_symbol_not_on_model()
    {
        var model = new ModelDto
        {
            Name = "Order",
            Attributes =
            [
                new ModelAttributePrimitiveDto
                {
                    Symbol = "orderid",
                    DataType = "string",
                    DisplayName = "Order id"
                }
            ],
            ModelMetaData = new Dictionary<string, string>
            {
                ["NaturalKeySymbols"] = "[\"orderid\",\"unknownField\"]"
            }
        };

        var ex = Assert.Throws<APIErrorsException>(() => EventModelContractValidation.ValidateNaturalKeySymbols(model));
        Assert.Contains("NaturalKeySymbol:unknownField", ex.Errors.Keys);
    }

    [Fact]
    public void ValidateNaturalKeySymbols_succeeds_when_all_symbols_match()
    {
        var model = new ModelDto
        {
            Name = "Order",
            Attributes =
            [
                new ModelAttributePrimitiveDto
                {
                    Symbol = "orderid",
                    DataType = "string",
                    DisplayName = "Order id"
                },
                new ModelAttributePrimitiveDto
                {
                    Symbol = "channel",
                    DataType = "string",
                    DisplayName = "Channel"
                }
            ],
            ModelMetaData = new Dictionary<string, string>
            {
                ["NaturalKeySymbols"] = "[\"orderid\",\"channel\"]"
            }
        };

        EventModelContractValidation.ValidateNaturalKeySymbols(model);
    }

    [Fact]
    public void SymbolDeclaredOnEventModel_accepts_dotted_path_when_root_attribute_exists()
    {
        var model = new ModelDto
        {
            Name = "Order",
            Attributes =
            [
                new ModelAttributePrimitiveDto
                {
                    Symbol = "lineitems",
                    DataType = "list",
                    DisplayName = "Lines"
                }
            ],
            ModelMetaData = new Dictionary<string, string>()
        };

        Assert.True(EventModelContractValidation.SymbolDeclaredOnEventModel("lineitems.sku", model));
    }

    [Fact]
    public void GetAccountXIdSymbolWarnings_returns_empty_when_valid()
    {
        var model = new ModelDto
        {
            Name = "Order",
            Attributes =
            [
                new ModelAttributePrimitiveDto { Symbol = "profileid", DataType = "string", DisplayName = "Profile" }
            ],
            ModelMetaData = new Dictionary<string, string> { ["AccountXIdSymbol"] = "profileid" }
        };

        var warnings = EventModelContractValidation.GetAccountXIdSymbolWarnings(model);
        Assert.Empty(warnings);
    }

    [Fact]
    public void GetAccountXIdSymbolWarnings_missing_metadata()
    {
        var model = new ModelDto { Name = "Order", ModelMetaData = new Dictionary<string, string>() };
        var warnings = EventModelContractValidation.GetAccountXIdSymbolWarnings(model);
        Assert.Contains(warnings, w => w.Contains("AccountXIdSymbol", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetAccountXIdSymbolWarnings_undeclared_symbol()
    {
        var model = new ModelDto
        {
            Name = "Order",
            Attributes = [new ModelAttributePrimitiveDto { Symbol = "orderid", DataType = "string", DisplayName = "Id" }],
            ModelMetaData = new Dictionary<string, string> { ["AccountXIdSymbol"] = "missingpath" }
        };

        var warnings = EventModelContractValidation.GetAccountXIdSymbolWarnings(model);
        Assert.Contains(warnings, w => w.Contains("missingpath", StringComparison.Ordinal));
    }
}
