using Microsoft.Extensions.AI;

namespace Journeys.Infra.Llm;

/// <summary>
/// Applies <see cref="OpenAICompatibleRuntimeOptions"/> to every chat request so workflow
/// segments inherit the same Ollama knobs without the orchestrator reading them.
/// </summary>
public sealed class OpenAICompatibleRequestOptionsChatClient : IChatClient
{
    private readonly IChatClient _inner;
    private readonly OpenAICompatibleRuntimeOptions _runtime;

    public OpenAICompatibleRequestOptionsChatClient(IChatClient inner, OpenAICompatibleRuntimeOptions runtime)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => _inner.GetResponseAsync(
            messages,
            OpenAICompatibleRequestOptionsApplier.Apply(options, _runtime),
            cancellationToken);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => _inner.GetStreamingResponseAsync(
            messages,
            OpenAICompatibleRequestOptionsApplier.Apply(options, _runtime),
            cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        _inner.GetService(serviceType, serviceKey);

    public void Dispose() => _inner.Dispose();
}
