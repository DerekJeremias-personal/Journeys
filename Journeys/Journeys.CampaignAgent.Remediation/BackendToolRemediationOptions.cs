namespace Journeys.CampaignAgent.Remediation;

public sealed class BackendToolRemediationOptions
{
    public const string SectionName = "CampaignAgent:BackendToolRemediation";

    public bool Enabled { get; set; } = true;

    public int MaxEnrichmentsPerTurn { get; set; } = 5;

    public string[] ToolAllowlist { get; set; } = ["SaveModel", "save_model"];

    public string[] JourneysToolAllowlist { get; set; } =
    [
        "UpsertCampaign", "upsert_campaign",
        "ValidateCampaign", "validate_campaign",
        "ProcessEvent", "process_event"
    ];
}
