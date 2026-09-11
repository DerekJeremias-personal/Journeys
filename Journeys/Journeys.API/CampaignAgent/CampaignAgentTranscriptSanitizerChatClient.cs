using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Strips duplicate <see cref="FunctionResultContent"/> before each inner LLM request so
/// <see cref="ChatClientBuilderExtensions.UseFunctionInvocation"/> multi-round calls satisfy Anthropic ordering.
/// Register directly on the base <see cref="IChatClient"/>, below function invocation.
/// </summary>
internal sealed class CampaignAgentTranscriptSanitizerChatClient : IChatClient, IDisposable
{
    private readonly IChatClient _inner;

    public CampaignAgentTranscriptSanitizerChatClient(IChatClient inner) =>
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        _inner.GetService(serviceType, serviceKey);

    public void Dispose() => (_inner as IDisposable)?.Dispose();

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var sanitized = CampaignAgentTranscriptRules.SanitizeChatMessages(messages);
        return _inner.GetResponseAsync(sanitized, options, cancellationToken);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sanitized = CampaignAgentTranscriptRules.SanitizeChatMessages(messages);
        await foreach (var update in _inner
                           .GetStreamingResponseAsync(sanitized, options, cancellationToken)
                           .ConfigureAwait(false))
            yield return update;
    }
}
