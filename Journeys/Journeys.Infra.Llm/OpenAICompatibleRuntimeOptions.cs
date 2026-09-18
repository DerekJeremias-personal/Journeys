using Microsoft.Extensions.Configuration;

namespace Journeys.Infra.Llm;

/// <summary>
/// Optional Ollama/OpenAI-compatible runtime knobs: context window and reasoning/think effort.
/// Unset fields are omitted from the request so non-Ollama endpoints keep working.
/// </summary>
public sealed record OpenAICompatibleRuntimeOptions(
    int? NumCtx,
    string? ReasoningEffort,
    TimeSpan? RequestTimeout = null,
    int ConnectRetrySeconds = 0)
{
    public const int MinNumCtx = 2048;
    public const int MaxNumCtx = 128_000;
    public const int MinRequestTimeoutSeconds = 30;
    public const int MaxRequestTimeoutSeconds = 7200;
    public const int MaxConnectRetrySeconds = 600;

    /// <summary>Idle timeout between SSE bytes when config omits <c>RequestTimeoutSeconds</c>.</summary>
    public static readonly TimeSpan DefaultNetworkTimeout = TimeSpan.FromMinutes(15);

    public static TimeSpan ResolveNetworkTimeout(TimeSpan? configured) =>
        configured ?? DefaultNetworkTimeout;

    /// <summary>
    /// Parse <c>{section}:OpenAICompatible:NumCtx|ReasoningEffort|RequestTimeoutSeconds|ConnectRetrySeconds</c>
    /// with env fallbacks <c>CAMPAIGN_AGENT_OPENAI_NUM_CTX</c>, <c>CAMPAIGN_AGENT_OPENAI_REASONING_EFFORT</c>,
    /// <c>CAMPAIGN_AGENT_OPENAI_REQUEST_TIMEOUT_SECONDS</c>, and <c>CAMPAIGN_AGENT_OPENAI_CONNECT_RETRY_SECONDS</c>.
    /// </summary>
    public static OpenAICompatibleRuntimeOptions Parse(IConfiguration configuration, string configurationSection)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configurationSection?.Trim().TrimEnd(':')
            ?? throw new ArgumentNullException(nameof(configurationSection));

        var numCtx = ParseNumCtx(configuration, section);
        var effort = ParseReasoningEffort(configuration, section);
        var timeout = ParseRequestTimeout(configuration, section);
        var connectRetry = ParseConnectRetrySeconds(configuration, section);
        return new OpenAICompatibleRuntimeOptions(numCtx, effort, timeout, connectRetry);
    }

    private static int? ParseNumCtx(IConfiguration configuration, string section)
    {
        var raw = FirstNonBlank(
            configuration[$"{section}:OpenAICompatible:NumCtx"],
            configuration["CAMPAIGN_AGENT_OPENAI_NUM_CTX"]);
        if (raw is null)
            return null;

        if (!int.TryParse(raw, out var value) || value < MinNumCtx || value > MaxNumCtx)
        {
            throw new InvalidOperationException(
                $"OpenAI-compatible NumCtx is invalid ({raw}). Allowed range: {MinNumCtx}-{MaxNumCtx}.");
        }

        return value;
    }

    private static string? ParseReasoningEffort(IConfiguration configuration, string section)
    {
        var raw = FirstNonBlank(
            configuration[$"{section}:OpenAICompatible:ReasoningEffort"],
            configuration["CAMPAIGN_AGENT_OPENAI_REASONING_EFFORT"]);
        if (raw is null)
            return null;

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is not ("off" or "low" or "medium" or "high"))
        {
            throw new InvalidOperationException(
                "Unknown OpenAI-compatible ReasoningEffort. Allowed values: off, low, medium, high.");
        }

        return normalized;
    }

    private static TimeSpan? ParseRequestTimeout(IConfiguration configuration, string section)
    {
        var raw = FirstNonBlank(
            configuration[$"{section}:OpenAICompatible:RequestTimeoutSeconds"],
            configuration["CAMPAIGN_AGENT_OPENAI_REQUEST_TIMEOUT_SECONDS"]);
        if (raw is null)
            return null;

        if (!int.TryParse(raw, out var seconds))
        {
            throw new InvalidOperationException(
                $"OpenAI-compatible RequestTimeoutSeconds is invalid ({raw}). Allowed: 0 (infinite) or {MinRequestTimeoutSeconds}-{MaxRequestTimeoutSeconds}.");
        }

        if (seconds == 0)
            return Timeout.InfiniteTimeSpan;

        if (seconds < MinRequestTimeoutSeconds || seconds > MaxRequestTimeoutSeconds)
        {
            throw new InvalidOperationException(
                $"OpenAI-compatible RequestTimeoutSeconds is invalid ({seconds}). Allowed: 0 (infinite) or {MinRequestTimeoutSeconds}-{MaxRequestTimeoutSeconds}.");
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static int ParseConnectRetrySeconds(IConfiguration configuration, string section)
    {
        var raw = FirstNonBlank(
            configuration[$"{section}:OpenAICompatible:ConnectRetrySeconds"],
            configuration["CAMPAIGN_AGENT_OPENAI_CONNECT_RETRY_SECONDS"]);
        if (raw is null)
            return 0;

        if (!int.TryParse(raw, out var value) || value < 0 || value > MaxConnectRetrySeconds)
        {
            throw new InvalidOperationException(
                $"OpenAI-compatible ConnectRetrySeconds is invalid ({raw}). Allowed range: 0-{MaxConnectRetrySeconds} (0 means no retry).");
        }

        return value;
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
