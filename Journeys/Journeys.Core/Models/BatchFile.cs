using Microsoft.VisualBasic.FileIO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Journeys.Core.Models;

public class BatchFile : TenantedModelBase
{
    [JsonConstructor]
    public BatchFile(string directory, string fileName, string originalFileName, string modelName,
            DateTimeOffset createDate, DateTimeOffset lastUpdated, string tenantId, string? id, string jobDirectory) : base(tenantId, id)
    {
        Directory = directory;
        FileName = fileName;
        OriginalFileName = originalFileName;
        ModelName = modelName;
        CreateDate = createDate;
        LastUpdated = lastUpdated;
        JobDirectory = jobDirectory;
    }

    [JsonIgnore]
    public string? ModelId { get; set; }

    //public required string TenantId { get; set; }

    //public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Directory { get; set; }

    public string JobDirectory { get; set; }

    public string FileName { get; set; }
    
    public string OriginalFileName { get; set; }
    
    public string ModelName { get; set; }

    public DateTimeOffset CreateDate { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}