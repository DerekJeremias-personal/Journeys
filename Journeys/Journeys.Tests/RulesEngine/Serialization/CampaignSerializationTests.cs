
using Journeys.Infra.Backend;
using System.Text.Json;

namespace Journeys.Tests.RulesEngine.Serialization;

public class TestHttpFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        return new HttpClient();
    }
}

public class CampaignSerializationTests
{
    [Fact]
    public void JourneySerialization()
    {
        var campaign = TestJourneyFactory.GetSimplePointEarningCampaign();
        var adapter = new BackendAdapter(new TestHttpFactory(), TestOptionsFactory.GetKeyValueStorageConfigOptions(), LoggerFactoryProvider.CreateLogger<BackendAdapter>());
        var options = adapter.GetJsonSerializerOptions();
        var json = JsonSerializer.Serialize(campaign, options);
        Assert.NotNull(json);
    }
}
