namespace Journeys.API.CampaignAgent.DataWarehouse;

public interface IDataWarehouseProxyClient
{
    Task<string> GetProgramPerformanceSummaryAsync(
        string tenantId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);

    Task<string> GetCampaignOutcomeStatsAsync(
        string tenantId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);

    Task<string> GetMemberEngagementBandsAsync(
        string tenantId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);

    Task<string> GetProductCategoryLiftAsync(
        string tenantId,
        DateTimeOffset start,
        DateTimeOffset end,
        int topN,
        CancellationToken cancellationToken = default);
}
