using Journeys.API.CampaignAgent;
using Microsoft.Extensions.Configuration;

namespace Journeys.Tests.CampaignAgent;

public class CampaignAgentLlmProviderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Anthropic")]
    [InlineData("anthropic")]
    public void Parse_defaults_or_anthropic(string? value)
    {
        Assert.Equal(CampaignAgentLlmProviderKind.Anthropic, CampaignAgentLlmProvider.Parse(value));
    }

    [Theory]
    [InlineData("OpenAICompatible")]
    [InlineData("openaiCompatible")]
    [InlineData("Ollama")]
    [InlineData("ollama")]
    public void Parse_openai_compatible_aliases(string value)
    {
        Assert.Equal(CampaignAgentLlmProviderKind.OpenAICompatible, CampaignAgentLlmProvider.Parse(value));
    }

    [Fact]
    public void Parse_unknown_throws_campaign_agent_message()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CampaignAgentLlmProvider.Parse("AzureOpenAI"));
        Assert.Contains("Campaign Agent", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Backend Agent", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Anthropic", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OpenAICompatible", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ollama", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_uses_section_then_env_key_when_section_blank()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CAMPAIGN_AGENT_PROVIDER"] = "Ollama"
            })
            .Build();
        Assert.Equal(CampaignAgentLlmProviderKind.OpenAICompatible, CampaignAgentLlmProvider.Resolve(config));
    }

    [Fact]
    public void Resolve_section_wins_when_set()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CampaignAgent:Provider"] = "Anthropic",
                ["CAMPAIGN_AGENT_PROVIDER"] = "Ollama"
            })
            .Build();
        Assert.Equal(CampaignAgentLlmProviderKind.Anthropic, CampaignAgentLlmProvider.Resolve(config));
    }
}
