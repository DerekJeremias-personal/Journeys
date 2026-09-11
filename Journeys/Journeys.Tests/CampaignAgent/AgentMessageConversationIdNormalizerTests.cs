using Journeys.DAL.Adapters;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public sealed class AgentMessageConversationIdNormalizerTests
{
    [Theory]
    [InlineData("4073A72A-9205-4395-9BDD-EF2B10900B5B", "4073a72a920543959bddef2b10900b5b")]
    [InlineData(" 4073a72a920543959bddef2b10900b5b ", "4073a72a920543959bddef2b10900b5b")]
    public void Normalize_strips_dashes_and_lowercases(string raw, string expected) =>
        Assert.Equal(expected, AgentMessageConversationIdNormalizer.Normalize(raw));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    public void Normalize_throws_when_empty(string raw) =>
        Assert.Throws<ArgumentException>(() => AgentMessageConversationIdNormalizer.Normalize(raw));
}
