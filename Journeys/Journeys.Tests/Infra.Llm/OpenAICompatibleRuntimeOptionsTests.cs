using Journeys.Infra.Llm;
using Microsoft.Extensions.Configuration;

namespace Journeys.Tests.Infra.Llm;

public class OpenAICompatibleRuntimeOptionsTests
{
    [Fact]
    public void Parse_omits_num_ctx_when_config_and_env_empty()
    {
        var options = OpenAICompatibleRuntimeOptions.Parse(EmptyConfig(), "CampaignAgent");

        Assert.Null(options.NumCtx);
        Assert.Null(options.ReasoningEffort);
        Assert.Null(options.RequestTimeout);
        Assert.Equal(0, options.ConnectRetrySeconds);
    }

    [Fact]
    public void Parse_reads_num_ctx_16384_from_config()
    {
        var config = Config(("CampaignAgent:OpenAICompatible:NumCtx", "16384"));

        var options = OpenAICompatibleRuntimeOptions.Parse(config, "CampaignAgent");

        Assert.Equal(16384, options.NumCtx);
    }

    [Fact]
    public void Parse_num_ctx_100_throws_range()
    {
        var config = Config(("CampaignAgent:OpenAICompatible:NumCtx", "100"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => OpenAICompatibleRuntimeOptions.Parse(config, "CampaignAgent"));

        Assert.Contains("2048", ex.Message);
        Assert.Contains("128000", ex.Message);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("OFF")]
    [InlineData("low")]
    [InlineData("LOW")]
    public void Parse_accepts_reasoning_effort_off_and_low(string value)
    {
        var config = Config(("CampaignAgent:OpenAICompatible:ReasoningEffort", value));

        var options = OpenAICompatibleRuntimeOptions.Parse(config, "CampaignAgent");

        Assert.Equal(value.ToLowerInvariant(), options.ReasoningEffort);
    }

    [Fact]
    public void Parse_bogus_reasoning_effort_throws()
    {
        var config = Config(("CampaignAgent:OpenAICompatible:ReasoningEffort", "bogus"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => OpenAICompatibleRuntimeOptions.Parse(config, "CampaignAgent"));

        Assert.Contains("off", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("low", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_omits_request_timeout_when_unset()
    {
        var options = OpenAICompatibleRuntimeOptions.Parse(EmptyConfig(), "CampaignAgent");

        Assert.Null(options.RequestTimeout);
        Assert.Equal(TimeSpan.FromMinutes(15), OpenAICompatibleRuntimeOptions.ResolveNetworkTimeout(options.RequestTimeout));
    }

    [Fact]
    public void Parse_zero_request_timeout_is_infinite()
    {
        var config = Config(("CampaignAgent:OpenAICompatible:RequestTimeoutSeconds", "0"));

        var options = OpenAICompatibleRuntimeOptions.Parse(config, "CampaignAgent");

        Assert.Equal(Timeout.InfiniteTimeSpan, options.RequestTimeout);
    }

    [Fact]
    public void Parse_reads_request_timeout_seconds_1800()
    {
        var config = Config(("CampaignAgent:OpenAICompatible:RequestTimeoutSeconds", "1800"));

        var options = OpenAICompatibleRuntimeOptions.Parse(config, "CampaignAgent");

        Assert.Equal(TimeSpan.FromSeconds(1800), options.RequestTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1800), OpenAICompatibleRuntimeOptions.ResolveNetworkTimeout(options.RequestTimeout));
    }

    [Fact]
    public void Parse_omitted_connect_retry_seconds_is_zero()
    {
        var options = OpenAICompatibleRuntimeOptions.Parse(EmptyConfig(), "CampaignAgent");

        Assert.Equal(0, options.ConnectRetrySeconds);
    }

    [Fact]
    public void Parse_reads_connect_retry_seconds_60()
    {
        var config = Config(("CampaignAgent:OpenAICompatible:ConnectRetrySeconds", "60"));

        var options = OpenAICompatibleRuntimeOptions.Parse(config, "CampaignAgent");

        Assert.Equal(60, options.ConnectRetrySeconds);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("601")]
    public void Parse_invalid_connect_retry_seconds_throws(string raw)
    {
        var config = Config(("CampaignAgent:OpenAICompatible:ConnectRetrySeconds", raw));

        var ex = Assert.Throws<InvalidOperationException>(
            () => OpenAICompatibleRuntimeOptions.Parse(config, "CampaignAgent"));

        Assert.Contains("ConnectRetrySeconds", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_falls_back_to_campaign_agent_env_keys_when_config_empty()
    {
        var config = Config(
            ("CAMPAIGN_AGENT_OPENAI_NUM_CTX", "8192"),
            ("CAMPAIGN_AGENT_OPENAI_REASONING_EFFORT", "medium"),
            ("CAMPAIGN_AGENT_OPENAI_REQUEST_TIMEOUT_SECONDS", "1200"),
            ("CAMPAIGN_AGENT_OPENAI_CONNECT_RETRY_SECONDS", "45"));

        var options = OpenAICompatibleRuntimeOptions.Parse(config, "CampaignAgent");

        Assert.Equal(8192, options.NumCtx);
        Assert.Equal("medium", options.ReasoningEffort);
        Assert.Equal(TimeSpan.FromSeconds(1200), options.RequestTimeout);
        Assert.Equal(45, options.ConnectRetrySeconds);
    }

    [Fact]
    public void Parse_does_not_read_model_from_env()
    {
        var config = Config(("CAMPAIGN_AGENT_OPENAI_MODEL", "llama3.1:8b"));

        var options = OpenAICompatibleRuntimeOptions.Parse(config, "CampaignAgent");

        Assert.Null(options.NumCtx);
        Assert.Null(options.ReasoningEffort);
        Assert.Null(options.RequestTimeout);
        Assert.Equal(0, options.ConnectRetrySeconds);
        Assert.Null(typeof(OpenAICompatibleRuntimeOptions).GetProperty("Model"));
    }

    [Fact]
    public void Parse_request_timeout_below_minimum_throws()
    {
        var config = Config(("CampaignAgent:OpenAICompatible:RequestTimeoutSeconds", "10"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => OpenAICompatibleRuntimeOptions.Parse(config, "CampaignAgent"));

        Assert.Contains("30", ex.Message);
    }

    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    private static IConfiguration Config(params (string Key, string? Value)[] pairs)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => p.Value))
            .Build();
    }
}
