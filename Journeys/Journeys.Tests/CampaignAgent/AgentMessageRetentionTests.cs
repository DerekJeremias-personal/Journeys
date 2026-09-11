using Journeys.Core.Models;
using Journeys.DAL.Adapters;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public sealed class AgentMessageRetentionTests
{
    [Fact]
    public void Apply_30_days_sets_ttl_seconds()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CampaignAgent:MessageRetentionDays"] = "30"
            })
            .Build();
        var msg = MinimalMessage();
        AgentMessageRetention.Apply(msg, cfg);
        Assert.Equal(30 * 86400, msg.TtlSeconds);
    }

    [Fact]
    public void Apply_zero_clears_ttl()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CampaignAgent:MessageRetentionDays"] = "0"
            })
            .Build();
        var msg = MinimalMessage();
        msg.TtlSeconds = 999;
        AgentMessageRetention.Apply(msg, cfg);
        Assert.Null(msg.TtlSeconds);
    }

    [Fact]
    public void Apply_default_when_config_missing_uses_30_days()
    {
        var cfg = new ConfigurationBuilder().Build();
        var msg = MinimalMessage();
        AgentMessageRetention.Apply(msg, cfg);
        Assert.Equal(AgentMessageRetention.DefaultRetentionDays * 86400, msg.TtlSeconds);
    }

    private static AgentMessage MinimalMessage() =>
        new("primo", "user1", "conv1", 1, "user", "hi", null, null, null, null, null);
}
