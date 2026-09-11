using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Journeys.API.CampaignAgent;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Microsoft.Extensions.AI;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class ModelReadDigestAIFunctionTests
{
    private sealed class StubGetModelFunction : AIFunction
    {
        private readonly string _json;

        public StubGetModelFunction(string json)
        {
            _json = json;
            Name = "get_model";
        }

        public override string Name { get; }

        protected override ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<object?>(_json);
    }

    [Fact]
    public async Task Invoke_merges_contract_from_raw_json_before_digesting()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";

        var raw = OrderModelJson("order-1");
        var fn = new ModelReadDigestAIFunction(
            new StubGetModelFunction(raw),
            enabled: true,
            logger: null,
            getState: () => state);

        var result = await fn.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Single(EventModelContractsAccumulator.ReadResolved(state));

        var text = Assert.IsType<string>(result);
        using var doc = JsonDocument.Parse(text);
        Assert.True(doc.RootElement.TryGetProperty("note", out _));
    }

    [Fact]
    public async Task Invoke_merges_raw_json_when_digest_disabled()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";

        var raw = OrderModelJson("order-1");
        var fn = new ModelReadDigestAIFunction(
            new StubGetModelFunction(raw),
            enabled: false,
            logger: null,
            getState: () => state);

        var result = await fn.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Single(EventModelContractsAccumulator.ReadResolved(state));
        Assert.Equal(raw, result);
    }

    private static string OrderModelJson(string id) =>
        JsonSerializer.Serialize(new
        {
            id,
            name = "order",
            tag = "eventable",
            modelType = "loyalty",
            modelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "f00df00d-dead-f00d-f00d-ea7f00d1337e",
                ["NaturalKeySymbols"] = "[\"orderid\"]",
                ["AccountXIdSymbol"] = "profileid",
                ["TimeOfOccurrence"] = "timestamp"
            },
            attributes = new[] { new { symbol = "orderid", type = "Primitive", dataType = "string" } }
        });
}
