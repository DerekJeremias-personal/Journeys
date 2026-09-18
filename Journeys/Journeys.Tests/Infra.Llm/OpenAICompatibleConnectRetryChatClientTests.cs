using System.Net;
using Journeys.Infra.Llm;
using Microsoft.Extensions.AI;

namespace Journeys.Tests.Infra.Llm;

public class OpenAICompatibleConnectRetryChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_retries_connection_error_then_succeeds()
    {
        var inner = new FakeChatClient
        {
            OnGetResponse = call =>
            {
                if (call == 1)
                    throw new HttpRequestException(HttpRequestError.ConnectionError, "connection error");
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
            }
        };
        using var sut = new OpenAICompatibleConnectRetryChatClient(
            inner,
            "http://localhost:11434/v1",
            TimeSpan.FromSeconds(5),
            TimeSpan.Zero);

        var response = await sut.GetResponseAsync([]);

        Assert.Equal("ok", response.Text);
        Assert.Equal(2, inner.GetResponseCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetResponseAsync_does_not_retry_401(bool useStatusCode)
    {
        var inner = new FakeChatClient
        {
            OnGetResponse = _ => throw Create401(useStatusCode)
        };
        using var sut = new OpenAICompatibleConnectRetryChatClient(
            inner,
            "http://localhost:11434/v1",
            TimeSpan.FromSeconds(5),
            TimeSpan.Zero);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetResponseAsync([]));

        Assert.Equal(1, inner.GetResponseCalls);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_retries_connection_error_then_succeeds()
    {
        var inner = new FakeChatClient
        {
            OnGetStreamingResponse = call =>
            {
                if (call == 1)
                    throw new HttpRequestException(HttpRequestError.ConnectionError, "connection error");
                return YieldSuccess();
            }
        };
        using var sut = new OpenAICompatibleConnectRetryChatClient(
            inner,
            "http://localhost:11434/v1",
            TimeSpan.FromSeconds(5),
            TimeSpan.Zero);

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in sut.GetStreamingResponseAsync([]))
            updates.Add(update);

        Assert.Single(updates);
        Assert.Equal("recovered", updates[0].Text);
        Assert.Equal(2, inner.GetStreamingCalls);
    }

    [Fact]
    public async Task GetResponseAsync_does_not_retry_invalid_response()
    {
        var inner = new FakeChatClient
        {
            OnGetResponse = _ => throw new HttpRequestException(
                HttpRequestError.InvalidResponse,
                "Internal Server Error",
                inner: null,
                statusCode: HttpStatusCode.InternalServerError)
        };
        using var sut = new OpenAICompatibleConnectRetryChatClient(
            inner,
            "http://localhost:11434/v1",
            TimeSpan.FromSeconds(5),
            TimeSpan.Zero);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetResponseAsync([]));

        Assert.Equal(1, inner.GetResponseCalls);
        Assert.DoesNotContain("Ollama unreachable", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpRequestError.InvalidResponse, ex.HttpRequestError);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_does_not_retry_after_first_update()
    {
        var inner = new FakeChatClient
        {
            OnGetStreamingResponse = call => call == 1 ? YieldThenThrow() : YieldSuccess()
        };
        using var sut = new OpenAICompatibleConnectRetryChatClient(
            inner,
            "http://localhost:11434/v1",
            TimeSpan.FromSeconds(5),
            TimeSpan.Zero);

        var updates = new List<ChatResponseUpdate>();
        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var update in sut.GetStreamingResponseAsync([]))
                updates.Add(update);
        });

        Assert.Single(updates);
        Assert.Equal("chunk", updates[0].Text);
        Assert.Equal(1, inner.GetStreamingCalls);
        Assert.Contains("connection error", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpRequestException Create401(bool useStatusCode) =>
        useStatusCode
            ? new HttpRequestException(
                HttpRequestError.InvalidResponse,
                "Unauthorized",
                inner: null,
                statusCode: HttpStatusCode.Unauthorized)
            : new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized).");

    private static async IAsyncEnumerable<ChatResponseUpdate> YieldThenThrow()
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, "chunk");
        await Task.CompletedTask;
        throw new HttpRequestException(HttpRequestError.ConnectionError, "connection error");
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> YieldSuccess()
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, "recovered");
        await Task.CompletedTask;
    }

    private sealed class FakeChatClient : IChatClient
    {
        public int GetResponseCalls { get; private set; }
        public int GetStreamingCalls { get; private set; }
        public Func<int, Task<ChatResponse>>? OnGetResponse { get; init; }
        public Func<int, IAsyncEnumerable<ChatResponseUpdate>>? OnGetStreamingResponse { get; init; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            GetResponseCalls++;
            return OnGetResponse!(GetResponseCalls);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            GetStreamingCalls++;
            return OnGetStreamingResponse!(GetStreamingCalls);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
