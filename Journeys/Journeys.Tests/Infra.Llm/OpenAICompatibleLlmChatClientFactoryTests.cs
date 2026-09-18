using Journeys.Infra.Llm;
using Microsoft.Extensions.Configuration;

namespace Journeys.Tests.Infra.Llm;

public class OpenAICompatibleLlmChatClientFactoryTests
{
    [Fact]
    public void CreateChatClient_missing_model_throws_without_calling_ollama()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        using var factory = new OpenAICompatibleLlmChatClientFactory(config, "CampaignAgent");

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateChatClient());

        Assert.Contains("OpenAI-compatible model is missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveEndpoint_blank_base_url_defaults_to_local_ollama()
    {
        var config = Config(
            ("CampaignAgent:OpenAICompatible:BaseUrl", "  "),
            ("CampaignAgent:OpenAICompatible:Model", "llama3.1:8b"));

        var (baseUrl, _, _) = OpenAICompatibleLlmChatClientFactory.ResolveEndpoint(config, "CampaignAgent");

        Assert.Equal("http://localhost:11434/v1", baseUrl);
    }

    [Fact]
    public void ResolveEndpoint_blank_api_key_defaults_to_ollama()
    {
        var config = Config(
            ("CampaignAgent:OpenAICompatible:ApiKey", ""),
            ("CampaignAgent:OpenAICompatible:Model", "llama3.1:8b"));

        var (_, apiKey, _) = OpenAICompatibleLlmChatClientFactory.ResolveEndpoint(config, "CampaignAgent");

        Assert.Equal("ollama", apiKey);
    }

    [Fact]
    public void ResolveEndpoint_model_from_env_when_config_model_blank()
    {
        var config = Config(("CAMPAIGN_AGENT_OPENAI_MODEL", "llama3.1:8b"));

        var (_, _, model) = OpenAICompatibleLlmChatClientFactory.ResolveEndpoint(config, "CampaignAgent");

        Assert.Equal("llama3.1:8b", model);
    }

    private static IConfiguration Config(params (string Key, string? Value)[] pairs)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => p.Value))
            .Build();
    }
}
