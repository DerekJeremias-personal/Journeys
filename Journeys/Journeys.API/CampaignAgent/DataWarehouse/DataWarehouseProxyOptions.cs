namespace Journeys.API.CampaignAgent.DataWarehouse;

public sealed class DataWarehouseProxyOptions
{
    public const string SectionName = "CampaignAgent:DataWarehouseMcp";

    public bool Enabled { get; set; }

    public string? ProxyBaseUrl { get; set; }

    public int DefaultAnalysisWindowDays { get; set; } = 365;

    public int MaxRowsPerTool { get; set; } = 50;

    public int TimeoutSeconds { get; set; } = 30;
}
