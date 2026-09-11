namespace Journeys.DTO.Models;

public class BatchResultFileLineDto
{
    public string LineKey { get; set; }

    public bool Success { get; set; }

    public Dictionary<string, string>? Errors { get; set; }
    
    public int? LineNumber { get; set; }
}
