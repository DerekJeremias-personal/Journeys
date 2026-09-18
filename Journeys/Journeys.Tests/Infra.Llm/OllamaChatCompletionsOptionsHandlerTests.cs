using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Journeys.Infra.Llm;

namespace Journeys.Tests.Infra.Llm;

public class OpenAICompatibleOllamaChatCompletionsOptionsHandlerTests
{
    [Fact]
    public async Task SendAsync_patches_num_ctx_and_think_off_on_chat_completions()
    {
        var capture = new CaptureHandler();
        var runtime = new OpenAICompatibleRuntimeOptions(16384, "off", null, 0);
        var ollama = new OllamaChatCompletionsOptionsHandler(runtime, capture);
        var invoking = new InvokingHandler(ollama);

        using var request = new HttpRequestMessage(HttpMethod.Post, "http://x/v1/chat/completions")
        {
            Content = new StringContent("""{"model":"m"}""", Encoding.UTF8, "application/json")
        };

        await invoking.Invoke(request, CancellationToken.None);

        var json = JsonNode.Parse(capture.Body!);
        Assert.Equal(16384, json?["options"]?["num_ctx"]?.GetValue<int>());
        Assert.Equal(false, json?["think"]?.GetValue<bool>());
    }

    public sealed class InvokingHandler : DelegatingHandler
    {
        public InvokingHandler(HttpMessageHandler inner) : base(inner) { }

        public Task<HttpResponseMessage> Invoke(HttpRequestMessage r, CancellationToken ct) => SendAsync(r, ct);
    }

    sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }
}
