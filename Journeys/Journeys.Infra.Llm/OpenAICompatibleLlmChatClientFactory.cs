using System.ClientModel;
using System.ClientModel.Primitives;
using Backend.Core.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace Journeys.Infra.Llm;

/// <summary>
/// OpenAI-compatible (e.g. Ollama) Microsoft.Extensions.AI <see cref="IChatClient"/> with
/// <see cref="FunctionInvokingChatClient"/> (tool calls). Configuration:
/// <c>{section}:OpenAICompatible:BaseUrl|ApiKey|Model|NumCtx|ReasoningEffort|RequestTimeoutSeconds|ConnectRetrySeconds</c>
/// with env fallbacks <c>CAMPAIGN_AGENT_OPENAI_BASE_URL</c>, <c>CAMPAIGN_AGENT_OPENAI_API_KEY</c>,
/// <c>CAMPAIGN_AGENT_OPENAI_MODEL</c>, plus the runtime env keys parsed by
/// <see cref="OpenAICompatibleRuntimeOptions"/>.
/// A blank <c>BaseUrl</c> defaults to the local Ollama endpoint; a blank <c>ApiKey</c> defaults to
/// <c>"ollama"</c> since local endpoints typically do not enforce one.
/// </summary>
public sealed class OpenAICompatibleLlmChatClientFactory : ILlmChatClientFactory, IDisposable
{
    private const string DefaultBaseUrl = "http://localhost:11434/v1";
    private const string DefaultApiKey = "ollama";

    private readonly IConfiguration _configuration;
    private readonly string _configurationSection;
    private readonly ILogger<OpenAICompatibleLlmChatClientFactory>? _logger;
    private OpenAIClient? _client;
    private HttpClient? _httpClient;
    private string? _lastClientFingerprint;
    private bool _disposed;

    /// <param name="configurationSection">e.g. <c>CampaignAgent</c> (no trailing colon).</param>
    public OpenAICompatibleLlmChatClientFactory(
        IConfiguration configuration,
        string configurationSection,
        ILogger<OpenAICompatibleLlmChatClientFactory>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _configurationSection = configurationSection?.Trim().TrimEnd(':')
            ?? throw new ArgumentNullException(nameof(configurationSection));
        _logger = logger;
    }

    internal static (string BaseUrl, string ApiKey, string Model) ResolveEndpoint(
        IConfiguration configuration,
        string section)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        section = section?.Trim().TrimEnd(':')
            ?? throw new ArgumentNullException(nameof(section));

        var baseUrl = configuration[$"{section}:OpenAICompatible:BaseUrl"]
                      ?? configuration["CAMPAIGN_AGENT_OPENAI_BASE_URL"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = DefaultBaseUrl;

        var apiKey = configuration[$"{section}:OpenAICompatible:ApiKey"]
                     ?? configuration["CAMPAIGN_AGENT_OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = DefaultApiKey;

        var model = configuration[$"{section}:OpenAICompatible:Model"]
                    ?? configuration["CAMPAIGN_AGENT_OPENAI_MODEL"];
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException(
                $"OpenAI-compatible model is missing ({section}:OpenAICompatible:Model or CAMPAIGN_AGENT_OPENAI_MODEL).");
        }

        return (baseUrl.Trim(), apiKey.Trim(), model.Trim());
    }

    public IChatClient CreateChatClient()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var (baseUrl, apiKey, model) = ResolveEndpoint(_configuration, _configurationSection);
        var runtime = OpenAICompatibleRuntimeOptions.Parse(_configuration, _configurationSection);

        // Recreate SDK client if endpoint/key/runtime knobs changed at runtime. Do not log ApiKey.
        var fp = $"{baseUrl}|{apiKey}|{runtime.NumCtx}|{runtime.ReasoningEffort}|{runtime.RequestTimeout}|{runtime.ConnectRetrySeconds}";
        if (_client == null || !string.Equals(_lastClientFingerprint, fp, StringComparison.Ordinal))
        {
            _logger?.LogDebug(
                "OpenAICompatibleLlmChatClientFactory: initializing OpenAIClient for section {Section} at {BaseUrl} (num_ctx={NumCtx}, reasoning={ReasoningEffort}, networkTimeout={NetworkTimeout}, connectRetrySeconds={ConnectRetrySeconds}).",
                _configurationSection,
                baseUrl,
                runtime.NumCtx,
                runtime.ReasoningEffort,
                OpenAICompatibleRuntimeOptions.ResolveNetworkTimeout(runtime.RequestTimeout),
                runtime.ConnectRetrySeconds);

            _httpClient?.Dispose();
            HttpMessageHandler pipeline = new HttpClientHandler();
            if (runtime.NumCtx is not null || runtime.ReasoningEffort is not null)
                pipeline = new OllamaChatCompletionsOptionsHandler(runtime, pipeline);

            var networkTimeout = OpenAICompatibleRuntimeOptions.ResolveNetworkTimeout(runtime.RequestTimeout);
            _httpClient = new HttpClient(pipeline)
            {
                // Entire completion (including thinking with no SSE bytes) must not hit HttpClient's 100s default.
                Timeout = Timeout.InfiniteTimeSpan
            };
            _client = new OpenAIClient(
                new ApiKeyCredential(apiKey),
                new OpenAIClientOptions
                {
                    Endpoint = new Uri(baseUrl),
                    Transport = new HttpClientPipelineTransport(_httpClient),
                    NetworkTimeout = networkTimeout
                });
            _lastClientFingerprint = fp;
        }

        // Outermost must stay FunctionInvoking so CampaignAgentChatClientStackBuilder can peel
        // a single FI layer. Retry sits inside FI (not outside); wrapping retry outside would
        // leave factory FI in place and the stack builder would add a second FI (duplicate tool_result).
        var builder = _client.GetChatClient(model).AsIChatClient()
            .AsBuilder()
            .Use(inner => new OpenAICompatibleRequestOptionsChatClient(inner, runtime));

        if (runtime.ConnectRetrySeconds > 0)
        {
            builder = builder.Use(inner => new OpenAICompatibleConnectRetryChatClient(
                inner,
                baseUrl,
                TimeSpan.FromSeconds(runtime.ConnectRetrySeconds),
                TimeSpan.FromSeconds(2),
                _logger));
        }

        return builder.UseFunctionInvocation().Build();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _httpClient?.Dispose();
        _httpClient = null;
        _client = null;
        _lastClientFingerprint = null;
    }
}
