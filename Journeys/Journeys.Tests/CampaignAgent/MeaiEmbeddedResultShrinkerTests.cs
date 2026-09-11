using Journeys.CampaignAgent.Remediation;
using System.Text.Json;

namespace Journeys.Tests.CampaignAgent;

public class MeaiEmbeddedResultShrinkerTests
{
    private static readonly string[] Priority = ["validate_campaign", "list_models"];

    [Fact]
    public void TryShrinkOneResult_stubs_oversized_embedded_result()
    {
        var big = new string('x', 60_000);
        var envelope =
            "{\"v\":1,\"meai\":true,\"role\":\"assistant\",\"contents\":[" +
            "{\"kind\":\"functionCall\",\"callId\":\"c1\",\"name\":\"validate_campaign\",\"arguments\":{}}," +
            "{\"kind\":\"functionResult\",\"callId\":\"c1\",\"result\":{\"body\":\"" + big + "\"}}" +
            "]}";

        var changed = MeaiEmbeddedResultShrinker.TryShrinkOneResult(envelope, 2048, Priority, out var updated);

        Assert.True(changed);
        Assert.Contains("historyStub", updated, StringComparison.Ordinal);
        Assert.DoesNotContain(big, updated, StringComparison.Ordinal);
        Assert.True(MeaiEmbeddedResultShrinker.IsMeaiEnvelope(updated));
    }

    [Fact]
    public void TryShrinkOneResult_no_op_when_already_stubbed()
    {
        var stub = ToolResultHistoryStub.Build("validate_campaign", 9000);
        var envelope =
            "{\"v\":1,\"meai\":true,\"role\":\"assistant\",\"contents\":[" +
            "{\"kind\":\"functionResult\",\"callId\":\"c1\",\"result\":" + stub + "}" +
            "]}";
        var changed = MeaiEmbeddedResultShrinker.TryShrinkOneResult(envelope, 2048, Priority, out var updated);
        Assert.False(changed);
        Assert.Equal(envelope, updated);
    }

    [Fact]
    public void CountEffectiveAssistantContentChars_skips_embedded_when_tool_row_exists()
    {
        var big = new string('y', 10_000);
        var envelope =
            "{\"v\":1,\"meai\":true,\"role\":\"assistant\",\"contents\":[" +
            "{\"kind\":\"text\",\"text\":\"hello\"}," +
            "{\"kind\":\"functionResult\",\"callId\":\"c1\",\"result\":{\"x\":\"" + big + "\"}}" +
            "]}";
        var withToolRow = new HashSet<string>(StringComparer.Ordinal) { "c1" };
        var without = new HashSet<string>(StringComparer.Ordinal);

        Assert.Equal(5, MeaiEmbeddedResultShrinker.CountEffectiveAssistantContentChars(envelope, withToolRow));
        Assert.True(MeaiEmbeddedResultShrinker.CountEffectiveAssistantContentChars(envelope, without) > 10_000);
    }
}
