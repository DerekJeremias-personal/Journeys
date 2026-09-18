using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.Infra.Llm;

/// <summary>
/// Retries OpenAI-compatible chat calls while the endpoint is unreachable (connect/reset),
/// without retrying HTTP 4xx or mid-stream failures. Never logs API keys.
/// </summary>
public sealed class OpenAICompatibleConnectRetryChatClient : IChatClient
{
    private readonly IChatClient _inner;
    private readonly string _baseUrl;
    private readonly TimeSpan _retryWindow;
    private readonly TimeSpan _retryDelay;
    private readonly ILogger? _logger;

    public OpenAICompatibleConnectRetryChatClient(
        IChatClient inner,
        string baseUrl,
        TimeSpan retryWindow,
        TimeSpan retryDelay,
        ILogger? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _retryWindow = retryWindow;
        _retryDelay = retryDelay;
        _logger = logger;
    }

    /// <summary>
    /// Inner client so stack builders can walk past this retry decorator.
    /// </summary>
    public IChatClient Inner => _inner;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Exception? last = null;
        var attempted = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (attempted && sw.Elapsed >= _retryWindow)
                break;

            attempted = true;
            try
            {
                return await _inner.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsUnreachable(ex))
            {
                last = Unwrap(ex);
                _logger?.LogWarning(
                    last,
                    "Ollama unreachable at {BaseUrl}; retrying within connect window.",
                    _baseUrl);

                if (sw.Elapsed >= _retryWindow)
                    break;

                if (_retryDelay > TimeSpan.Zero)
                    await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException($"Ollama unreachable at {_baseUrl} after retry window.", last);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Exception? last = null;
        IAsyncEnumerator<ChatResponseUpdate>? enumerator = null;
        ChatResponseUpdate? first = null;
        var hasFirst = false;
        var connected = false;
        var attempted = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (attempted && sw.Elapsed >= _retryWindow)
                break;

            attempted = true;
            enumerator = null;
            first = null;
            hasFirst = false;

            try
            {
                enumerator = _inner.GetStreamingResponseAsync(messages, options, cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);
                hasFirst = await enumerator.MoveNextAsync();
                if (hasFirst)
                    first = enumerator.Current;
                connected = true;
                break;
            }
            catch (Exception ex)
            {
                if (enumerator is not null)
                    await DisposeQuietlyAsync(enumerator).ConfigureAwait(false);
                enumerator = null;

                if (!IsUnreachable(ex))
                    throw;

                last = Unwrap(ex);
                _logger?.LogWarning(
                    last,
                    "Ollama unreachable at {BaseUrl}; retrying within connect window.",
                    _baseUrl);

                if (sw.Elapsed >= _retryWindow)
                    break;

                if (_retryDelay > TimeSpan.Zero)
                    await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        if (!connected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException($"Ollama unreachable at {_baseUrl} after retry window.", last);
        }

        try
        {
            if (hasFirst)
            {
                yield return first!;
                while (await enumerator!.MoveNextAsync())
                    yield return enumerator.Current;
            }
        }
        finally
        {
            if (enumerator is not null)
                await enumerator.DisposeAsync();
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        _inner.GetService(serviceType, serviceKey);

    public void Dispose() => _inner.Dispose();

    /// <summary>
    /// True only for connect / connection-refused / reset / DNS. Not 4xx, not 5xx,
    /// not <see cref="HttpRequestError.InvalidResponse"/>, not generic timeouts.
    /// </summary>
    internal static bool IsUnreachable(Exception exception)
    {
        exception = Unwrap(exception);

        switch (exception)
        {
            case HttpRequestException http:
                if (http.StatusCode is { } status)
                {
                    var code = (int)status;
                    if (code is >= 400 and <= 499)
                        return false;
                }

                if (http.Message.Contains("401", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (http.HttpRequestError is HttpRequestError.ConnectionError
                    or HttpRequestError.NameResolutionError)
                    return true;

                return ContainsUnreachableSignal(http.Message);
            case SocketException:
                return true;
            case IOException io:
                return ContainsUnreachableSignal(io.Message);
            default:
                return false;
        }
    }

    private static bool ContainsUnreachableSignal(string message) =>
        message.Contains("refused", StringComparison.OrdinalIgnoreCase)
        || message.Contains("reset", StringComparison.OrdinalIgnoreCase)
        || message.Contains("unreachable", StringComparison.OrdinalIgnoreCase);

    private static Exception Unwrap(Exception exception)
    {
        while (exception is AggregateException { InnerException: { } inner })
            exception = inner;
        return exception;
    }

    private static async Task DisposeQuietlyAsync(IAsyncEnumerator<ChatResponseUpdate> enumerator)
    {
        try
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort cleanup before a retry.
        }
    }
}
