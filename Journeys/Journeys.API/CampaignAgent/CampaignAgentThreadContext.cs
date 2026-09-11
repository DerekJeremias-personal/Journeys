namespace Journeys.API.CampaignAgent;

public sealed class CampaignAgentThreadContext
{
    public const int DefaultMaxCampaigns = 5;
    public const int DefaultMaxPointAccountTypes = 10;

    public CampaignAgentThreadCampaignRef? PrimaryCampaign { get; init; }
    public IReadOnlyList<CampaignAgentThreadCampaignRef> Campaigns { get; init; } = [];
    public IReadOnlyList<CampaignAgentThreadPatRef> PointAccountTypes { get; init; } = [];
    public int TruncatedCampaignCount { get; init; }
    public int TruncatedPointAccountTypeCount { get; init; }

    public bool IsEmpty => PrimaryCampaign is null && Campaigns.Count == 0 && PointAccountTypes.Count == 0;

    public string ToPromptBlock()
    {
        if (IsEmpty)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("--- THREAD CONTEXT (from persisted tool calls in this conversation; prefer over empty ListCampaigns) ---");
        if (PrimaryCampaign is not null)
        {
            sb.AppendLine(
                $"Primary campaign for edits this thread: \"{PrimaryCampaign.Name}\" (campaignId={PrimaryCampaign.CampaignId}, status={PrimaryCampaign.Status}).");
        }

        var others = Campaigns
            .Where(c => PrimaryCampaign is null
                        || !string.Equals(c.CampaignId, PrimaryCampaign.CampaignId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        sb.AppendLine(others.Count == 0
            ? "Other campaigns in this thread: (none)"
            : "Other campaigns in this thread: " + string.Join("; ",
                others.Select(o => $"\"{o.Name}\" ({o.CampaignId}, {o.Status})")));

        if (TruncatedCampaignCount > 0)
            sb.AppendLine($"(+ {TruncatedCampaignCount} more campaign(s) in thread not shown)");

        if (PointAccountTypes.Count == 0)
        {
            sb.AppendLine("Point account types in this thread: (none)");
        }
        else
        {
            sb.AppendLine("Point account types created/updated in this thread:");
            foreach (var p in PointAccountTypes)
                sb.AppendLine($"- {p.DisplayLabel} (id={p.Id})");
        }

        if (TruncatedPointAccountTypeCount > 0)
            sb.AppendLine($"(+ {TruncatedPointAccountTypeCount} more point account type(s) in thread not shown)");

        sb.AppendLine(
            "After a campaign id appears above, use get_campaign / get_campaign_assistant_context with that id; do not call list_campaigns.");
        return sb.ToString().TrimEnd();
    }
}

public sealed record CampaignAgentThreadCampaignRef(
    string CampaignId,
    string Status,
    string Name,
    IReadOnlyList<string>? EventModelIds = null);

public sealed record CampaignAgentThreadPatRef(string Id, string DisplayLabel);
