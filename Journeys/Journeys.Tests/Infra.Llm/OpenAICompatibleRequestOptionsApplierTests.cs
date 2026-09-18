using Journeys.Infra.Llm;
using Microsoft.Extensions.AI;

namespace Journeys.Tests.Infra.Llm;

public class OpenAICompatibleRequestOptionsApplierTests
{
    [Fact]
    public void Apply_null_with_num_ctx_sets_property_without_mutating_source()
    {
        var runtime = new OpenAICompatibleRuntimeOptions(16384, null, null, 0);
        var source = new ChatOptions { MaxOutputTokens = 256 };

        var appliedFromNull = OpenAICompatibleRequestOptionsApplier.Apply(null, runtime);
        var appliedFromSource = OpenAICompatibleRequestOptionsApplier.Apply(source, runtime);

        Assert.Equal(16384, appliedFromNull.AdditionalProperties?["num_ctx"]);
        Assert.Equal(16384, appliedFromSource.AdditionalProperties?["num_ctx"]);
        Assert.Equal(256, appliedFromSource.MaxOutputTokens);
        Assert.False(Has(source, "num_ctx"));
        Assert.NotSame(source, appliedFromSource);
    }

    [Fact]
    public void Apply_omitted_leaves_extra_fields_empty()
    {
        var source = new ChatOptions { MaxOutputTokens = 256 };
        var applied = OpenAICompatibleRequestOptionsApplier.Apply(
            source,
            new OpenAICompatibleRuntimeOptions(null, null, null, 0));

        Assert.Equal(256, applied.MaxOutputTokens);
        Assert.False(Has(applied, "num_ctx"));
        Assert.False(Has(applied, "think"));
        Assert.False(Has(applied, "reasoning_effort"));
    }

    [Fact]
    public void Apply_low_sets_think_and_reasoning_effort()
    {
        var applied = OpenAICompatibleRequestOptionsApplier.Apply(
            new ChatOptions(),
            new OpenAICompatibleRuntimeOptions(16384, "low", null, 0));

        Assert.Equal(16384, applied.AdditionalProperties?["num_ctx"]);
        Assert.Equal("low", applied.AdditionalProperties?["think"]);
        Assert.Equal("low", applied.AdditionalProperties?["reasoning_effort"]);
    }

    [Fact]
    public void Apply_off_sets_think_false()
    {
        var applied = OpenAICompatibleRequestOptionsApplier.Apply(
            new ChatOptions(),
            new OpenAICompatibleRuntimeOptions(null, "off", null, 0));

        Assert.Equal(false, applied.AdditionalProperties?["think"]);
        Assert.False(Has(applied, "reasoning_effort"));
        Assert.False(Has(applied, "num_ctx"));
    }

    [Fact]
    public void Apply_does_not_mutate_source()
    {
        var source = new ChatOptions();
        OpenAICompatibleRequestOptionsApplier.Apply(
            source,
            new OpenAICompatibleRuntimeOptions(8192, "high", null, 0));

        Assert.False(Has(source, "num_ctx"));
        Assert.False(Has(source, "think"));
    }

    private static bool Has(ChatOptions options, string key) =>
        options.AdditionalProperties is not null && options.AdditionalProperties.ContainsKey(key);
}
