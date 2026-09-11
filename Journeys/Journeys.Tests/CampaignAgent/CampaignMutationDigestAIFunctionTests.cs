using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Journeys.API.CampaignAgent;
using Microsoft.Extensions.AI;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class CampaignMutationDigestAIFunctionTests
{
    // Realistically-sized campaign so the digest (which collapses the journey to names + per-rule-set
    // outcome counts) is meaningfully smaller than the original. The fixed "note" overhead the digester
    // adds only pays off once the summarized journey content exceeds it, which is the production case.
    private const string SuccessJson = """
    {
      "Id": "c1", "Status": "draft", "Name": "x", "Events": [], "Segments": [],
      "Journey": {
        "Name": "Welcome Journey",
        "Children": [
          {
            "Name": "Awareness Node",
            "Rules": [
              { "Name": "Rule A", "OutcomesJsonElement": [ { "type": "email", "templateId": "tpl-001", "delayHours": 24, "audience": "new" }, { "type": "sms", "templateId": "tpl-002", "delayHours": 48, "audience": "lapsed" }, { "type": "push", "templateId": "tpl-003", "delayHours": 72, "audience": "all" } ] },
              { "Name": "Rule B", "OutcomesJsonElement": [ { "type": "email", "templateId": "tpl-004", "delayHours": 12, "audience": "vip" }, { "type": "webhook", "url": "https://example.com/hook", "retries": 3, "audience": "all" } ] }
            ]
          },
          {
            "Name": "Conversion Node",
            "Rules": [
              { "Name": "Rule C", "OutcomesJsonElement": [ { "type": "discount", "code": "SAVE20", "percent": 20, "audience": "cart-abandoners" }, { "type": "email", "templateId": "tpl-005", "delayHours": 6, "audience": "browsers" }, { "type": "sms", "templateId": "tpl-006", "delayHours": 3, "audience": "high-intent" } ] }
            ]
          }
        ]
      }
    }
    """;

    private sealed class FakeStringFunction : AIFunction
    {
        private readonly string _result;
        public FakeStringFunction(string result) => _result = result;
        public override string Name => "upsert_campaign";
        public override string Description => "creates or updates a campaign";
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
            => ValueTask.FromResult<object?>(_result);
    }

    private sealed class FakeObjectFunction : AIFunction
    {
        private readonly object? _result;
        public FakeObjectFunction(object? result) => _result = result;
        public override string Name => "upsert_campaign";
        public override string Description => "creates or updates a campaign";
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
            => ValueTask.FromResult(_result);
    }

    [Fact]
    public async Task Invoke_digests_success_and_delegates_metadata()
    {
        var fn = new CampaignMutationDigestAIFunction(new FakeStringFunction(SuccessJson), enabled: true, logger: null);

        Assert.Equal("upsert_campaign", fn.Name);
        Assert.Equal("creates or updates a campaign", fn.Description);

        var result = await fn.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        using var doc = JsonDocument.Parse((string)result!);
        Assert.Equal("draft", doc.RootElement.GetProperty("campaign").GetProperty("Status").GetString());
        Assert.True(((string)result!).Length < SuccessJson.Length);
    }

    [Fact]
    public async Task Invoke_passthrough_when_disabled()
    {
        var fn = new CampaignMutationDigestAIFunction(new FakeStringFunction(SuccessJson), enabled: false, logger: null);
        var result = await fn.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        Assert.Equal(SuccessJson, (string)result!);
    }

    [Fact]
    public async Task Invoke_passthrough_when_failure_result()
    {
        const string failure = """{ "errors": { "journey.validation.0": "boom" } }""";
        var fn = new CampaignMutationDigestAIFunction(new FakeStringFunction(failure), enabled: true, logger: null);
        var result = await fn.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        Assert.Equal(failure, (string)result!);
    }

    [Fact]
    public async Task Invoke_passthrough_when_result_text_not_extractable()
    {
        var fn = new CampaignMutationDigestAIFunction(new FakeObjectFunction(42), enabled: true, logger: null);
        var result = await fn.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Invoke_digests_success_and_rewraps_text_content()
    {
        var fn = new CampaignMutationDigestAIFunction(
            new FakeObjectFunction(new TextContent(SuccessJson)), enabled: true, logger: null);

        var result = await fn.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        var text = Assert.IsType<TextContent>(result);
        using var doc = JsonDocument.Parse(text.Text!);
        Assert.Equal("draft", doc.RootElement.GetProperty("campaign").GetProperty("Status").GetString());
        Assert.True(text.Text!.Length < SuccessJson.Length);
    }
}
