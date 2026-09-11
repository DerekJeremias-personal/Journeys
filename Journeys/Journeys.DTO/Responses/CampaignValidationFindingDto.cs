namespace Journeys.DTO.Responses;

public class CampaignValidationFindingDto
{
    public string Code { get; set; } = string.Empty;
    public string? Field { get; set; }
    public string? Path { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "error";
    public string? NodeId { get; set; }
    public string? NodeName { get; set; }
}
