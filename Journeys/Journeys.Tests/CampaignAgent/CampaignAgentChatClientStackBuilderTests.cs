using Journeys.API.CampaignAgent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace Journeys.Tests.CampaignAgent;

public sealed class CampaignAgentChatClientStackBuilderTests
{
    [Fact]
    public void HasFunctionInvocationLayer_ReturnsTrue_WhenFactoryClientIsFunctionInvoking()
    {
        var wrapped = new FunctionInvokingTestDouble(new NoOpChatClient());
        Assert.True(CampaignAgentChatClientStackBuilder.HasFunctionInvocationLayer(wrapped));
    }

    [Fact]
    public void Build_LeavesExactlyOneFunctionInvocationLayer()
    {
        var factoryClient = new FunctionInvokingTestDouble(
            new FunctionInvokingTestDouble(new NoOpChatClient()));

        var built = CampaignAgentChatClientStackBuilder.Build(factoryClient, NullLogger.Instance);

        Assert.Equal(1, CountLayersWithNameFragment(built, "FunctionInvoking"));
    }

    private static int CountLayersWithNameFragment(IChatClient client, string fragment)
    {
        var count = 0;
        var current = client;
        var visited = new HashSet<IChatClient>();
        while (current != null && visited.Add(current))
        {
            if (current.GetType().Name.Contains(fragment, StringComparison.Ordinal))
                count++;

            if (!TryGetInner(current, out current))
                break;
        }

        return count;
    }

    private static bool TryGetInner(IChatClient client, out IChatClient? inner)
    {
        var prop = client.GetType().GetProperty("InnerClient")
                   ?? client.GetType().GetProperty("Inner");
        inner = prop?.GetValue(client) as IChatClient;
        return inner != null;
    }

    private sealed class NoOpChatClient : IChatClient, IDisposable
    {
        public void Dispose() { }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FunctionInvokingTestDouble(IChatClient inner) : IChatClient, IDisposable
    {
        public IChatClient InnerClient { get; } = inner;

        public void Dispose() => (InnerClient as IDisposable)?.Dispose();

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            InnerClient.GetService(serviceType, serviceKey);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            InnerClient.GetResponseAsync(messages, options, cancellationToken);

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            InnerClient.GetStreamingResponseAsync(messages, options, cancellationToken);
    }
}
