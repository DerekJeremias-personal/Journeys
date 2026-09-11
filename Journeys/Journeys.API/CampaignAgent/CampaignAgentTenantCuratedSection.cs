using System.Security.Cryptography;
using System.Text;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Builds the TENANT CURATED CONTEXT fragment for LLM instructions (unit-testable).
/// </summary>
public static class CampaignAgentTenantCuratedSection
{
    public static TenantCuratedSectionResult Build(CampaignAgentTenantLlmSlice? slice)
    {
        if (slice == null || !slice.HasAnyContext)
            return new TenantCuratedSectionResult(null, null);

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("TENANT CURATED CONTEXT (tenant-maintained snippets; may be incomplete or outdated — prefer tools for factual campaign, member, and configuration data)");
        sb.AppendLine("---");
        sb.AppendLine("Tone: When Marketing / business context is present, use it for program vocabulary, tier or PAT nicknames, and business-specific scenarios.");
        sb.AppendLine("When End-consumer context is present, use it for member-facing voice and gamification style in narratives (stay truthful to engine mechanics in governance files).");
        sb.AppendLine("Do not invent claims that contradict these snippets or tool-verified configuration.");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(slice.MarketingContext))
        {
            sb.AppendLine("Marketing / business context:");
            if (!string.IsNullOrWhiteSpace(slice.MarketingContextAsOf))
                sb.AppendLine($"Stated as of: {slice.MarketingContextAsOf}");
            sb.AppendLine(slice.MarketingContext.Trim());
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(slice.EndConsumerContext))
        {
            sb.AppendLine("End-consumer / member audience (persona-level, not real individual data):");
            if (!string.IsNullOrWhiteSpace(slice.EndConsumerContextAsOf))
                sb.AppendLine($"Stated as of: {slice.EndConsumerContextAsOf}");
            sb.AppendLine(slice.EndConsumerContext.Trim());
            sb.AppendLine();
        }

        if (slice.HasVerificationAllowlistConfigured)
        {
            sb.AppendLine("Campaign test accounts (draft ProcessEvent verification allowlist; Tenant.campaignTestAccountExtIds):");
            if (slice.CampaignTestAccountExtIds!.Count == 0)
            {
                sb.AppendLine("(none configured — draft verification blocked until admin adds campaignTestAccountExtIds)");
            }
            else
            {
                sb.AppendLine(string.Join(", ", slice.CampaignTestAccountExtIds));
            }
        }

        var text = sb.ToString().TrimEnd();
        var hash = ComputeSha256Hex(text);
        return new TenantCuratedSectionResult(text, hash);
    }

    private static string ComputeSha256Hex(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed record TenantCuratedSectionResult(string? SectionText, string? ContentSha256Hex);
