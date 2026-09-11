using Backend.Core.Llm;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Composed prompt material for one LLM request. Governance and persona are never persisted as chat rows.
/// </summary>
public sealed class CampaignAgentLlmPromptContext
{
    public CampaignAgentLlmPromptContext(
        LlmPromptPlan promptPlan,
        IReadOnlyList<ChatMessage> ephemeralMessages,
        string governanceContentHash,
        string? tenantCuratedContentHash = null,
        string? tenantContextCacheVersion = null)
    {
        PromptPlan = promptPlan ?? throw new ArgumentNullException(nameof(promptPlan));
        Instructions = promptPlan.MergeToInstructions();
        EphemeralMessages = ephemeralMessages;
        GovernanceContentHash = governanceContentHash;
        TenantCuratedContentHash = tenantCuratedContentHash;
        TenantContextCacheVersion = tenantContextCacheVersion;
    }

    /// <summary>Portable segment plan for provider-specific prompt caching.</summary>
    public LlmPromptPlan PromptPlan { get; }

    /// <summary>Merged system instructions (persona + governance + session facts). Portable across providers.</summary>
    public string Instructions { get; }

    /// <summary>
    /// Optional messages prepended before chat history (e.g. developer-role delivery). Empty until a provider is verified.
    /// </summary>
    public IReadOnlyList<ChatMessage> EphemeralMessages { get; }

    /// <summary>SHA-256 hex of governance file content for logging and correlation (not full prompt).</summary>
    public string GovernanceContentHash { get; }

    /// <summary>SHA-256 hex of tenant curated section when present; otherwise null.</summary>
    public string? TenantCuratedContentHash { get; }

    /// <summary>Version string from tenant <c>LastUpdated</c>/<c>ETag</c> used for cache coherence.</summary>
    public string? TenantContextCacheVersion { get; }
}
