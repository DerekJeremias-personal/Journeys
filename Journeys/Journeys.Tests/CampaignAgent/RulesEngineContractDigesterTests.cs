using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Journeys.API.Mcp;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.RulesEngine.Providers.Historical;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class RulesEngineContractDigesterTests
{
    private static readonly JsonSerializerOptions ContractOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Digest_full_contract_shrinks_and_retains_fix_fields()
    {
        var raw = JsonSerializer.Serialize(RulesEngineMcpContractSummary.Build(), ContractOptions);
        var result = RulesEngineContractDigester.Digest(raw);

        Assert.True(result.Transformed);
        Assert.True(result.DigestChars < result.OriginalChars);

        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;

        Assert.Equal(RulesEngineMcpContractSummary.MatrixVersion, root.GetProperty("matrixVersion").GetString());
        Assert.True(root.TryGetProperty("criticalRows", out var rows) && rows.GetArrayLength() > 0);
        Assert.True(root.TryGetProperty("enumCatalog", out var catalog) && catalog.GetArrayLength() > 0);
        Assert.True(root.TryGetProperty("tierAErrorShape", out _));
        Assert.False(root.TryGetProperty("artifact", out _));

        var firstRow = rows[0];
        Assert.False(firstRow.TryGetProperty("facet", out _));
        Assert.False(firstRow.TryGetProperty("notes", out _));
        Assert.True(firstRow.TryGetProperty("tierAViolationCodes", out _));

        var firstEnum = catalog[0];
        Assert.True(firstEnum.TryGetProperty("enumName", out _));
        Assert.True(firstEnum.TryGetProperty("validStrings", out _));
        Assert.False(firstEnum.TryGetProperty("jsonContexts", out _));
        Assert.False(firstEnum.TryGetProperty("notes", out _));
    }

    [Fact]
    public void Digest_meai_envelope_round_trips()
    {
        var inner = JsonSerializer.Serialize(RulesEngineMcpContractSummary.Build(), ContractOptions);
        var envelope = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["$type"] = "text",
            ["text"] = inner
        });

        var result = RulesEngineContractDigester.Digest(envelope);

        Assert.True(result.Transformed);
        using var doc = JsonDocument.Parse(result.Json);
        Assert.Equal("text", doc.RootElement.GetProperty("$type").GetString());
        using var innerDoc = JsonDocument.Parse(doc.RootElement.GetProperty("text").GetString()!);
        Assert.True(innerDoc.RootElement.TryGetProperty("criticalRows", out _));
    }

    [Fact]
    public void Digest_retains_polymorphic_discriminator_fields()
    {
        var raw = JsonSerializer.Serialize(RulesEngineMcpContractSummary.Build(), ContractOptions);
        var result = RulesEngineContractDigester.Digest(raw);

        Assert.True(result.Transformed);

        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("valueProviderKinds", out var valueKinds));
        Assert.Contains("PointBalanceProvider", valueKinds.EnumerateArray().Select(k => k.GetString()));
        Assert.True(root.TryGetProperty("historicalProviderKinds", out var historicalKinds));
        Assert.Equal(nameof(SimpleCalculationProvider), historicalKinds[0].GetString());
        Assert.True(root.TryGetProperty("navigationCriteriaTypes", out _));
        Assert.True(root.TryGetProperty("typeDiscriminatorCatalog", out var typeCatalog));
        Assert.True(typeCatalog.GetArrayLength() >= 5);

        var historicalEntry = typeCatalog.EnumerateArray()
            .First(e => e.GetProperty("id").GetString() == nameof(IHistoricalValueProvider));
        Assert.True(historicalEntry.TryGetProperty("allowedTypes", out _));
        Assert.True(historicalEntry.TryGetProperty("jsonContexts", out _));
        Assert.False(historicalEntry.TryGetProperty("notes", out _));
    }

    [Fact]
    public void Digest_errors_passthrough()
    {
        const string failure = """{ "errors": { "journey.validation.0": "bad" } }""";
        var result = RulesEngineContractDigester.Digest(failure);

        Assert.False(result.Transformed);
        Assert.Equal(failure, result.Json);
    }
}
