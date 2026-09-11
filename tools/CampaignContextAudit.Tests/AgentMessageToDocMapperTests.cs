using CampaignContextAudit.Mapping;
using Journeys.Core.Models;
using Xunit;

namespace CampaignContextAudit.Tests;

public class AgentMessageToDocMapperTests
{
    [Fact]
    public void ToDoc_maps_fields_and_lastupdated_to_cosmos_timestamp()
    {
        var lastUpdated = new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);
        var msg = new AgentMessage(
            "primo", "user1", "conv1", 3, "tool", "",
            null, "call1", "list_models", "{}", "{\"ok\":true}",
            lastupdated: lastUpdated,
            toolDurationMs: 42);

        var doc = AgentMessageToDocMapper.ToDoc(msg);

        Assert.Equal("primo", doc.TenantId);
        Assert.Equal("conv1", doc.ConversationId);
        Assert.Equal(3, doc.Sequence);
        Assert.Equal("list_models", doc.ToolName);
        Assert.Equal(42, doc.ToolDurationMs);
        Assert.Equal(lastUpdated.ToUnixTimeSeconds(), doc.CosmosTimestamp);
    }
}
