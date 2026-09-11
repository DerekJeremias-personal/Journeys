using System.Text.Json;
using System.Text.Json.Serialization;

namespace Journeys.Core.Models;

public class DropboxConfig : TenantedModelBase
{
    [JsonConstructor]
    public DropboxConfig(string directory, string fileType, string modelName, JsonElement? fileMap,
                DateTimeOffset createDate, DateTimeOffset lastUpdated, string serviceTypeName, string methodName, 
                string tenantId, string? id, string? responseTypeName = null, string? responseLineKeyFormat = null) : base(tenantId, id) // Updated constructor with response handling
    {
        Directory = directory;
        FileType = fileType;
        ModelName = modelName;
        FileMap = fileMap;
        CreateDate = createDate;
        LastUpdated = lastUpdated;
        ServiceTypeName = serviceTypeName;
        MethodName = methodName;
        ResponseTypeName = responseTypeName ?? null; // Ensure null handling
        ResponseLineKeyFormat = responseLineKeyFormat ?? null; // Ensure null handling
    }

    [JsonIgnore]
    public string? ModelId { get; set; }
    
    public string Directory { get; set; }
    
    public string FileType { get; set; }
    
    public string ModelName { get; set; }
    
    public JsonElement? FileMap { get; set; }

    public DateTimeOffset CreateDate { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    public string ServiceTypeName { get; set; }
    
    public string MethodName { get; set; }
    
    /// <summary>
    /// Optional: The expected response type name for dynamic service calls.
    /// Used to determine how to handle the response in batch result files.
    /// Examples: "TagDto", "Tag", "EventPayloadResponseDto"
    /// </summary>
    public string? ResponseTypeName { get; set; }
    
    /// <summary>
    /// Optional: Format string for generating line keys in batch result files.
    /// Uses property names from the response object.
    /// Examples: "{EntityId}_{Name}", "{EventNaturalKey}", "{Id}"
    /// </summary>
    public string? ResponseLineKeyFormat { get; set; }
    
}
