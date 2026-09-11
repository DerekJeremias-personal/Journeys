using System.Globalization;

namespace Journeys.API.CampaignAgent;

internal static class CampaignAgentToolAuditPath
{
    /// <summary>
    /// Builds compact and preview blob names (within the configured container) following
    /// {root}/{tenant}/users/{user}/days/{yyyy-MM-dd}/conversations/{conversationId}.ndjson (+ .preview.ndjson).
    /// </summary>
    public static (string CompactBlobName, string PreviewBlobName) BuildBlobNames(
        string rootPrefix,
        string tenantId,
        string ownerUserId,
        DateTime auditDayUtc,
        string conversationId)
    {
        var day = auditDayUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var baseName =
            $"{rootPrefix.TrimEnd('/')}/{EscapeSegment(tenantId)}/users/{EscapeSegment(ownerUserId)}/days/{day}/conversations/{EscapeSegment(conversationId)}";
        return ($"{baseName}.ndjson", $"{baseName}.preview.ndjson");
    }

    private static string EscapeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "_";
        return Uri.EscapeDataString(value.Trim());
    }
}
