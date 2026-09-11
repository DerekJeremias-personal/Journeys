using System.Text.Json;

namespace Journeys.API.Models;

public class CreateDropboxConfigRequest
{
    public string DirectoryName { get; set; }
    
    public string FileType { get; set; }
    
    public string ModelName { get; set; }

    public string? ServiceTypeName { get; set; }

    public string? MethodName { get; set; }

    /// <summary>
    /// Optional: The expected response type name for dynamic service calls.
    /// Examples: "TagDto", "Tag", "EventPayloadResponseDto"
    /// </summary>
    public string? ResponseTypeName { get; set; }

    /// <summary>
    /// Optional: Format string for generating line keys in batch result files.
    /// Examples: "{EntityId}_{Name}", "{EventNaturalKey}", "{Id}"
    /// </summary>
    public string? ResponseLineKeyFormat { get; set; }

    public JsonElement? FileMap { get; set; }
}
