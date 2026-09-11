using System.Text.Json;
using Journeys.API.CampaignAgent;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class EventModelSingleModelFetcherTests
{
    [Fact]
    public void BuildGetModelToolNamesToTry_prefers_snake_case()
    {
        var names = EventModelSingleModelFetcher.BuildGetModelToolNamesToTry([]);
        Assert.Equal("get_model", names[0]);
        Assert.Contains("GetModel", names);
    }

    [Fact]
    public void TryExtractModelJson_parses_text_envelope()
    {
        const string inner = """{"id":"order-1","name":"order","tag":"eventable"}""";
        var envelope = JsonSerializer.Serialize(new { text = inner });

        var ok = EventModelSingleModelFetcher.TryExtractModelJson(envelope, out var json);

        Assert.True(ok);
        Assert.Contains("order-1", json!, StringComparison.Ordinal);
    }

    [Fact]
    public void TryExtractModelJson_parses_bare_object()
    {
        const string inner = """{"id":"order-1","name":"order"}""";

        var ok = EventModelSingleModelFetcher.TryExtractModelJson(inner, out var json);

        Assert.True(ok);
        Assert.Equal(inner, json);
    }
}
