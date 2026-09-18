using System.Text.Json;

namespace Journeys.Tests.CampaignAgent;

public class CampaignAgentDevelopmentJsonTests
{
    [Fact]
    public void DevelopmentJson_HasOllamaDiet()
    {
        var path = JourneysApiContentPaths.DevelopmentJson;
        var jsonOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
        using var doc = JsonDocument.Parse(File.ReadAllText(path), jsonOptions);
        var campaignAgent = doc.RootElement.GetProperty("CampaignAgent");
        Assert.Equal("Ollama", campaignAgent.GetProperty("Provider").GetString());
        Assert.False(campaignAgent.GetProperty("ExposeFullBackendMcpToolSurface").GetBoolean());
        var openAi = campaignAgent.GetProperty("OpenAICompatible");
        Assert.Equal("llama3.1:8b", openAi.GetProperty("Model").GetString());
        Assert.Equal(60, openAi.GetProperty("ConnectRetrySeconds").GetInt32());
    }
}
