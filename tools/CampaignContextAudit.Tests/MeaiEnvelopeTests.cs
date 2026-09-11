using CampaignContextAudit.Transcript;
using Xunit;

namespace CampaignContextAudit.Tests;

public class MeaiEnvelopeTests
{
    [Fact]
    public void Parses_function_calls_from_envelope()
    {
        var content = """
        { "v":1, "meai":true, "role":"assistant", "contents":[
          { "kind":"text", "text":"working" },
          { "kind":"functionCall", "callId":"c1", "name":"get_all_models", "arguments":{} }
        ]}
        """;
        Assert.True(MeaiEnvelope.IsEnvelope(content));
        var calls = MeaiEnvelope.ExtractFunctionCalls(content);
        Assert.Single(calls);
        Assert.Equal("get_all_models", calls[0].Name);
        Assert.Equal("c1", calls[0].CallId);
    }

    [Fact]
    public void Non_envelope_text_returns_false_and_no_calls()
    {
        Assert.False(MeaiEnvelope.IsEnvelope("just plain assistant text"));
        Assert.Empty(MeaiEnvelope.ExtractFunctionCalls("just plain assistant text"));
    }
}
