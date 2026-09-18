using System.Text;
using System.Text.Json.Nodes;

namespace Journeys.Infra.Llm;

/// <summary>
/// Injects Ollama <c>think</c> / <c>reasoning_effort</c> / <c>options.num_ctx</c> into
/// OpenAI-compatible <c>/v1/chat/completions</c> JSON. The OpenAI SDK does not send these fields.
/// </summary>
public sealed class OllamaChatCompletionsOptionsHandler : DelegatingHandler
{
    private readonly OpenAICompatibleRuntimeOptions _runtime;

    public OllamaChatCompletionsOptionsHandler(
        OpenAICompatibleRuntimeOptions runtime,
        HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (ShouldPatch(request))
            await PatchBodyAsync(request, cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private bool ShouldPatch(HttpRequestMessage request)
    {
        if (_runtime.NumCtx is null && _runtime.ReasoningEffort is null)
            return false;
        if (request.Method != HttpMethod.Post)
            return false;
        var path = request.RequestUri?.AbsolutePath ?? "";
        return path.Contains("chat/completions", StringComparison.OrdinalIgnoreCase);
    }

    private async Task PatchBodyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null)
            return;

        var raw = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
            return;

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(raw);
        }
        catch (System.Text.Json.JsonException)
        {
            return;
        }

        if (root is not JsonObject obj)
            return;

        if (_runtime.NumCtx is int numCtx)
        {
            var options = obj["options"] as JsonObject ?? new JsonObject();
            options["num_ctx"] = numCtx;
            obj["options"] = options;
        }

        if (_runtime.ReasoningEffort is "off")
        {
            obj["think"] = false;
            obj.Remove("reasoning_effort");
        }
        else if (_runtime.ReasoningEffort is string effort)
        {
            obj["think"] = effort;
            obj["reasoning_effort"] = effort;
        }

        request.Content = new StringContent(obj.ToJsonString(), Encoding.UTF8, "application/json");
    }
}
