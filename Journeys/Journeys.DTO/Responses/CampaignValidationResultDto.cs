namespace Journeys.DTO.Responses;

public class CampaignValidationResultDto
{
    public bool IsValid { get; set; }
    public CampaignValidationDetailsDto Validation { get; set; } = new();
    public CampaignValidationSummaryDto Summary { get; set; } = new();
    public List<string> NextSteps { get; set; } = new();
}

public class CampaignValidationDetailsDto
{
    public List<CampaignValidationFindingDto> Errors { get; set; } = new();
    public List<CampaignValidationFindingDto> Warnings { get; set; } = new();
}

public class CampaignValidationSummaryDto
{
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int JourneyNodeCount { get; set; }
    public int RuleSetCount { get; set; }
    public bool HasJourney { get; set; }
    public string? Status { get; set; }
    public int EventModelCount { get; set; }

    public bool JourneyDeliveryReady { get; set; }
}
